from laspy.file import File
import numpy as np
import scipy.io as sio
import Encoding
import shutil
import os
import JSONUtils

from datetime import datetime

class PointCloudModel:

    def __init__(self, name, las_paths):

        self.__file_paths = las_paths
        self.name = name

    def add_las_path(self, path):
        self.__file_paths += [path]

    def split_two_longest_axis(xyzc, size):
        max_dim = np.argmax(size)
        m = np.median(xyzc[:, max_dim])
        division = xyzc[:, max_dim] > m
        return division


    def __save_tree(self, xyzc, indices, out_folder, max_points):
        n_points = xyzc.shape[0]

        if n_points == 0:
            return

        min_xyz = np.min(xyzc[:, 0:3], axis=0)
        max_xyz = np.max(xyzc[:, 0:3], axis=0)

        voxel_index = {"min": min_xyz.tolist(),
                       "max": max_xyz.tolist(),
                       "indices": indices}

        if n_points < max_points:
            file_name = "Node"
            for i in indices: file_name += "_" + str(i)
            file_name += ".bytes"

            file_path = "%s/%s" % (out_folder, file_name)

            Encoding.matrix_to_file(xyzc, file_path)

            voxel_index["filename"] = file_name
            voxel_index["children"] = []
        else:
            size = max_xyz - min_xyz
            division = PointCloudModel.split_two_longest_axis(xyzc, size)

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

            voxel_index["filename"] = ""
            voxel_index["children"] = [vi0, vi1]

        return voxel_index


    def generate(self, out_path="", max_file_points=65000):

        out_folder = out_path + self.name
        shutil.rmtree(out_folder, ignore_errors=True)
        os.mkdir(out_folder)

        voxel_indices = []
        index = 0
        for file in self.__file_paths:
            in_file = File(file, mode='r')

            xyzc = np.transpose(np.array([in_file.x,
                                         in_file.y,
                                         in_file.z,
                                         in_file.Classification.astype(float)]))

            if "pivot" not in locals():
                pivot = np.min(xyzc[:, 0:3], axis=0)

            xyzc[:, 0] -= pivot[0]
            xyzc[:, 1] -= pivot[1]
            xyzc[:, 2] -= pivot[2]

            vi = self.__save_tree(xyzc,
                                  [index],
                                  out_folder=out_folder,
                                  max_points=max_file_points)
            voxel_indices += [vi]

        JSONUtils.writeJSON(voxel_indices, out_folder + "/voxelIndex.json")

            # # Testing write speed
            # t0 = datetime.now()
            # sio.savemat('lasMatlab.mat', {'xyzc': xyzc})
            # t1 = datetime.now()
            # td = t1-t0
            # print(td.microseconds * (10**-6))
            # print(xyzc.shape)

if __name__ == "__main__":

    t0 = datetime.now()
    model = PointCloudModel("Model 18", ["../Data/000018.las"])
    model.generate("../Models/")
    t1 = datetime.now()
    td = t1-t0

    print("Model Generated in %f sec." % td.total_seconds())