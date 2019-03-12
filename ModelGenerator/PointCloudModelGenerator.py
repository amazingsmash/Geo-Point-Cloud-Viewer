from laspy.file import File
import numpy as np
import scipy.io as sio
import Encoding
import shutil
import os
import JSONUtils

from datetime import datetime

class PointCloudModel:

    def __init__(self, name, lasFilePaths):

        self.__file_paths = lasFilePaths
        self.name = name

    def addLASFilePath(self, path):
        self.__file_paths += [path]

    def splitInTwoByLargestAxis(xyzc, size):
        max_dim = np.argmax(size)
        m = np.median(xyzc[:, max_dim])
        division = xyzc[:, max_dim] > m
        return division


    def __save_tree(self, xyzc, indices):
        n_points = xyzc.shape[0]

        if n_points == 0:
            return

        min = np.min(xyzc[:, 0:3], axis=0)
        max = np.max(xyzc[:, 0:3], axis=0)

        voxelIndex = {}

        voxelIndex["min"] = min.tolist()
        voxelIndex["max"] = max.tolist()
        voxelIndex["indices"] = indices

        if n_points < self.__max_file_points:
            file_name = "Node"
            for i in indices: file_name += "_" + str(i)
            file_name += ".bytes"

            file_path = "%s/%s" % (self.__out_folder, file_name)

            Encoding.saveMatrixToFile(xyzc, file_path)

            voxelIndex["filename"] = file_name
            voxelIndex["children"] = []
        else:
            size = max - min
            division = PointCloudModel.splitInTwoByLargestAxis(xyzc, size)

            xyzc0 = xyzc[division, :]
            vi0 = self.__save_tree(xyzc0, indices + [0])

            xyzc1 = xyzc[np.logical_not(division), :]
            vi1 = self.__save_tree(xyzc1, indices + [1])

            voxelIndex["filename"] = ""
            voxelIndex["children"] = [vi0, vi1]

        return voxelIndex


    def generate(self, outPath="", max_file_points=65000):

        self.__out_folder = outPath + self.name
        self.__max_file_points = max_file_points
        shutil.rmtree(self.__out_folder, ignore_errors=True)
        os.mkdir(self.__out_folder)

        voxelIndices = []
        index = 0
        for file in self.__file_paths:
            in_file = File(file, mode='r')

            xyzc = np.transpose(np.array([in_file.X / 100, # Reading in cm.
                                         in_file.Y / 100,
                                         in_file.Z / 100,
                                         in_file.Classification.astype(float)]))

            if "pivot" not in locals():
                pivot = np.min(xyzc[:, 0:3], axis=0)

            xyzc[:, 0] -= pivot[0]
            xyzc[:, 1] -= pivot[1]
            xyzc[:, 2] -= pivot[2]

            vi = self.__save_tree(xyzc, [index])
            voxelIndices += [vi]

        JSONUtils.writeJSON(voxelIndices, self.__out_folder + "/voxelIndex.json")

            # # Testing write speed
            # t0 = datetime.now()
            # sio.savemat('lasMatlab.mat', {'xyzc': xyzc})
            # t1 = datetime.now()
            # td = t1-t0
            # print(td.microseconds * (10**-6))
            # print(xyzc.shape)

if __name__ == "__main__":

    t0 = datetime.now()
    model = PointCloudModel("Model 92", ["../Data/000092.las"])
    model.generate("../Models/")
    t1 = datetime.now()
    td = t1-t0

    print("Model Generated in %f sec." % td.total_seconds())


