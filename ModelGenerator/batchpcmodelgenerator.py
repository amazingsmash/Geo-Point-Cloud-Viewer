import os

import pcutils
from pcmodel import generate_geopointcloud_model

if __name__ == "__main__":
    "Generating several models from different source folders"

    # CORRIDOR_MODEL_XYZI_FTP -o C:/Users/amazi/Desktop/CORRIDOR_MODELS -d X:/ftp/DatosLidarAerolaser/101C_400BLC-MOT2/01_LiDAR/ -e 32630 -s -g 0.1 -i
    base_folder_path = "X:/ftp/DatosLidarAerolaser/"
    lidar_subfolder = "01_LiDAR"
    # out_folder = "C:/Users/amazi/Desktop/CORRIDOR_MODELS"
    out_folder = "C:/Users/amazi/Desktop/repos/project-megaboard/Models/PointClouds"
    epsg_num = 32630

    models = [d for d in os.listdir(base_folder_path) if os.path.isdir(os.path.join(base_folder_path, d))]
    existing_models = [d for d in os.listdir(out_folder) if os.path.isdir(os.path.join(out_folder, d))]

    max_n_models = 1
    models = [m for m in models if m not in existing_models]
    models = models[0:max_n_models]

    for model in models:

        print("Generating model: %s" % model)

        in_folder = os.path.join(base_folder_path, model, lidar_subfolder)
        las_files = pcutils.get_las_paths_from_directory(in_folder)

        generate_geopointcloud_model(model_name=model,
                                     parent_directory=out_folder,
                                     las_files=las_files,
                                     epsg_num=epsg_num)
