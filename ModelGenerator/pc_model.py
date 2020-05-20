import argparse
import gc
import os
import sys
import time
from datetime import datetime
import numpy as np
import seaborn as sns
import encoding
import json_utils
import pc_utils


class PointCloudModel:

    def __init__(self,
                 name,
                 dgg_cell_size=0.1,
                 parent_directory="",
                 max_file_points=65000,
                 parent_subsampling=True):

        self._dgg_cell_size = dgg_cell_size
        self._name = name
        self._parent_directory = parent_directory
        self._max_file_points = max_file_points
        self._parent_subsampling = parent_subsampling
        self._point_classes = []

        self.n_generation_stored_points = 0
        self.n_generation_points = 0
        self.generation_file = 0

    def store_las_file(self, las_path, epsg):
        xyzc = pc_utils.read_las_as_wgs84_xyzc(las_path, epsg)
        cells = pc_utils.split_xyzc_in_wgs84_normalized_cells(xyzc,
                                                              dgg_cell_size=self._dgg_cell_size)

        for c in cells:
            self._store_cell(c)

        self._save_model_descriptor()

    def _store_cell(self, cell):
        directory = "Cell_%d_%d" % cell["cell_index"]
        directory = os.path.join(self._parent_directory, self._name, directory)
        if not os.path.isdir(directory):
            os.makedirs(directory)

        xyzc = cell["xyzc"]
        index_data = []
        point_classes = np.unique(xyzc[:, 3]).tolist()
        print("\n%d points. %d classes.\n" % (xyzc.shape[0], len(point_classes)))
        self._point_classes = list(dict.fromkeys(point_classes + self._point_classes))  # add new classes

        index_file_name = "cell.json"
        index_path = os.path.join(directory, index_file_name)

        self.n_generation_points = xyzc.shape[0]
        self.n_generation_stored_points = 0

        vi = self._save_tree(xyzc, [0], out_folder=directory)

        index_data += [{"file": index_file_name,
                        "min": np.min(xyzc[:, 0:3], axis=0).tolist(),
                        "max": np.max(xyzc[:, 0:3], axis=0).tolist()}]
        json_utils.write_json(vi, index_path)

        gc.collect()  # Forcing garbage collection

    def _save_model_descriptor(self):
        desc_model = {"model_name": self._name,
                      "classes": PointCloudModel._generate_color_palette(self._point_classes)}

        path = os.path.join(self._parent_directory, self._name, "pc_model.json")
        json_utils.write_json(desc_model, path)

    def _save_tree(self, xyzc, indices, out_folder):
        n_points = xyzc.shape[0]

        if n_points == 0:
            return

        if self._parent_subsampling or n_points < self._max_file_points:
            min_xyz = np.min(xyzc[:, 0:3], axis=0)
            max_xyz = np.max(xyzc[:, 0:3], axis=0)

            node_points, remaining_points = pc_utils.random_subsampling(xyzc, self._max_file_points)
            file_name, file_path = self._get_file_path(indices, out_folder)
            encoding.matrix_to_file(node_points, file_path)

            self.n_generation_stored_points += node_points.shape[0]
            self._print_generation_state()

            voxel_index = {"min": min_xyz.tolist(),
                           "max": max_xyz.tolist(),
                           "indices": indices,
                           "filename": file_name,
                           "npoints": node_points.shape[0],
                           "avgDistance": pc_utils.aprox_average_distance(node_points[:, 0:3])}

            xyzc = remaining_points

        # Creating children
        voxel_index["children"] = self._get_children(xyzc, indices, out_folder)
        return voxel_index

    def _get_children(self, xyzc, indices, out_folder):
        children = []
        if xyzc.shape[0] >= self._max_file_points:
            # TODO change to option
            # pcs = pc_utils.split_longest_axis(xyzc)
            pcs = pc_utils.split_octree(xyzc, level=len(indices))
            for i, pc in enumerate(pcs):
                vi = self._save_tree(pc, indices + [i], out_folder=out_folder)
                children += [vi]
        return children

    @staticmethod
    def _get_file_path(indices, out_folder):
        file_name = "Node-" + "_".join( [str(n) for n in indices] ) + ".bytes"
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
    parser.add_argument("pc_model", help="Creates a hierarchical representation of a multi LAS point cloud designed "
                                         "for out-of-core rendering.")
    parser.add_argument("-d", "--directory", help="Folder with LAS files inside")
    parser.add_argument("-f", "--files", nargs="+", help="Paths to LAS files")
    parser.add_argument("-o", "--out", help="Path to output folder (default wd)", default="")
    parser.add_argument("-e", "--epsg", help="EPSG reference system number of input data (default 32631)",
                        type=int, default=32631)
    parser.add_argument("-n", "--node_points", help="Max points per node (default 65000)", type=int, default=65000)
    parser.add_argument("-s", "--subsample", help="Subsample point cloud in parent nodes.",
                        action='store_true')
    parser.add_argument("-g", "--grid_cell_size", help="Divide model in a Rectangular WGS84 Discrete Global Grid with "
                                                       "the given cell size length in degrees. (default 0.1º)",
                        type=float, default=0.1)

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

    model = PointCloudModel(name=args.pc_model,
                            dgg_cell_size=args.grid_cell_size,
                            parent_directory=args.out)

    for f in las_files:
        model.store_las_file(f, args.epsg)

    t0 = datetime.now()

    for f in las_files:
        model.store_las_file(f, args.epsg)

    t1 = datetime.now()
    td = t1 - t0

    print("\nModel generated in %f sec.\n" % td.total_seconds())
