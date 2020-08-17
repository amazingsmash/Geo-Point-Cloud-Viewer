import sys
import encoding
import pcutils
import numpy as np
from globalgrid import GlobalGridCell
import os


class PCNode:

    n_generation_stored_points = 0

    def __init__(self, indices, point_indices_by_class, cell: GlobalGridCell):
        """point_indices_by_class is a dict of classes -> point indices.
        cell_points is a reference to a matrix with all the points with the form XYZCPPPPP..."""
        self._indices = indices
        self._cell = cell
        self.point_indices_by_class = point_indices_by_class
        self.n_points_by_class = sum([n.shape[0] for n in self.point_indices_by_class.values()])
        self.n_points = int(np.sum(self.n_points_by_class))

        counts = np.array([n.shape[0] for n in self.point_indices_by_class.values()])
        classes = np.array(list(self.point_indices_by_class.keys()))
        sorted_classes = classes[np.argsort(counts)]
        self.sorted_class_count = {c: self.point_indices_by_class[c].shape[0] for c in sorted_classes}


    def _get_point_indices_intra_class_shuffled(self) -> np.ndarray:
        t = tuple([self.point_indices_by_class[c] for c in self.sorted_class_count.keys()])
        for m in t:
            np.random.shuffle(m)
        return np.hstack(t)

    def get_normalized_xyz_intra_class_shuffled(self) -> np.ndarray:
        indices = self._get_point_indices_intra_class_shuffled()
        return self._cell.cell_points_normalized[indices]

    def get_extent(self):
        indices = np.hstack([self.point_indices_by_class[c] for c in self.sorted_class_count.keys()])
        node_points = self._cell.cell_points_normalized[indices]
        min_xyz = np.min(node_points, axis=0)
        max_xyz = np.max(node_points, axis=0)
        return min_xyz, max_xyz

    def balanced_sampling(self, n_selected_points, balanced=True):
        """Returns balanced subsample and remaining points by class"""

        if self.n_points <= n_selected_points:
            return self, None

        sampled = {}
        remaining = {}
        remaining_points = n_selected_points
        remaining_classes = len(self.sorted_class_count)
        for c, n in self.sorted_class_count.items():
            n_taken = remaining_points / remaining_classes if balanced \
                else n_selected_points * (self.point_indices_by_class[c].shape[0] / self.n_points)  # not balanced
            n_taken = int(min(n_taken, n)) if remaining_classes > 1 else remaining_points
            remaining_points -= n_taken
            remaining_classes -= 1

            sampled[c], class_remaining = pcutils.random_split(self.point_indices_by_class[c], n_taken)
            if class_remaining is not None:
                remaining[c] = class_remaining

        sampled = PCNode(self._indices, sampled, self._cell)
        remaining = PCNode(self._indices, remaining, self._cell) if len(remaining) > 0 else None

        rP = remaining.n_points if remaining is not None else 0
        assert sampled.n_points + rP == self.n_points and sampled.n_points <= n_selected_points

        return sampled, remaining

    def split_octree(self):
        children = [{} for _ in range(8)]

        level = len(self._indices) - 1
        node_indices = self._cell.get_octree_node_indices(level)

        for c in self.sorted_class_count.keys():

            point_indices = self.point_indices_by_class[c]

            class_node_indices = node_indices[point_indices]
            unique_class_node_indices = np.unique(class_node_indices)

            assert len(unique_class_node_indices) <= 8

            for i, index in enumerate(unique_class_node_indices):
                ps = np.where(class_node_indices == index)[0]
                ps = point_indices[ps]

                if ps.shape[0] > 0:
                    children[i][c] = ps  # Storing indices

        children = [PCNode(self._indices + [i], c, self._cell) for i, c in enumerate(children) if bool(c)]  # Removing empty

        assert len(children) <= 8
        return children

    def save_tree(self, parent_sampling: bool, max_node_points: int, balanced_sampling: bool, out_folder: str):
        if self.n_points == 0:
            return

        if parent_sampling or self.n_points < max_node_points:
            min_xyz, max_xyz = self.get_extent()

            sampled_node, remaining_node = self.balanced_sampling(max_node_points, balanced=balanced_sampling)

            file_name, file_path = self._get_file_path(out_folder)
            selected_xyz_normalized = sampled_node.get_normalized_xyz_intra_class_shuffled()
            encoding.matrix_to_file(selected_xyz_normalized, file_path)

            PCNode.n_generation_stored_points += sampled_node.n_points
            self._print_generation_state()

            voxel_index = {"min": min_xyz.tolist(),
                           "max": max_xyz.tolist(),
                           "indices": self._indices,
                           "filename": file_name,
                           "n_node_points": sampled_node.n_points,
                           "n_subtree_points": self.n_points,
                           "avg_distance": pcutils.aprox_average_distance(selected_xyz_normalized),
                           "sorted_class_count": sampled_node.sorted_class_count}
        else:
            remaining_node = self

        # Creating children
        voxel_index["children"] = []
        if remaining_node is not None:
            nodes = remaining_node.split_octree()
            for i, node in enumerate(nodes):
                vi = node.save_tree(parent_sampling,
                                      max_node_points,
                                      balanced_sampling,
                                      out_folder)
                voxel_index["children"] += [vi]

        return voxel_index

    def _get_file_path(self, out_folder):
        file_name = "Node-" + "_".join([str(n) for n in self._indices]) + ".bytes"
        file_path = os.path.join(out_folder, file_name)
        return file_name, file_path

    def _print_generation_state(self):
        msg = "Processed %f%%." % (PCNode.n_generation_stored_points / self._cell.n_points * 100)
        sys.stdout.write('\r' + msg)
        sys.stdout.flush()
