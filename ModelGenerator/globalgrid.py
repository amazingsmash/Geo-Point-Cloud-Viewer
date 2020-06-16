import math

import pcutils
import numpy as np


class GlobalGrid:

    def generate_cells_from_las(self, las_path, las_epsg) -> list:
        pass

    def get_descriptor(self) -> dict:
        pass


class GlobalGridCell:

    def __init__(self, xy_index, point_xyz, point_classes, cell_xy_min, cell_side_length):

        self.xy_index = xy_index
        self.pc_bounds_min = np.min(point_xyz, axis=0)
        self.pc_bounds_max = np.max(point_xyz, axis=0)
        h_min = self.pc_bounds_min[2]
        self.cell_extent_min = np.hstack((cell_xy_min, h_min))
        self.cell_extent_max = self.cell_extent_min + cell_side_length
        cell_extent = np.array([cell_side_length, cell_side_length, cell_side_length])

        cell_xyz_normalized = (point_xyz - self.cell_extent_min) / cell_extent

        assert np.min(np.min(cell_xyz_normalized)) >= 0 and np.max(np.max(cell_xyz_normalized)) <= 1

        self.points_by_class = pcutils.divide_points_by_class(cell_xyz_normalized, point_classes)

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
                "cell_extent_min": self.cell_extent_min.tolist(),
                "cell_extent_max": self.cell_extent_max.tolist(),
                "pc_bounds_min": self.pc_bounds_min.tolist(),
                "pc_bounds_max": self.pc_bounds_max.tolist()}


class TileMapServiceGG(GlobalGrid):
    """Reference: https://wiki.osgeo.org/wiki/Tile_Map_Service_Specification
                    https://www.maptiler.com/google-maps-coordinates-tile-bounds-projection/
    """

    MAP_SIDE_LENGTH_METERS = 40075016.6784

    def __init__(self, level: int):
        self.level = level
        self.side_n_tiles = 2 ** self.level
        self.tile_size_meters = TileMapServiceGG.MAP_SIDE_LENGTH_METERS / self.side_n_tiles

    def generate_cells_from_las(self, las_paths: list, epsg_num: int) -> list:
        xyzc = np.zeros((0,4));

        for las_path in las_paths:
            # coordinates in spherical mercator
            l_xyzc = pcutils.read_las_as_spherical_mercator_xyzc(las_path, epsg_num=epsg_num)
            xyzc = np.vstack((xyzc, l_xyzc))

        tile_indices = np.floor((xyzc[:, 0:2] + (TileMapServiceGG.MAP_SIDE_LENGTH_METERS / 2)) / self.tile_size_meters)
        tile_flat_indices = tile_indices[:, 0] * self.side_n_tiles + tile_indices[:, 1]

        unique_indices, cell_indices = np.unique(tile_flat_indices, return_index=True)

        cell_indices = tile_indices[cell_indices, :]
        cell_xy_mins = cell_indices * self.tile_size_meters - (TileMapServiceGG.MAP_SIDE_LENGTH_METERS / 2)
        cell_xy_maxs = cell_xy_mins + self.tile_size_meters - (TileMapServiceGG.MAP_SIDE_LENGTH_METERS / 2)

        cells = []
        for i, xy_index, cell_xy_min, cell_xy_max in zip(unique_indices, cell_indices, cell_xy_mins, cell_xy_maxs):
            point_indices = np.where(tile_flat_indices == i)[0]
            cell_point_classes = xyzc[point_indices, 3]
            cell_point_xyz = xyzc[point_indices, 0:3]

            # GlobalGridCell.test_precision_loss(cell_point_xyz, cell_xy_min, self.tile_size_meters)

            cells += [GlobalGridCell(xy_index, cell_point_xyz, cell_point_classes, cell_xy_min, self.tile_size_meters)]

        return cells

    def get_descriptor(self) -> dict:
        return {
            "type": "TileMapServiceGG",
            "level": self.level,
            "side_n_tiles": self.side_n_tiles,
            "tile_size_meters": self.tile_size_meters
        }


# TODO Implement
class LatLonGG(GlobalGrid):
    pass
