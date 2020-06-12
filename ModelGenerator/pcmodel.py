import argparse
import sys
from datetime import datetime
import pcutils
from geopcmodel import GeoPointCloudModel
from globalgrid import TileMapServiceGG

if __name__ == "__main__":

    # example: pc_model PC_MODEL_NAME -d path/to/las/folder -o path/to/out -p 32631 -n MAX_POINTS_NODE -s
    # example: pc_model PC_MODEL_NAME -f las1.las las2.las -o path/to/out
    # example: pc_model PC_MODEL_NAME -f las1.las las2.las -d path/to/las/folder -o path/to/out

    parser = argparse.ArgumentParser()
    parser.add_argument("pc_model", help="Creates a hierarchical model of the given name of a multi LAS point cloud "
                                         "designed for efficient out-of-core processing and rendering.")
    parser.add_argument("-d", "--directory", help="Folder with LAS files inside")
    parser.add_argument("-f", "--files", nargs="+", help="Paths to LAS files", default=[])
    parser.add_argument("-o", "--out", help="Path to output folder (default wd)", default="")
    parser.add_argument("-e", "--epsg", help="EPSG reference system number of input data (default 32631)",
                        type=int, default=32631)
    parser.add_argument("-n", "--node_points", help="Max points per node (default 65000)", type=int, default=65000)
    parser.add_argument("-s", "--sample", help="Sample point cloud in parent nodes.",
                        action='store_true')
    parser.add_argument("-g", "--grid_cell_size", help="Divide model in a Rectangular WGS84 Discrete Global Grid with "
                                                       "the given cell side length in degrees. (default 0.1º)",
                        type=float, default=0.1)
    parser.add_argument("-b", "--binary", help="Creates a binary tree, where nodes split by their longest axis,"
                                               "Otherwise, it creates a regular octree where the root node is the size of a cell",
                        action='store_true')
    parser.add_argument("-u", "--unbalanced_sampling", help="Do not sample parent nodes attending to class.",
                        action='store_true')

    args = parser.parse_args()  # getting optionals

    if args.directory is None and args.files is None:
        parser.print_help()
        sys.exit()

    las_files = args.files
    if args.directory is not None:
        las_files += pcutils.get_las_paths_from_directory(args.directory)

    if len(las_files) == 0:
        print("No input LAS found.")
        parser.print_help()
        sys.exit()

    global_grid = TileMapServiceGG(level=12)

    if args.binary:
        method = GeoPointCloudModel.Partitioning.LONGEST_AXIS_BINTREE
    else:
        method = GeoPointCloudModel.Partitioning.REGULAR_OCTREE

    model = GeoPointCloudModel(name=args.pc_model,
                               global_grid=global_grid,
                               parent_directory=args.out,
                               partitioning_method=method,
                               max_node_points=args.node_points,
                               parent_sampling=args.sample,
                               balanced_sampling=not args.unbalanced_sampling)

    t0 = datetime.now()

    model.store_las_files(las_files, args.epsg)

    t1 = datetime.now()
    td = t1 - t0

    print("\nModel generated in %f sec." % td.total_seconds())
