import gc
import os
import sys
import time
from enum import Enum
import numpy as np
import seaborn as sns
import encoding
import jsonutils
import pcutils
from globalgrid import GlobalGrid, GlobalGridCell
from pcnode import PCNode
import shutil


class GeoPointCloudModel:

    class Partitioning(Enum):
        LONGEST_AXIS_BINTREE = 0
        REGULAR_OCTREE = 1

    def __init__(self,
                 name,
                 global_grid: GlobalGrid,
                 parent_directory="",
                 partitioning_method: Partitioning = Partitioning.REGULAR_OCTREE,
                 max_node_points=65000,
                 parent_sampling=True,
                 balanced_sampling=True):

        self._global_grid = global_grid
        self._name = name
        self._parent_directory = parent_directory
        self._max_node_points = max_node_points
        self._parent_sampling = parent_sampling
        self._balanced_sampling = balanced_sampling
        self._point_classes = []
        self._partitioning_method = partitioning_method
        self._cells = []

        self.n_generation_stored_points = 0
        self.n_generation_points = 0
        self.generation_file = 0

        shutil.rmtree(self.model_directory(), ignore_errors=True)

    def store_las_files(self, las_paths, epsg_num):
        modelpath = self.model_directory()
        cell_indices = self._global_grid.store_points_in_cell_folders(modelpath, las_paths, epsg_num)
        cell_generator = self._global_grid.cell_generator(modelpath, cell_indices)
        for c in cell_generator:
            self._store_cell(c)

        self._save_model_descriptor()

    def model_directory(self): return os.path.join(self._parent_directory, self._name)

    def _store_cell(self, cell: GlobalGridCell):
        folder_name = "Cell_%d_%d" % tuple(cell.xy_index)
        directory = os.path.join(self._parent_directory, self._name, folder_name)
        if not os.path.isdir(directory):
            os.makedirs(directory)

        point_classes = list(cell.points_by_class.keys())
        n_points = pcutils.num_points_by_class(cell.points_by_class)
        print("\n%d points. %d classes." % (n_points, len(point_classes)))
        self._point_classes = list(dict.fromkeys(point_classes + self._point_classes))  # add new classes

        index_file_name = "cell.json"
        index_path = os.path.join(directory, index_file_name)

        self.n_generation_points = n_points
        self.n_generation_stored_points = 0

        vi = self._save_tree(PCNode(cell.points_by_class), [0], out_folder=directory)

        cell_data = cell.get_descriptor()
        cell_data["directory"] = folder_name

        self._cells += [cell_data]
        jsonutils.write_json(vi, index_path)

        gc.collect()  # Forcing garbage collection

    def _save_model_descriptor(self):

        desc_model = {"model_name": self._name,
                      "global_grid": self._global_grid.get_descriptor(),
                      "max_node_points": self._max_node_points,
                      "parent_sampling": self._parent_sampling,
                      "partitioning_method": self._partitioning_method.name,
                      "cells": self._cells,
                      "classes": GeoPointCloudModel._generate_color_palette(self._point_classes)}

        path = os.path.join(self._parent_directory, self._name, "pc_model.json")
        jsonutils.write_json(desc_model, path)

    def _save_tree(self, node: PCNode, indices, out_folder):
        n_points = node.n_points

        if n_points == 0:
            return

        if self._parent_sampling or n_points < self._max_node_points:
            xyz_points = node.get_all_xyz_points()

            min_xyz = np.min(xyz_points, axis=0)
            max_xyz = np.max(xyz_points, axis=0)

            sampled_node, remaining_node = node.sample(self._max_node_points, balanced=self._balanced_sampling)

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
                           "avg_distance": pcutils.aprox_average_distance(xyz_selected_points),
                           "sorted_class_count": sampled_node.sorted_class_count}

            node = remaining_node

        # Creating children
        voxel_index["children"] = self._save_children(node, indices, out_folder)
        return voxel_index

    def _save_children(self, node, indices, out_folder):
        children = []

        if node is not None:
            if self._partitioning_method == GeoPointCloudModel.Partitioning.REGULAR_OCTREE:
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
