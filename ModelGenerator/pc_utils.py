import webbrowser
import numpy as np
from pyproj import CRS, Transformer, exceptions
from laspy.file import File
import os


def get_sector(lat, lon):
    sector = (np.min(lat), np.min(lon), np.max(lat), np.max(lon))
    return sector


def convert_las_to_wgs84(las_x, las_y, epsg_num=32733, show_map=True):
    if epsg_num is 4326:
        return las_x, las_y

    crs_in = CRS.from_epsg(epsg_num)
    crs_4326 = CRS.from_epsg(4326)
    transformer = Transformer.from_crs(crs_from=crs_in, crs_to=crs_4326)

    lat, lon = transformer.transform(las_x, las_y)

    if show_map:
        url = "http://maps.google.com/maps?q=%f,%f" % ((np.min(lat) + np.max(lat)) / 2, (np.min(lon) + np.max(lon)) / 2)
        webbrowser.open(url)

    return lat, lon


def discover_epsg(las_x, las_y):
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
            d = np.linalg.norm(ref_point - new_point)
            dists[i] = d
            print("EPSG %d -> %f" % (i, d))
            if d < 10:
                url = "http://maps.google.com/maps?q=%f,%f" % (lat, lon)
                webbrowser.open(url)

        except exceptions.CRSError:
            pass


def read_las_as_wgs84_xyzc(las_path, epsg_num):
    in_file = File(las_path, mode='r')
    lat, lon = convert_las_to_wgs84(in_file.x, in_file.y, epsg_num, show_map=False)
    h = in_file.z

    sector = get_sector(lat, lon)
    print("LAS WGS84 Sector [%f, %f - %f, %f]" % sector)

    xyzc = np.transpose(np.array([lat, lon, h,
                                  in_file.Classification.astype(float)]))
    in_file.close()
    return xyzc


def split_xyzc_in_wgs84_normalized_cells(xyzc, dgg_cell_size):
    """Returns voxels of points encapuslated in WGS84 zero-centered cells
    all dimensions being normalized between 0 and 1"""
    lon_indices = np.floor(xyzc[:, 0] / dgg_cell_size)
    lat_indices = np.floor(xyzc[:, 1] / dgg_cell_size)

    map_res = (np.ceil(360 / dgg_cell_size).astype(int), np.ceil(180 / dgg_cell_size).astype(int))
    indices2d = np.array([lon_indices, lat_indices]).astype(int)
    indices = np.ravel_multi_index(indices2d, map_res)

    unique_indices = np.unique(indices)

    cells = []
    for i in unique_indices:
        points = np.where(indices == i)[0]
        lon_lat_index = [lon_indices[points[0]], lat_indices[points[0]]]
        cell_min_lon_lat = np.array(lon_lat_index) * dgg_cell_size
        cell_max_lon_lat = cell_min_lon_lat + np.array([dgg_cell_size, dgg_cell_size])
        cell_xyzc = xyzc[points, :]
        min_lon_lat_height = np.min(cell_xyzc[:, 0:3], axis=0)
        max_lon_lat_height = np.max(cell_xyzc[:, 0:3], axis=0)

        cell_xyzc[:, 0:2] = (cell_xyzc[:, 0:2] - cell_min_lon_lat) / dgg_cell_size
        min_h = min_lon_lat_height[2]
        max_h = max_lon_lat_height[2]
        cell_xyzc[:, 2] = (cell_xyzc[:, 2] - min_h) / (max_h - min_h)

        cell = {"cell_index": tuple(lon_lat_index),
                "cell_min_lon_lat": cell_min_lon_lat.tolist(),
                "cell_max_lon_lat": cell_max_lon_lat.tolist(),
                "min_lon_lat_height": min_lon_lat_height.tolist(),
                "max_lon_lat_height": max_lon_lat_height.tolist(),
                "xyzc": cell_xyzc}
        cells += [cell]
    return cells


def split_longest_axis(xyzc):
    min_xyz = np.min(xyzc[:, 0:3], axis=0)
    max_xyz = np.max(xyzc[:, 0:3], axis=0)
    size = max_xyz - min_xyz
    max_dim = np.argmax(size)
    m = np.median(xyzc[:, max_dim])
    division = xyzc[:, max_dim] > m

    s_div = sum(division)
    if s_div == 0 or s_div == xyzc.shape[0]:
        division = xyzc[:, max_dim] >= m

    res = [xyzc[division, :], xyzc[np.logical_not(division), :]]
    return res


def random_subsampling(xyzc, n_selected_points):
    n_points = xyzc.shape[0]
    if n_points < n_selected_points:
        return xyzc, None

    selection = np.random.choice(n_points, size=n_selected_points, replace=False)
    inverse_mask = np.ones(n_points, np.bool)
    inverse_mask[selection] = 0

    selection = xyzc[selection, :]
    non_selected = xyzc[inverse_mask, :]
    return selection, non_selected


def aprox_average_distance(xyz):
    xyz0 = np.random.permutation(xyz)
    xyz1 = np.random.permutation(xyz)
    d = xyz1 - xyz0
    d = d[:, 0] ** 2 + d[:, 1] ** 2 + d[:, 2] ** 2
    d = np.mean(d)
    return np.math.sqrt(d)


def split_octree(xyzc, level):
    """Split point cloud in an regular octree space partitioning with a top node size of 1x1x1"""

    n_level_partitions = int(2 ** level)

    indices = np.clip(np.floor(xyzc[:, 0:3] * n_level_partitions), 0, n_level_partitions-1).astype(int)
    indices = indices[:, 0] + indices[:, 1] * n_level_partitions + indices[:, 0] ** n_level_partitions**2


    children = []
    for i, index in enumerate(np.unique(indices)):
        ps = indices == index
        points = xyzc[ps, :]
        if points.shape[0] > 0:
            children += [xyzc[ps, :]]

    assert len(children) <= 8

    return children


def get_las_paths_from_directory(las_dir):
    file_paths = [os.path.join(las_dir, f) for f in os.listdir(las_dir) if ".las" in f]
    return file_paths


def split_by_class(xyzc):
    cs = np.unique(xyzc[:, 3])
    ls = []
    for c in cs:
        index = xyzc[:, 3] == c
        ls += [xyzc[index, :]]
    return ls


def balanced_subsampling(xyzc, n_selected_points):

    n_points = xyzc.shape[0]
    if n_points < n_selected_points:
        return xyzc, None

    cs, count = np.unique(xyzc[:, 3], return_counts=True)
    max_n_points_class = n_selected_points / cs.shape[0]
    order_classes = np.argsort(count)

    selection = np.array((0,1))
    for i in order_classes:
        indices = np.where(xyzc[:, 3] == cs[i])
        if indices.shape[0] < max_n_points_class:
            selection = np.vstack(selection, indices)
        else:
            s = np.random.choice(indices, size=max_n_points_class, replace=False)
            selection = np.vstack(selection, s)

    selection = xyzc[selection, :]
    inverse_mask = np.ones(n_points, dtype='bool')
    inverse_mask[selection] = False

    non_selected = xyzc[inverse_mask, :]
    return selection, non_selected
