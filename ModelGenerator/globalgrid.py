import gc
import math

import pcutils
import numpy as np
import os
import encoding
from typing import Tuple


class GlobalGrid:

    def store_points_in_cell_folders(self, modelpath, las_paths: list, epsg_num: int) -> np.ndarray:
        pass

    def cell_generator(self, modelpath, cell_indices: np.ndarray):
        pass

    def generate_cells_from_las(self, las_path, las_epsg) -> list:
        pass

    def get_descriptor(self) -> dict:
        pass


class GlobalGridCell:

    def __init__(self, xy_index, point_xyz, point_classes, cell_xy_min, cell_side_length):

        self.xy_index = xy_index
        self._pc_bounds_min = np.min(point_xyz, axis=0)
        self._pc_bounds_max = np.max(point_xyz, axis=0)
        h_min = self._pc_bounds_min[2]
        self._cell_extent_min = np.hstack((cell_xy_min, h_min))
        self._cell_extent_max = self._cell_extent_min + cell_side_length
        cell_extent = np.array([cell_side_length, cell_side_length, cell_side_length])

        # Normalizing points in -1 - 1 space
        cell_center = (self._cell_extent_min + self._cell_extent_max) / 2
        cell_xyz_normalized = (point_xyz - cell_center) / (cell_extent / 2)
        epsilon = 1e-6
        assert np.min(np.min(cell_xyz_normalized)) >= -1-epsilon and np.max(np.max(cell_xyz_normalized)) <= 1+epsilon
        cell_xyz_normalized = np.clip(cell_xyz_normalized, -1, 1)

        self.points_by_class = pcutils.divide_points_by_class(cell_xyz_normalized, point_classes)

    # MIN_HEIGHT = -1000 # Min Representable Height
    #
    # def __init__(self, modelpath, xy_index, cell_xy_min, cell_side_length):
    #
    #     points = encoding.file_to_matrix(GlobalGridCell.descriptor_path(modelpath, xy_index))
    #     cell_xyz_normalized = points[:, 0:3]
    #     point_classes = points[:, 3]
    #
    #     self.cell_extent_min = np.hstack((cell_xy_min, GlobalGridCell.MIN_HEIGHT))
    #     self.cell_extent_max = self.cell_extent_min + cell_side_length
    #     cell_center = (self.cell_extent_min + self.cell_extent_max) / 2
    #     cell_extent = np.array([cell_side_length, cell_side_length, cell_side_length])
    #
    #     self.xy_index = xy_index
    #     self.pc_bounds_min = np.min(cell_xyz_normalized, axis=0) * (cell_extent / 2) + cell_center
    #     self.pc_bounds_max = np.max(cell_xyz_normalized, axis=0) * (cell_extent / 2) + cell_center
    #
    #     self.points_by_class = pcutils.divide_points_by_class(cell_xyz_normalized, point_classes)

    @staticmethod
    def folder_path(xy_index): return "Cell_%d_%d" % tuple(xy_index)

    @staticmethod
    def descriptor_path(modelpath, xy_index):
        return os.path.join(modelpath, GlobalGridCell.folder_path(xy_index), "cell.json")

    @staticmethod
    def all_points_path(modelpath, xy_index):
        return os.path.join(modelpath, GlobalGridCell.folder_path(xy_index), "points.bytes")

    # @staticmethod
    # def store_points_in_folder(modelpath, points, xy_index, cell_xy_min, cell_side_length):
    #     point_xyz = points[:, 0:3]
    #
    #     cell_extent_min = np.hstack((cell_xy_min, GlobalGridCell.MIN_HEIGHT))
    #     cell_extent_max = cell_extent_min + cell_side_length
    #     cell_extent = np.array([cell_side_length, cell_side_length, cell_side_length])
    #
    #     # Normalizing points in -1 - 1 space
    #     cell_center = (cell_extent_min + cell_extent_max) / 2
    #     cell_xyz_normalized = (point_xyz - cell_center) / (cell_extent / 2)
    #     epsilon = 1e-6
    #     assert np.min(np.min(cell_xyz_normalized)) >= -1-epsilon and np.max(np.max(cell_xyz_normalized)) <= 1+epsilon
    #     cell_xyz_normalized = np.clip(cell_xyz_normalized, -1, 1)
    #     points[:, 0:3] = cell_xyz_normalized
    #
    #     encoding.append_rows_to_file(GlobalGridCell.descriptor_path(modelpath, xy_index), points)

    @staticmethod
    def store_points_double(modelpath, xy_index, points):
        print("Storing data for Cell %d x %d" % (xy_index[0], xy_index[1]))
        n = 0
        while True:
            path = os.path.join(modelpath, GlobalGridCell.folder_path(xy_index), "points_%d.bytes" % n)
            if not os.path.exists(path):
                break
            else:
                n = n + 1

        encoding.matrix_to_file_double(points, path)


    @staticmethod
    def get_all_points(modelpath, xy_index):
        points = None
        n = 0
        while True:
            path = os.path.join(modelpath, GlobalGridCell.folder_path(xy_index), "points_%d.bytes" % n)
            if not os.path.exists(path):
                break

            ps = encoding.file_to_matrix_double(path)
            points = ps if points is None else np.vstack((points, ps))
            n = n + 1

        return points

    @staticmethod
    def test_precision_loss(point_xyz, cell_xy_min, cell_side_length):
        pc_bounds_min = np.min(point_xyz, axis=0)
        h_min = pc_bounds_min[2]
        cell_extent_min = np.hstack((cell_xy_min, h_min))
        cell_extent = np.array([cell_side_length, cell_side_length, cell_side_length])

        cell_xyz_normalized = (point_xyz - cell_extent_min) / cell_extent
        cell_xyz_normalized_float = cell_xyz_normalized.astype(np.single)

        points = cell_xyz_normalized_float.astype(np.double)
        points = points * cell_extent + cell_extent_min

        ds = point_xyz - points
        ds = np.linalg.norm(ds, axis=1)
        print("Precision Loss due to Double To Float Conversion  = %d m." % np.max(ds))

    def get_descriptor(self) -> dict:
        return {"cell_index": tuple(self.xy_index),
                "cell_extent_min": self._cell_extent_min.tolist(),
                "cell_extent_max": self._cell_extent_max.tolist(),
                "pc_bounds_min": self._pc_bounds_min.tolist(),
                "pc_bounds_max": self._pc_bounds_max.tolist()}


class TileMapServiceGG(GlobalGrid):
    """Reference: https://wiki.osgeo.org/wiki/Tile_Map_Service_Specification
                    https://www.maptiler.com/google-maps-coordinates-tile-bounds-projection/
    """

    MAP_SIDE_LENGTH_METERS = 40075016.6784

    def __init__(self, level: int):
        self.level = level
        self.side_n_tiles = 2 ** self.level
        self.cell_side_lenght_meters = TileMapServiceGG.MAP_SIDE_LENGTH_METERS / self.side_n_tiles

    def get_bounds_from_cell_indices(self, cell_indices: np.ndarray) -> Tuple[np.ndarray, np.ndarray]:
        cell_xy_mins = cell_indices * self.cell_side_lenght_meters - (TileMapServiceGG.MAP_SIDE_LENGTH_METERS / 2)
        cell_xy_maxs = cell_xy_mins + self.cell_side_lenght_meters - (TileMapServiceGG.MAP_SIDE_LENGTH_METERS / 2)
        return cell_xy_mins, cell_xy_maxs

    def store_points_in_cell_folders(self, modelpath, las_paths: list, epsg_num: int) -> np.ndarray:
        # Separating point sets
        cell_indices_set = set()
        las_index = 0
        while las_index < len(las_paths):
            # coordinates in spherical mercator
            try:
                las_points = np.zeros((0, 4))
                while las_points.nbytes <  1 * 1024 * 1024 * 1024 and las_index < len(las_paths): # 1 GB
                    las_path = las_paths[las_index]
                    las_index = las_index + 1                      # next
                    print("Processing LAS %s" % las_path)
                    las_points_i = pcutils.read_las_as_spherical_mercator_xyzc(las_path, epsg_num=epsg_num)
                    las_points = np.vstack((las_points, las_points_i))

            except Exception:
                print("Problem reading LAS %s" % las_path)
                continue

            tile_indices = np.floor((las_points[:, 0:2] + (TileMapServiceGG.MAP_SIDE_LENGTH_METERS / 2)) / self.cell_side_lenght_meters)
            tile_flat_indices = tile_indices[:, 0] * self.side_n_tiles + tile_indices[:, 1]

            unique_indices, cell_indices = np.unique(tile_flat_indices, return_index=True)

            cell_indices = tile_indices[cell_indices, :]
            cell_xy_mins, cell_xy_maxs = self.get_bounds_from_cell_indices(cell_indices)

            for i, xy_index, cell_xy_min, cell_xy_max in zip(unique_indices, cell_indices, cell_xy_mins, cell_xy_maxs):
                cell_indices_set.add((xy_index[0], xy_index[1]))
                point_indices = np.where(tile_flat_indices == i)[0]
                ps = las_points[point_indices, :]
                GlobalGridCell.store_points_double(modelpath, xy_index, ps)
                gc.collect()

        print("Point disk storage completed")
        cell_indices = np.array(list(cell_indices_set))
        return cell_indices

    def cell_generator(self, modelpath, cell_indices: np.ndarray):
        gc.collect()
        print("Identified Cells: ", cell_indices)
        cell_xy_mins, cell_xy_maxs = self.get_bounds_from_cell_indices(cell_indices)
        cells = []
        for xy_index, cell_xy_min, cell_xy_max in zip(cell_indices, cell_xy_mins, cell_xy_maxs):
            print("Generating Cell %d x %d" % (xy_index[0], xy_index[1]))
            ps = GlobalGridCell.get_all_points(modelpath, xy_index)
            point_xyz = ps[:, 0:3]
            point_classes = ps[:, 3]
            c = GlobalGridCell(xy_index, point_xyz, point_classes, cell_xy_min, self.cell_side_lenght_meters)
            yield c

    def get_descriptor(self) -> dict:
        return {
            "type": "TileMapServiceGG",
            "level": self.level,
            "side_n_tiles": self.side_n_tiles,
            "tile_size_meters": self.cell_side_lenght_meters
        }


# TODO Implement
class LatLonGG(GlobalGrid):
    pass
