import struct
import numpy as np


def floatArray2Binary(array):
    # ba = bytearray(struct.pack("f", array))
    buf = struct.pack('%sf' % len(array), *array)
    return buf


def matrix_to_file(matrix, file_path):

    values = matrix.flatten().tolist()
    values = list(matrix.shape) + values

    buf = floatArray2Binary(values)
    file = open(file_path, 'wb')
    file.write(buf)
    file.close()

