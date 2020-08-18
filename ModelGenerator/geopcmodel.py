import gc
import os
import seaborn as sns
import jsonutils
from globalgrid import GlobalGrid, GlobalGridCell
from pcnode import PCNode
import shutil
from datetime import datetime
from pathlib import Path


class GeoPointCloudModel:

    def __init__(self,
                 name,
                 global_grid: GlobalGrid,
                 parent_directory="",
                 max_node_points=65000,
                 parent_sampling=True,
                 balanced_sampling=True,
                 included_metadata=[]):

        self._model_version = "2.0"
        self._global_grid = global_grid
        self._name = name
        self._parent_directory = parent_directory
        self._max_node_points = max_node_points
        self._parent_sampling = parent_sampling
        self._balanced_sampling = balanced_sampling
        self._point_classes = []
        self._cells = []
        self.generation_file = 0
        self.included_metadata = included_metadata

        shutil.rmtree(self.model_directory(), ignore_errors=True)

    def store_las_files(self, las_paths, epsg_num):
        t0 = datetime.now()

        modelpath = self.model_directory()
        cell_indices = self._global_grid.store_points_in_cell_folders(modelpath,
                                                                      las_paths,
                                                                      epsg_num,
                                                                      self.included_metadata)
        cell_generator = self._global_grid.cell_generator(modelpath, cell_indices)
        for c in cell_generator:
            self._store_cell(c)

        self._save_model_descriptor()

        t1 = datetime.now()
        td = t1 - t0
        print("\nLAS added to model generated in %f sec." % td.total_seconds())

    def model_directory(self): return os.path.join(self._parent_directory, self._name)

    def _store_cell(self, cell: GlobalGridCell):
        folder_name = "Cell_%d_%d" % tuple(cell.xy_index)
        directory = os.path.join(self._parent_directory, self._name, folder_name)
        Path(directory).mkdir(parents=True, exist_ok=True)

        if not os.path.isdir(directory):
            os.makedirs(directory)

        point_classes = list(cell.point_indices_by_class.keys())
        print("%d points. %d classes." % (cell.n_points, len(point_classes)))
        self._point_classes = list(dict.fromkeys(point_classes + self._point_classes))  # add new classes

        index_file_name = "cell.json"
        index_path = os.path.join(directory, index_file_name)

        root_node = PCNode([0], cell.point_indices_by_class, cell)
        PCNode.n_generation_stored_points = 0
        vi = root_node.save_tree(self._parent_directory,
                                 self._max_node_points,
                                 self._balanced_sampling,
                                 out_folder=directory)

        cell_data = cell.get_descriptor()
        cell_data["directory"] = folder_name

        self._cells += [cell_data]
        jsonutils.write_json(vi, index_path)

        gc.collect()  # Forcing garbage collection

    def _save_model_descriptor(self):

        desc_model = {"model_name": self._name,
                      "model_version": self._model_version,
                      "global_grid": self._global_grid.get_descriptor(),
                      "max_node_points": self._max_node_points,
                      "parent_sampling": self._parent_sampling,
                      "cells": self._cells,
                      "classes": GeoPointCloudModel._generate_color_palette(self._point_classes)}

        path = os.path.join(self._parent_directory, self._name, "pc_model.json")
        jsonutils.write_json(desc_model, path)

    @staticmethod
    def _generate_color_palette(point_classes):
        palette = sns.color_palette(None, len(point_classes))
        return [{"class": c, "color": list(palette[i])} for i, c in enumerate(point_classes)]

