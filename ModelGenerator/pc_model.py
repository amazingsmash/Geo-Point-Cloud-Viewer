import argparse
import gc
import os
import sys
import time
from datetime import datetime
from enum import Enum
import numpy as np
import seaborn as sns
import encoding
import json_utils
import pc_utils
from pc_node import PCNode


class PointCloudModel:

    class Partitioning(Enum):
        LONGEST_AXIS_BINTREE = 0
        REGULAR_OCTREE = 1

    def __init__(self,
                 name,
                 dgg_cell_side_length=0.1,
                 parent_directory="",
                 partitioning_method=Partitioning.REGULAR_OCTREE,
                 max_node_points=65000,
                 parent_subsampling=True):

        self._dgg_cell_side_length = dgg_cell_side_length
        self._name = name
        self._parent_directory = parent_directory
        self._max_node_points = max_node_points
        self._parent_subsampling = parent_subsampling
        self._point_classes = []
        self._partitioning_method = partitioning_method
        self._cells = []

        self.n_generation_stored_points = 0
        self.n_generation_points = 0
        self.generation_file = 0

    def store_las_file(self, las_path, epsg):
        print("Storing file %s." % las_path)
        xyzc = pc_utils.read_las_as_wgs84_xyzc(las_path, epsg)
        cells = pc_utils.split_xyzc_in_wgs84_normalized_cells(xyzc,
                                                              dgg_cell_size=self._dgg_cell_side_length)

        for c in cells:
            self._store_cell(c)

        self._save_model_descriptor()

    def _store_cell(self, cell):
        folder_name = "Cell_%d_%d" % cell["cell_index"]
        directory = os.path.join(self._parent_directory, self._name, folder_name)
        if not os.path.isdir(directory):
            os.makedirs(directory)

        points_by_class = cell["points"]
        point_classes = list(points_by_class.keys())
        n_points = pc_utils.num_points_by_class(points_by_class)
        print("\n%d points. %d classes." % (n_points, len(point_classes)))
        self._point_classes = list(dict.fromkeys(point_classes + self._point_classes))  # add new classes

        index_file_name = "cell.json"
        index_path = os.path.join(directory, index_file_name)

        self.n_generation_points = n_points
        self.n_generation_stored_points = 0

        vi = self._save_tree(PCNode(points_by_class), [0], out_folder=directory)

        cell_data = {"directory": folder_name,
                     "cell_index": cell["cell_index"],
                     "cell_min_lon_lat": cell["cell_min_lon_lat"],
                     "cell_max_lon_lat": cell["cell_max_lon_lat"],
                     "min_lon_lat_height": cell["min_lon_lat_height"],
                     "max_lon_lat_height": cell["max_lon_lat_height"]}
        self._cells += [cell_data]
        json_utils.write_json(vi, index_path)

        gc.collect()  # Forcing garbage collection

    def _save_model_descriptor(self):

        desc_model = {"model_name": self._name,
                      "dgg_cell_side_length": self._dgg_cell_side_length,
                      "max_node_points": self._max_node_points,
                      "parent_subsampling": self._parent_subsampling,
                      "partitioning_method": self._partitioning_method.name,
                      "cells": self._cells,
                      "classes": PointCloudModel._generate_color_palette(self._point_classes)}

        path = os.path.join(self._parent_directory, self._name, "pc_model.json")
        json_utils.write_json(desc_model, path)

    def _save_tree(self, node: PCNode, indices, out_folder):
        n_points = node.n_points

        if n_points == 0:
            return

        if self._parent_subsampling or n_points < self._max_node_points:
            xyz_points = node.get_all_xyz_points()

            min_xyz = np.min(xyz_points, axis=0)
            max_xyz = np.max(xyz_points, axis=0)

            sampled_node, remaining_node = node.balanced_subsampling(self._max_node_points)

            file_name, file_path = self._get_file_path(indices, out_folder)
            xyz_selected_points = sampled_node.get_all_xyz_points()
            encoding.matrix_to_file(xyz_selected_points, file_path)

            self.n_generation_stored_points += sampled_node.n_points
            self._print_generation_state()

            voxel_index = {"min": min_xyz.tolist(),
                           "max": max_xyz.tolist(),
                           "indices": indices,
                           "filename": file_name,
                           "n_points": sampled_node.n_points,
                           "avg_distance": pc_utils.aprox_average_distance(xyz_selected_points),
                           "sorted_class_count": sampled_node.sorted_class_count}

            node = remaining_node

        # Creating children
        voxel_index["children"] = self._save_children(node, indices, out_folder)
        return voxel_index

    def _save_children(self, node, indices, out_folder):
        children = []

        if node is not None:
            if self._partitioning_method == PointCloudModel.Partitioning.REGULAR_OCTREE:
                nodes = node.split_octree(level=len(indices))
            else:
                nodes = node.split_bintree_longest_axis()

            for i, node in enumerate(nodes):
                vi = self._save_tree(node, indices + [i], out_folder=out_folder)
                children += [vi]

        return children

    @staticmethod
    def _get_file_path(indices, out_folder):
        file_name = "Node-" + "_".join([str(n) for n in indices]) + ".bytes"
        file_path = os.path.join(out_folder, file_name)
        return file_name, file_path

    @staticmethod
    def _generate_color_palette(point_classes):
        palette = sns.color_palette(None, len(point_classes))
        return [{"class": c, "color": list(palette[i])} for i, c in enumerate(point_classes)]

    def _print_generation_state(self):
        msg = "Processed %f%%." % (self.n_generation_stored_points / self.n_generation_points * 100)
        sys.stdout.write('\r' + msg)
        sys.stdout.flush()
        time.sleep(0.0000000000001)


if __name__ == "__main__":

    # example: pc_model PC_MODEL_NAME -d path/to/las/folder -o path/to/out -p 32631 -n MAX_POINTS_NODE -s
    # example: pc_model PC_MODEL_NAME -f las1.las las2.las -o path/to/out
    # example: pc_model PC_MODEL_NAME -f las1.las las2.las -d path/to/las/folder -o path/to/out

    parser = argparse.ArgumentParser()
    parser.add_argument("pc_model", help="Creates a hierarchical model of the given name of a multi LAS point cloud "
                                         "designed for efficient out-of-core processing and rendering.")
    parser.add_argument("-d", "--directory", help="Folder with LAS files inside")
    parser.add_argument("-f", "--files", nargs="+", help="Paths to LAS files")
    parser.add_argument("-o", "--out", help="Path to output folder (default wd)", default="")
    parser.add_argument("-e", "--epsg", help="EPSG reference system number of input data (default 32631)",
                        type=int, default=32631)
    parser.add_argument("-n", "--node_points", help="Max points per node (default 65000)", type=int, default=65000)
    parser.add_argument("-s", "--subsample", help="Subsample point cloud in parent nodes.",
                        action='store_true')
    parser.add_argument("-g", "--grid_cell_size", help="Divide model in a Rectangular WGS84 Discrete Global Grid with "
                                                       "the given cell side length in degrees. (default 0.1º)",
                        type=float, default=0.1)
    parser.add_argument("-b", "--binary", help="Creates a binary tree, where nodes split by their longest axis,"
                                               "Otherwise, it creates a regular octree where the root node is the size of a cell",
                        action='store_true')

    args = parser.parse_args()  # getting optionals

    if args.directory is None and args.files is None:
        parser.print_help()
        sys.exit()

    las_files = args.files
    if args.directory is not None:
        las_files += pc_utils.get_las_paths_from_directory(args.directory)

    if len(las_files) == 0:
        print("No input LAS found.")
        parser.print_help()
        sys.exit()

    if args.binary:
        method = PointCloudModel.Partitioning.LONGEST_AXIS_BINTREE
    else:
        method = PointCloudModel.Partitioning.REGULAR_OCTREE

    model = PointCloudModel(name=args.pc_model,
                            dgg_cell_side_length=args.grid_cell_size,
                            parent_directory=args.out,
                            partitioning_method=method,
                            max_node_points=args.node_points,
                            parent_subsampling=args.subsample)

    t0 = datetime.now()

    for f in las_files:
        model.store_las_file(f, args.epsg)

    t1 = datetime.now()
    td = t1 - t0

    print("\nModel generated in %f sec." % td.total_seconds())
