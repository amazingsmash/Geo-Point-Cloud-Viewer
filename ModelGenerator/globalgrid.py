import math

import pc_utils
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

        self.points_by_class = pc_utils.divide_points_by_class(cell_xyz_normalized, point_classes)

    def get_descriptor(self) -> dict:
        return {"cell_index": tuple(self.xy_index),
                "cell_extent_min": self.cell_extent_min.tolist(),
                "cell_extent_max": self.cell_extent_max.tolist(),
                "pc_bounds_min": self.pc_bounds_min.tolist(),
                "pc_bounds_max": self.pc_bounds_max.tolist(),
                "points": self.points_by_class}


class TileMapServiceGG(GlobalGrid):
    """Reference: https://wiki.osgeo.org/wiki/Tile_Map_Service_Specification
                    https://www.maptiler.com/google-maps-coordinates-tile-bounds-projection/
    """

    def __init__(self, level: int):
        self.level = level
        self.dimension_n_tiles = (self.level ** 2)
        self.tile_size_meters = 40075016.6784 / self.dimension_n_tiles

    def generate_cells_from_las(self, las_path: str, epsg_num: int) -> list:
        xyzc = pc_utils.read_las_as_spherical_mercator_xyzc(las_path, epsg_num=epsg_num)

        tx = np.floor(xyzc[:, 0] / self.tile_size_meters)
        ty = np.floor(xyzc[:, 1] / self.tile_size_meters)

        indices = tx * self.dimension_n_tiles + ty
        unique_indices, cell_indices = np.unique(indices, return_index=True)

        cell_indices = np.transpose(np.vstack((tx[cell_indices], ty[cell_indices])))
        cell_xy_mins = cell_indices * self.tile_size_meters
        cell_xy_maxs = cell_xy_mins + self.tile_size_meters

        cells = []
        for i, xy_index, cell_xy_min, cell_xy_max in zip(unique_indices, cell_indices, cell_xy_mins, cell_xy_maxs):
            point_indices = np.where(indices == i)[0]
            cell_point_classes = xyzc[point_indices, 3]
            cell_point_xyz = xyzc[point_indices, 0:3]

            cells += [GlobalGridCell(xy_index, cell_point_xyz, cell_point_classes, cell_xy_min, self.tile_size_meters)]

        return cells

    def get_descriptor(self) -> dict:
        return {
            "type": "TileMapServiceGG",
            "level": self.level,
            "dimension_n_tiles": self.dimension_n_tiles,
            "tile_size_meters": self.tile_size_meters
        }


# TODO Implement
class LatLonGG(GlobalGrid):
    pass
