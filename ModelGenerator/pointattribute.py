import numpy as np
from enum import Enum

#Attributes store their ranges in this var
_registeredPointAttributeRanges = {}

class PointAttribute(Enum):
    INTENSITY = "Intensity"

    @staticmethod
    def adjustRange(attribute, newValues: np.ndarray):
        vmin = np.min(newValues)
        vmax = np.max(newValues)

        if attribute in _registeredPointAttributeRanges:
            old_min, old_max = _registeredPointAttributeRanges[attribute]
            vmin = min([vmin, old_min])
            vmax = max([vmax, old_max])

        _registeredPointAttributeRanges[attribute] = (float(vmin), float(vmax))

    @staticmethod
    def normalizeValues(attribute, values: np.ndarray):
        """values moved to 0,1 range for rendering"""
        vmin, vmax = _registeredPointAttributeRanges[attribute]
        nvalues = (values - vmin) / (vmax - vmin)
        return nvalues

    @staticmethod
    def get_registered_attributes_descriptor():
        return {k.value: list(r) for k, r in _registeredPointAttributeRanges.items() }