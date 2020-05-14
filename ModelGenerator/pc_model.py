import argparse
import sys
from getopt import getopt

from laspy.file import File
import numpy as np
import encoding
import shutil
import os
import json_utils
import math
import gc
import seaborn as sns
from datetime import datetime
import pc_utils
import time


class PointCloudModel:

    def __init__(self,
                 name,
                 las_paths=None,
                 las_folder=None,
                 epsg_num=4326):

        if las_folder is not None:
            file_paths = [os.path.join(las_folder, f) for f in os.listdir(las_folder) if ".las" in f]
            las_paths = file_paths if las_paths is None else las_paths + file_paths

        self.__file_paths = las_paths
        self.name = name
        self.epsg_num = epsg_num

        self.n_generation_stored_points = 0
        self.n_generation_points = 0
        self.generation_file = 0

    def add_las_path(self, path):
        self.__file_paths += [path]

    def get_las_paths_in_folder(self, folder_path):
        self.__file_paths += [folder_path + "/" + f for f in os.listdir(folder_path) if ".las" in f]

    @staticmethod
    def __split_two_longest_axis(xyzc, size):
        max_dim = np.argmax(size)
        m = np.median(xyzc[:, max_dim])
        division = xyzc[:, max_dim] > m

        s_div = sum(division)
        if s_div == 0 or s_div == xyzc.shape[0]:
            division = xyzc[:, max_dim] >= m

        return division

    @staticmethod
    def __random_subsampling(xyzc, max_points):
        n_points = xyzc.shape[0]
        if n_points < max_points:
            return xyzc, np.array([0, 4])

        selection = np.random.choice(n_points, size=max_points, replace=False)
        inverse_mask = np.ones(n_points, np.bool)
        inverse_mask[selection] = 0

        selection = xyzc[selection, :]
        non_selected = xyzc[inverse_mask, :]
        return selection, non_selected

    @staticmethod
    def __aprox_average_distance(xyz):
        xyz0 = np.random.permutation(xyz)
        xyz1 = np.random.permutation(xyz)
        d = xyz1 - xyz0
        d = d[:, 0] ** 2 + d[:, 1] ** 2 + d[:, 2] ** 2
        d = np.mean(d)
        return math.sqrt(d)

    def __save_tree(self, xyzc, indices, out_folder, max_points, subsample):
        n_points = xyzc.shape[0]

        if n_points == 0:
            return

        if subsample or n_points < max_points:

            min_xyz = np.min(xyzc[:, 0:3], axis=0)
            max_xyz = np.max(xyzc[:, 0:3], axis=0)

            voxel_index = {"min": min_xyz.tolist(),
                           "max": max_xyz.tolist(),
                           "indices": indices}

            node_points, remaining_points = PointCloudModel.__random_subsampling(xyzc, max_points)
            file_name, file_path = self.__get_file_path(indices, out_folder)
            encoding.matrix_to_file(node_points, file_path)

            self.n_generation_stored_points += node_points.shape[0]
            self.__print_generation_state()

            voxel_index["filename"] = file_name
            voxel_index["npoints"] = node_points.shape[0]
            voxel_index["avgDistance"] = PointCloudModel.__aprox_average_distance(node_points[:, 0:3])

            xyzc = remaining_points
            n_points = xyzc.shape[0]

        # Creating children
        if n_points < max_points:
            voxel_index["children"] = []
        else:
            size = max_xyz - min_xyz
            division = PointCloudModel.__split_two_longest_axis(xyzc, size)

            xyzc0 = xyzc[division, :]
            vi0 = self.__save_tree(xyzc0,
                                   indices + [0],
                                   out_folder=out_folder,
                                   max_points=max_points)

            xyzc1 = xyzc[np.logical_not(division), :]
            vi1 = self.__save_tree(xyzc1,
                                   indices + [1],
                                   out_folder=out_folder,
                                   max_points=max_points)
            voxel_index["children"] = [vi0, vi1]

        return voxel_index

    @staticmethod
    def __get_file_path(indices, out_folder):
        file_name = "Node" + "_".join(str(indices)) + ".bytes"
        file_path = os.path.join(out_folder, file_name)
        return file_name, file_path

    @staticmethod
    def __generate_color_palette(point_classes):
        palette = sns.color_palette(None, len(point_classes))
        return [{"class": c, "color": list(palette[i])} for i, c in enumerate(point_classes)]

    def __print_generation_state(self):
        msg = "Processed %f%%." % (self.n_generation_stored_points / self.n_generation_points)
        sys.stdout.write('\r' + msg)
        sys.stdout.flush()
        time.sleep(0.0000000000001)

    def generate(self, out_path="", max_file_points=65000, max_las_files=None, subsample=True):
        out_folder = out_path + self.name
        shutil.rmtree(out_folder, ignore_errors=True)
        os.mkdir(out_folder)

        voxel_indices = []
        bounds = []
        point_classes = []

        files = self.__file_paths
        if max_las_files is not None: files = files[:max_las_files]

        for index, file in enumerate(files):

            index_file_name = "tree_%d.json" % index

            print("Processing file %s" % file)
            in_file = File(file, mode='r')

            lat, lon = pc_utils.convert_las_to_wgs84(in_file.x, in_file.y, self.epsg_num, show_map=False)
            h = in_file.z

            sector = pc_utils.get_sector(lat, lon)
            print("Sector ", end="")
            print(sector)

            xyzc = np.transpose(np.array([in_file.x,
                                          in_file.y,
                                          in_file.z,
                                          in_file.Classification.astype(float)]))

            if "xyz_offset" not in locals():
                xyz_offset = np.min(xyzc[:, 0:3], axis=0)

            xyzc[:, 0] -= xyz_offset[0]
            xyzc[:, 1] -= xyz_offset[1]
            xyzc[:, 2] -= xyz_offset[2]

            cs = np.unique(xyzc[:, 3]).tolist()
            point_classes += [c for c in cs if c not in point_classes]

            self.n_generation_points = xyzc.shape[0]
            self.n_generation_stored_points = 0
            print("%d points. %d classes." % (xyzc.shape[0], len(point_classes)))

            vi = self.__save_tree(xyzc,
                                  [index],
                                  out_folder=out_folder,
                                  max_points=max_file_points,
                                  subsample=subsample)

            bounds += [vi["min"], vi["max"]]

            voxel_indices += [{"file": index_file_name,
                               "min": vi["min"],
                               "max": vi["max"]}]
            json_utils.write_json(vi, out_folder + "/" + index_file_name)

            gc.collect()  # Forcing garbage collection

        bounds = np.array(bounds)
        xyz_min = np.min(bounds, axis=0)
        xyz_max = np.max(bounds, axis=0)

        model = {"model_name": self.name,
                 "xyz_offset": xyz_offset.tolist(),
                 "min": xyz_min.tolist(),
                 "max": xyz_max.tolist(),
                 "classes": PointCloudModel.__generate_color_palette(point_classes),
                 "nodes": voxel_indices
                 }

        json_utils.write_json(model, out_folder + "/pc_model.json")


if __name__ == "__main__":

    # example: pc_model PC_MODEL_NAME -d path/to/las/folder -o path/to/out -p 32631 -n MAX_POINTS_NODE -s True
    # example: pc_model PC_MODEL_NAME -f las1.las las2.las -o path/to/out
    # example: pc_model PC_MODEL_NAME -f las1.las las2.las -d path/to/las/folder -o path/to/out

    parser = argparse.ArgumentParser(prog='pc_model PC_MODEL_NAME')
    parser.add_argument("-d", help="Folder with LAS files inside")
    parser.add_argument("-f", nargs="+",  help="Paths to LAS files")
    parser.add_argument("-o", help="Path to output folder (default wd)", default="")
    parser.add_argument("-e", help="EPSG num (default 32631)", type=int, default=32631)
    parser.add_argument("-n", help="Max points per node (default 65000)", type=int, default=65000)
    parser.add_argument("-s", help="Subsample PC in parent nodes (default True)", type=bool, default=True)

    parser.print_help()
    arg = parser.parse_args(sys.argv[2:]) #getting optionals

    model_name = sys.argv[1]
    las_folder = arg.d
    las_files = arg.f
    out_model_path = arg.o
    epsg_num = arg.e
    max_points_per_node = arg.n
    subsample = arg.s

    if las_folder is None and las_files is None:
        parser.print_help()
        sys.exit()

    model = PointCloudModel("LAS_MODEL",
                            las_files,
                            las_folder=las_folder,
                            epsg_num=epsg_num)
    out_model_path = "../Models/"
    t0 = datetime.now()
    model.generate(out_model_path,
                   max_file_points=max_points_per_node,
                   subsample=True)
    t1 = datetime.now()
    td = t1 - t0

    print("Model Generated in %f sec." % td.total_seconds())
