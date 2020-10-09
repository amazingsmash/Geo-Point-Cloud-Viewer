import argparse
import sys
import pcutils
from geopcmodel import GeoPointCloudModel
from globalgrid import TileMapServiceGG
from pointattribute import PointAttribute


def generate_geopointcloud_model(
        model_name: str,
        parent_directory: str,
        las_files: list,
        epsg_num: int,
        tms_level: int = 15,
        max_node_points: int = 65000,
        parent_sampling: bool = True,
        balanced_sampling: bool = True,
        point_attributes_list: list = [PointAttribute.INTENSITY],
        trace_memory: bool = False,
        profile:bool = False,
):
    global_grid = TileMapServiceGG(level=tms_level)

    model = GeoPointCloudModel(name=model_name,
                               global_grid=global_grid,
                               parent_directory=parent_directory,
                               max_node_points=max_node_points,
                               parent_sampling=parent_sampling,
                               balanced_sampling=balanced_sampling,
                               point_attributes=point_attributes_list)

    if trace_memory:
        import tracemalloc
        tracemalloc.start()
        model.store_las_files(las_files, args.epsg)
        current, peak = tracemalloc.get_traced_memory()
        print(f"Peak Memory Usage was {peak / 10 ** 6}MB")
        tracemalloc.stop()
    else:
        if profile:
            import cProfile
            import pstats
            profile = cProfile.Profile()

            def profile_func():
                model.store_las_files(las_files, epsg_num)

            profile.runcall(profile_func)
            ps = pstats.Stats(profile)
            ps.sort_stats('tottime', 'calls')
            ps.print_stats()
        else:
            model.store_las_files(las_files, epsg_num)

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
    parser.add_argument("-u", "--unbalanced_sampling", help="Do not sample parent nodes attending to class.",
                        action='store_true')
    parser.add_argument("-i", "--add_point_intensity", help="Add point intensity to model.",
                        action='store_true')
    parser.add_argument("-l", "--tms_level", help="Cell sizes match this TMS Level (default 15)",
                        type=int, default=15)

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

    #     model_name: str,
    #     parent_directory: str,
    #     tms_level: int = 15,
    #     max_node_points: int = 65000,
    #     parent_sampling: bool = True,
    #     balanced_sampling: bool = True,
    #     point_attributes_list: list = [PointAttribute.INTENSITY],
    #     trace_memory: bool = False,
    #     profile: bool = False,
    # ):

    generate_geopointcloud_model(model_name=args.pc_model,
                                 parent_directory=args.out,
                                 las_files=las_files,
                                 epsg_num=args.epsg,
                                 tms_level=args.tms_level,
                                 max_node_points=args.node_points,
                                 parent_sampling=args.sample,
                                 balanced_sampling=not args.unbalanced_sampling,
                                 point_attributes_list=[PointAttribute.INTENSITY] if args.add_point_intensity else [],
                                 trace_memory=False,
                                 profile=False)


    # global_grid = TileMapServiceGG(level=15)
    #
    # model = GeoPointCloudModel(name=args.pc_model,
    #                            global_grid=global_grid,
    #                            parent_directory=args.out,
    #                            max_node_points=args.node_points,
    #                            parent_sampling=args.sample,
    #                            balanced_sampling=not args.unbalanced_sampling,
    #                            point_attributes=[PointAttribute.INTENSITY] if args.add_point_intensity else [])
    #
    # trace_memory = False
    # profile = False
    #
    # if trace_memory:
    #     import tracemalloc
    #     tracemalloc.start()
    #     model.store_las_files(las_files, args.epsg)
    #     current, peak = tracemalloc.get_traced_memory()
    #     print(f"Peak Memory Usage was {peak / 10 ** 6}MB")
    #     tracemalloc.stop()
    # else:
    #     if profile:
    #         import cProfile
    #         import pstats
    #         profile = cProfile.Profile()
    #
    #         def profile_func():
    #             model.store_las_files(las_files, args.epsg)
    #
    #         profile.runcall(profile_func)
    #         ps = pstats.Stats(profile)
    #         ps.sort_stats('tottime', 'calls')
    #         ps.print_stats()
    #     else:
    #         model.store_las_files(las_files, args.epsg)
