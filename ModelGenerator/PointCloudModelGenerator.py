from laspy.file import File
import numpy as np
import scipy.io as sio
import Encoding
import shutil
import os
import JSONUtils
from pyproj import CRS, Transformer, exceptions
import webbrowser

from datetime import datetime


class PointCloudModel:

    def __init__(self, name, las_paths, epsg_num=4326):
        self.__file_paths = las_paths
        self.name = name
        self.epsg_num = epsg_num

    def add_las_path(self, path):
        self.__file_paths += [path]

    def split_two_longest_axis(xyzc, size):
        max_dim = np.argmax(size)
        m = np.median(xyzc[:, max_dim])
        division = xyzc[:, max_dim] > m

        s_div = sum(division)
        if s_div == 0 or s_div == xyzc.shape[0]:
            division = xyzc[:, max_dim] >= m

        return division

    def __save_tree(self, xyzc, indices, out_folder, max_points):
        """Stores tree nodes recursively on disk."""

        print("Saving tree for %d points. Recursion Level %d." % (xyzc.shape[0], len(indices)));

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

    @staticmethod
    def __discover_epsg(las_x, las_y):

        ref_point = np.array([40.4165, -3.70256])
        point = np.array([(np.min(las_x) + np.max(las_x)) / 2, (np.min(las_y) + np.max(las_y)) / 2])

        crs_4326 = CRS.from_epsg(4326)

        dists = {}

        for i in range(2000, 10000):
            try:
                crs_in = CRS.from_epsg(i)
                transformer = Transformer.from_crs(crs_from=crs_in, crs_to=crs_4326)
                lat, lon = transformer.transform(point[0], point[1])
                new_point = np.array([lat, lon])
                d = np.linalg.norm(ref_point-new_point)
                dists[i] = d
                print("EPSG %d -> %f" % (i, d))
                if d < 10:
                    url = "http://maps.google.com/maps?q=%f,%f" % (lat, lon)
                    webbrowser.open(url)

            except exceptions.CRSError:
                pass



    @staticmethod
    def __convert_las_to_wgs84(las_x, las_y, epsg_num=32733, show_map=True):

        if epsg_num is 4326:
            return las_x, las_y

        crs_in = CRS.from_epsg(epsg_num)
        crs_4326 = CRS.from_epsg(4326)
        transformer = Transformer.from_crs(crs_from=crs_in, crs_to=crs_4326)

        lat, lon = transformer.transform(las_x, las_y)

        if show_map:
            url = "http://maps.google.com/maps?q=%f,%f" % ((np.min(lat) + np.max(lat))/2, (np.min(lon) + np.max(lon))/2)
            webbrowser.open(url)

        return lat, lon

    @staticmethod
    def __get_sector(lat, lon):
        sector = (np.min(lat), np.min(lon), np.max(lat), np.max(lon))
        return sector

    def generate(self, out_path="", max_file_points=65000):
        """Stores tree model on disk. Nodes have a max size of max_file_points"""

        out_folder = os.path.join(out_path, self.name)
        if (os.path.exists(out_folder)):
            shutil.rmtree(out_folder, ignore_errors=True)
        os.makedirs(out_folder)

        voxel_indices = []
        index = 0
        for file in self.__file_paths:
            in_file = File(file, mode='r')

            # PointCloudModel.__discover_epsg(in_file.x, in_file.y)

            lat, lon = PointCloudModel.__convert_las_to_wgs84(in_file.x, in_file.y, self.epsg_num, show_map=False)
            h = in_file.z

            sector = PointCloudModel.__get_sector(lat, lon)
            print("Sector ", end="")
            print(sector)

            xyzc = np.transpose(np.array([lon,
                                         lat,
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
    # model = PointCloudModel("000029_NS", ["../Data/000029.las"])
    # model = PointCloudModel("DEMO", ["000052.las"], epsg_num=2062)
    model = PointCloudModel("LASBCN", ["000001.las"], epsg_num=32631)
    model.generate("../Models")
    t1 = datetime.now()
    td = t1-t0

    print("Model Generated in %f sec." % td.total_seconds())