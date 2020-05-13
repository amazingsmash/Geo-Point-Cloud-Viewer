# Unity Geo Point Cloud Viewer

This project is an implementation of a novel technique for massive point cloud rendering on the Unity game engine. 
It consists in two parts:

## Model Generator

This Python program contains a preprocessing stage that structures the point cloud on a hierarchical structure designed for efficient 
out-of-core rendering.

The usage is defined as:

pc_model PC_MODEL_NAME [-h] [-d D] [-f F [F ...]] [-o O] [-e E] [-n N]
                              [-s S]

optional arguments:
  -h, --help    show this help message and exit
  -d D          Folder with LAS files inside
  -f F [F ...]  Paths to LAS files
  -o O          Path to output folder (default wd)
  -e E          EPSG num (default 32631)
  -n N          Max points per node (default 65000)
  -s S          Subsample PC in parent nodes (default True)
  
For example:

pc_model PC_MODEL_NAME -f las1.las las2.las -d path/to/las/folder -o path/to/out -p 32631 -n MAX_POINTS_NODE -s True 
