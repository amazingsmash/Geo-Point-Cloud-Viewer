import webbrowser

import numpy as np
from pyproj import CRS, Transformer


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
        url = "http://maps.google.com/maps?q=%f,%f" % ((np.min(lat) + np.max(lat))/2, (np.min(lon) + np.max(lon))/2)
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
            d = np.linalg.norm(ref_point-new_point)
            dists[i] = d
            print("EPSG %d -> %f" % (i, d))
            if d < 10:
                url = "http://maps.google.com/maps?q=%f,%f" % (lat, lon)
                webbrowser.open(url)

        except exceptions.CRSError:
            pass