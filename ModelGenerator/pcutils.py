import webbrowser
import numpy as np
from pyproj import CRS, Transformer, exceptions
from laspy.file import File
import os
from pointattribute import PointAttribute


def get_sector(lat, lon):
    sector = (np.min(lat), np.min(lon), np.max(lat), np.max(lon))
    return sector


def open_sector_in_googlemaps(sector):
    webbrowser.open(get_sector_googlemaps_url(sector))


def get_sector_googlemaps_url(sector):
    return "http://maps.google.com/maps?q=%f,%f" % ((sector[0] + sector[2]) / 2, (sector[1] + sector[3]) / 2)


def convert_las_to_wgs84(las_x, las_y, epsg_num=32733):
    if epsg_num == 4326:
        return las_x, las_y

    crs_in = CRS.from_epsg(epsg_num)
    crs_4326 = CRS.from_epsg(4326)
    transformer = Transformer.from_crs(crs_from=crs_in, crs_to=crs_4326)

    lat, lon = transformer.transform(las_x, las_y)

    return lat, lon


def convert_crs(lasx, lasy, epsg_num_in, epsg_num_out):
    if epsg_num_in is epsg_num_out:
        return lasx, lasy

    crs_in = CRS.from_epsg(epsg_num_in)
    crs_out = CRS.from_epsg(epsg_num_out)
    transformer = Transformer.from_crs(crs_from=crs_in, crs_to=crs_out)

    nx, ny = transformer.transform(lasx, lasy)
    return nx, ny


def show_wgs84_data(x, y, espg_num):
    if espg_num != 4326:
        crs_in = CRS.from_epsg(espg_num)
        t_wgs84 = Transformer.from_crs(crs_from=crs_in, crs_to=CRS.from_epsg(4326))
        lat, lon = t_wgs84.transform(x, y)

    sector = get_sector(lat, lon)
    print("Coordinates in WGS84 Sector [%f, %f - %f, %f]" % sector)
    print("Google Maps: %s" % get_sector_googlemaps_url(sector))


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


def read_las_as_wgs84_lonlathc(las_path, epsg_num):
    in_file = File(las_path, mode='r')
    lat, lon = convert_crs(in_file.x, in_file.y, epsg_num_in=epsg_num, epsg_num_out=4326)
    h = in_file.z

    show_wgs84_data(lat, lon, espg_num=4326)

    lonlathc = np.transpose(np.array([lon, lat, h, in_file.Classification.astype(float)]))
    in_file.close()
    return lonlathc


def read_las_as_spherical_mercator_xyzc(las_path, epsg_num, included_metadata):

    in_file = File(las_path, mode='r')
    x, y = convert_crs(in_file.x, in_file.y, epsg_num_in=epsg_num, epsg_num_out=3857)
    h = in_file.z

    show_wgs84_data(in_file.x, in_file.y, epsg_num)

    xyz_c = np.transpose(np.array([x, y, h, in_file.Classification.astype(float)]))

    metadata_columns = {}
    metadata = None
    if "intensities" in included_metadata:
        intensities = in_file.intensity
        intensities = np.reshape(intensities, (intensities.shape[0],1))
        metadata = intensities
        metadata_columns[0] = "intensities"

    points = np.hstack((xyz_c, metadata)) if metadata is not None else xyz_c

    in_file.close()
    return points, metadata_columns

def read_las_as_spherical_mercator(las_path, epsg_num, point_attributes):

    in_file = File(las_path, mode='r')
    x, y = convert_crs(in_file.x, in_file.y, epsg_num_in=epsg_num, epsg_num_out=3857)
    h = in_file.z

    show_wgs84_data(in_file.x, in_file.y, epsg_num)

    xyz_c = np.transpose(np.array([x, y, h, in_file.Classification.astype(float)]))

    attribute_columns = {}
    attribute_matrix = None
    if PointAttribute.INTENSITY in point_attributes:
        intensities = in_file.intensity
        intensities = np.reshape(intensities, (intensities.shape[0],1))
        attribute_matrix = intensities
        attribute_columns[0] = PointAttribute.INTENSITY
        PointAttribute.adjustRange(PointAttribute.INTENSITY, intensities) # important for renormalization

    points = np.hstack((xyz_c, attribute_matrix)) if attribute_matrix is not None else xyz_c

    in_file.close()
    return points, attribute_columns


def print_spherical_mercator_limits():
    x, y = convert_crs(np.array([-85.05112878, 85.05112878]), np.array([-180, 180]), epsg_num_in=4326, epsg_num_out=3857)
    print("Spherical Mercator Limits: %f, %f - %f, %f" % (x[0], y[0], x[1], y[1]))


def split_xyzc_into_TMS_tile_cells(xyzc, level):
    dimension_n_tiles = (level ** 2)
    tile_size_meters = 156543.0339 / dimension_n_tiles
    tx = np.floor(xyzc[:, 0] / tile_size_meters)
    ty = np.floor(xyzc[:, 1] / tile_size_meters)

    indices = tx * dimension_n_tiles + ty
    unique_indices = np.unique(indices)

    cells = []
    for i in unique_indices:
        points = np.where(indices == i)[0]
        xy_index = [tx[points[0]], ty[points[0]]]
        cell_min_lon_lat = np.array(xy_index) * dgg_cell_size
        cell_max_lon_lat = cell_min_lon_lat + np.array([dgg_cell_size, dgg_cell_size])
        cell_xyzc = xyzc[points, :]
        min_lon_lat_height = np.min(cell_xyzc[:, 0:3], axis=0)
        max_lon_lat_height = np.max(cell_xyzc[:, 0:3], axis=0)

        cell_xyzc[:, 0:2] = (cell_xyzc[:, 0:2] - cell_min_lon_lat) / dgg_cell_size
        min_h = min_lon_lat_height[2]
        max_h = max_lon_lat_height[2]
        cell_xyzc[:, 2] = (cell_xyzc[:, 2] - min_h) / (max_h - min_h)

        cell = {"cell_index": tuple(xy_index),
                "cell_min_lon_lat": cell_min_lon_lat.tolist(),
                "cell_max_lon_lat": cell_max_lon_lat.tolist(),
                "min_lon_lat_height": min_lon_lat_height.tolist(),
                "max_lon_lat_height": max_lon_lat_height.tolist(),
                "points": divide_by_class(cell_xyzc)}
        cells += [cell]
    return cells


    return tx, ty


def split_lonlathc_in_wgs84_normalized_cells(xyzc, dgg_cell_size):
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
                "points": divide_by_class(cell_xyzc)}
        cells += [cell]
    return cells


def divide_by_class(xyzc):
    classes = np.unique(xyzc[:, 3])

    points = {}
    for c in classes:
        xyz = xyzc[c == xyzc[:, 3], 0:3]
        points[float(c)] = xyz

    return points


# def divide_points_by_class(points, classes) -> dict:
#     unique_classes = np.unique(classes)
#
#     points_by_class = {}
#     for c in unique_classes:
#         xyz = points[c == classes, :]
#         points_by_class[float(c)] = xyz
#
#     return points_by_class

def get_indices_by_class(classes) -> dict:
    unique_classes = np.unique(classes)
    points_by_class = {float(c): np.where(c == classes)[0] for c in unique_classes}
    return points_by_class


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


def random_sampling(points, n_selected_points):
    n_points = points.shape[0]
    if n_points <= n_selected_points:
        return points, None

    selection = np.random.choice(n_points, size=n_selected_points, replace=False)
    inverse_mask = np.ones(n_points, np.bool)
    inverse_mask[selection] = 0

    selection = points[selection, :]
    non_selected = points[inverse_mask, :]
    return selection, non_selected


def random_split(vector, n_selected):
    if vector.shape[0] <= n_selected:
        return vector, None
    else:
        selected = np.random.choice(vector.shape[0], size=n_selected, replace=False)
        mask_not_selected = np.ones(vector.shape[0], np.bool)
        mask_not_selected[selected] = 0
        return vector[selected], vector[mask_not_selected]


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

    indices = np.clip(np.floor(xyzc[:, 0:3] * n_level_partitions), 0, n_level_partitions - 1).astype(int)
    indices = indices[:, 0] + indices[:, 1] * n_level_partitions + indices[:, 0] ** n_level_partitions ** 2

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


def balanced_sampling(xyzc, n_selected_points):
    n_points = xyzc.shape[0]
    if n_points < n_selected_points:
        return xyzc, None

    cs, count = np.unique(xyzc[:, 3], return_counts=True)
    max_n_points_class = n_selected_points / cs.shape[0]
    order_classes = np.argsort(count)

    selection = np.array((0, 1))
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


def num_points_by_class(points_by_class):
    n_points = sum([n.shape[0] for n in points_by_class.values()])
    return n_points


def get_all_xyz_points(points_by_class):
    t = tuple([n for n in points_by_class.values()])
    return np.vstack(t)


def get_class_count(points_by_class):
    cc = {}
    for c in points_by_class.items():
        cc[c[0]] = c[1].shape[0]
    return cc


def cell_balanced_sampling(points_by_class, n_selected_points):
    """Returns balanced subsample and remaining points by class"""

    counts = np.array([n.shape[0] for n in points_by_class.values()])
    n_points = np.sum(counts)

    if n_points < n_selected_points:
        return points_by_class, None

    classes = np.array(list(points_by_class.keys()))
    ordered_classes = classes[np.argsort(counts)]

    sampled = {}
    remaining = {}
    remaining_points = n_points
    remaining_classes = ordered_classes.shape[0]
    for c in ordered_classes:
        n_taken = min(remaining_points / remaining_classes,
                      points_by_class[c].shape[0]) if remaining_classes > 1 else remaining_points
        remaining_points -= n_taken
        remaining_classes -= 1
        sampled_points, not_sampled_points = random_sampling(points_by_class[c], n_taken)
        sampled[c] = sampled_points
        if not_sampled_points is not None:
            remaining[c] = not_sampled_points

    assert num_points_by_class(sampled) + num_points_by_class(remaining) == num_points_by_class(points_by_class)

    return sampled, remaining


def test_float_mercator_numerical_precision(las_path, las_epsg):

    def get_point(xs,ys,zs, i):
        return np.array([xs[i], ys[i], zs[i]])

    def dist(points, i, j):
        return np.linalg.norm(points[i,:] - points[j,:])

    in_file = File(las_path, mode='r')


    points_utm = np.transpose(np.array([in_file.x, in_file.y, in_file.z, in_file.Classification.astype(float)]))

    DELTA_LAT = 25
    if DELTA_LAT != 0:
        lat, lon = convert_crs(points_utm[:, 0], points_utm[:, 1], epsg_num_in=las_epsg, epsg_num_out=4326)
        lat += DELTA_LAT
        points_utm = np.transpose(np.array([lat, lon, in_file.z, in_file.Classification.astype(float)]))
        las_epsg = 4326

    print("Distancia UTM: %f m." % dist(points_utm, 100, 200))

    x, y = convert_crs(points_utm[:, 0], points_utm[:, 1], epsg_num_in=las_epsg, epsg_num_out=3857)
    points_mercator = np.transpose(np.array([x, y, in_file.z, in_file.Classification.astype(float)]))
    print("Distancia Mercator: %f m." % dist(points_mercator, 100, 200))

    utm_x, utm_y = convert_crs(x, y, epsg_num_in=3857, epsg_num_out=las_epsg)
    points_utm_reproj = np.transpose(np.array([utm_x, utm_y, in_file.z, in_file.Classification.astype(float)]))
    print("Distancia UTM Double: %f m." % dist(points_utm_reproj, 100, 200))

    error = points_utm - points_utm_reproj
    error = np.linalg.norm(error, axis=1)
    print("Error %d meters." % np.max(error))



def split_octree(xyzc, level):
    """Split point cloud in an regular octree space partitioning with a top node size of 1x1x1"""

    n_level_partitions = int(2 ** level)

    indices = np.clip(np.floor(xyzc[:, 0:3] * n_level_partitions), 0, n_level_partitions - 1).astype(int)
    indices = indices[:, 0] + indices[:, 1] * n_level_partitions + indices[:, 0] ** n_level_partitions ** 2

    children = []
    for i, index in enumerate(np.unique(indices)):
        ps = indices == index
        points = xyzc[ps, :]
        if points.shape[0] > 0:
            children += [xyzc[ps, :]]

    assert len(children) <= 8

    return children


def balanced_sampling(point_classes, n_selected_points) -> map:

    unique_classes, unique_c_inv, unique_c_count = np.unique(point_classes,
                                                             return_counts=True,
                                                             return_inverse=True)

    selected_points_by_class = {}
    n_left_to_take = int(n_selected_points)
    for i in range(unique_classes.shape[0]):
        point_class = unique_classes[i]
        remaining_classes = unique_classes.shape[0] - i
        n_taken = int(min(n_left_to_take / remaining_classes, unique_c_count[i])) if remaining_classes > 1 else n_left_to_take
        n_left_to_take -= n_taken

        class_pos = np.where(unique_c_inv == i)[0]
        sampled_point_classes = np.random.choice(class_pos, n_taken)
        selected_points_by_class[point_class] = sampled_point_classes

    return selected_points_by_class


def generate_balanced_octree__(normalized_xyz, classes, max_node_points=65000) -> (list, list):
    not_taken = np.ones((normalized_xyz.shape[0],), dtype=bool) # all false
    level_linear_indices = []
    level_balanced_node_samplings = []
    level = 0

    xyz01 = normalized_xyz * 0.5 + 0.5  # -1, 1 -> 0, 1
    while np.any(not_taken):
        #Calculating partitioning
        n_level_partitions = int(2 ** level)
        indices = np.clip(np.floor(xyz01[:, 0:3] * n_level_partitions), 0, n_level_partitions - 1).astype(int)
        linear_indices = indices[:, 0] + indices[:, 1] * n_level_partitions + indices[:, 0] * (n_level_partitions ** 2)
        level += 1
        level_linear_indices += [linear_indices]

        #Sampling balanced
        unique_ind, unique_inv_ind = np.unique(linear_indices, return_inverse=True)
        node_sampling = {}
        for i in range(unique_ind.shape[0]):
            node_index = unique_ind[i]
            node_point_indices = np.where(np.logical_and(unique_inv_ind == i, not_taken))[0] # node points
            if node_point_indices.shape[0] > 0:
                selected_points_by_class = balanced_sampling(classes[node_point_indices], max_node_points)

                # remapping to whole set
                for s in selected_points_by_class:
                    s_index = selected_points_by_class[s]
                    s_index = node_point_indices[s_index]
                    selected_points_by_class[s] = s_index
                    not_taken[s_index] = False

                n_selected = sum([s.shape[0] for s in selected_points_by_class.values()])
                assert n_selected <= max_node_points

            node_sampling[node_index] = selected_points_by_class # storing node selection as map

        level_balanced_node_samplings += [node_sampling]

        print(np.sum(not_taken))

    return level_linear_indices, level_balanced_node_samplings


def generate_balanced_octree(normalized_xyz, classes, max_node_points=65000) -> (list, list):
    level_linear_indices = []
    level_balanced_node_samplings = []
    level = 0

    xyz01 = normalized_xyz * 0.5 + 0.5  # -1, 1 -> 0, 1

    while True:
        #Calculating partitioning
        n_level_partitions = int(2 ** level)
        indices = np.clip(np.floor(xyz01[:, 0:3] * n_level_partitions), 0, n_level_partitions - 1).astype(int)
        linear_indices = indices[:, 0] + indices[:, 1] * n_level_partitions + indices[:, 0] * (n_level_partitions ** 2)
        level += 1
        level_linear_indices += [linear_indices]

        # Sampling balanced
        unique_ind, counts = np.unique(linear_indices, return_counts=True)
        if max(counts) <= max_node_points:
            break

    not_taken = np.ones((normalized_xyz.shape[0],), dtype=bool) # all false

    return level_linear_indices, level_balanced_node_samplings




