import pcutils
import numpy as np


class PCNode:

    def __init__(self, points_by_class_dictionary):
        self.points_by_class = points_by_class_dictionary
        self.n_points_by_class = sum([n.shape[0] for n in self.points_by_class.values()])
        self.n_points = int(np.sum(self.n_points_by_class))

        counts = np.array([n.shape[0] for n in self.points_by_class.values()])
        classes = np.array(list(self.points_by_class.keys()))
        sorted_classes = classes[np.argsort(counts)]
        self.sorted_class_count = {c: self.points_by_class[c].shape[0] for c in sorted_classes}

    def get_all_xyz_points(self):
        t = tuple([self.points_by_class[c] for c in self.sorted_class_count.keys()])
        return np.vstack(t)

    def sample(self, n_selected_points, balanced=True):
        """Returns balanced subsample and remaining points by class"""

        if self.n_points <= n_selected_points:
            return self, None

        sampled = {}
        remaining = {}
        remaining_points = n_selected_points
        remaining_classes = len(self.sorted_class_count)
        for c, n in self.sorted_class_count.items():
            n_taken = remaining_points / remaining_classes if balanced \
                else n_selected_points * (self.points_by_class[c].shape[0] / self.n_points)  # not balanced
            n_taken = int(min(n_taken, n)) if remaining_classes > 1 else remaining_points
            remaining_points -= n_taken
            remaining_classes -= 1

            sampled_points, not_sampled_points = pcutils.random_sampling(self.points_by_class[c], n_taken)
            sampled[c] = sampled_points
            if not_sampled_points is not None:
                remaining[c] = not_sampled_points

        # TODO return sampled point indices not node
        sampled = PCNode(sampled)
        remaining = PCNode(remaining)
        assert sampled.n_points + remaining.n_points == self.n_points and sampled.n_points <= n_selected_points

        return sampled, remaining

    def split_octree(self, level):
        n_level_partitions = int(2 ** level)
        children = [{} for _ in range(8)]

        for c in self.sorted_class_count.keys():
            xyz = self.points_by_class[c]

            xyz01 = (xyz + 1) / 2  # Re-normalizing to 0 - 1
            indices = np.clip(np.floor(xyz01 * n_level_partitions), 0, n_level_partitions - 1).astype(int)
            indices = indices[:, 0] + indices[:, 1] * n_level_partitions + indices[:, 0] ** n_level_partitions ** 2

            for i, index in enumerate(np.unique(indices)):
                ps = indices == index
                points = xyz[ps, :]
                if points.shape[0] > 0:
                    children[i][c] = xyz[ps, :]  # Storing -1 - 1 points

        children = [PCNode(c) for c in children if bool(c)]  # Removing empty's

        assert len(children) <= 8
        return children

    def split_bintree_longest_axis(self):

        children = [{}, {}]

        for c in self.sorted_class_count.keys():
            xyz = self.points_by_class[c]
            min_xyz = np.min(xyz, axis=0)
            max_xyz = np.max(xyz, axis=0)
            size = max_xyz - min_xyz
            max_dim = np.argmax(size)
            m = np.median(xyz[:, max_dim])
            division = xyz[:, max_dim] > m

            s_div = sum(division)
            if s_div == 0 or s_div == xyz.shape[0]:
                division = xyz[:, max_dim] >= m

            children[0][c] = xyz[division, :]
            children[1][c] = xyz[np.logical_not(division), :]

        children = [PCNode(c) for c in children if bool(c)]  # removing empty's

        return children
