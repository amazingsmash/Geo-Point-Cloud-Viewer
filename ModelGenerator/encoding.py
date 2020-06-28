import struct
import numpy as np
import os
from pathlib import Path


def floats_to_binary(array):
    # ba = bytearray(struct.pack("f", array))
    buf = struct.pack('%sf' % len(array), *array)
    assert len(buf) == len(array) * 4
    return buf


def doubles_to_binary(array):
    buf = struct.pack('%sd' % len(array), *array)
    assert len(buf) == len(array) * 8
    return buf


def matrix_to_file(matrix, file_path):

    folder = os.path.dirname(file_path)
    Path(folder).mkdir(parents=True, exist_ok=True)

    values = matrix.flatten().tolist()
    values = list(matrix.shape) + values

    buf = floats_to_binary(values)
    file = open(file_path, 'wb')
    file.write(buf)
    file.close()


def file_to_matrix(file_path):
    f = open(file_path, 'rb')
    raw = f.read()
    n_floats = int(len(raw) / 4)
    buf = struct.unpack('f'*n_floats, raw)

    mat = np.reshape(buf[2:], newshape=(int(buf[0]), int(buf[1])))

    return mat


def append_rows_to_file(path, rows):
    if os.path.exists(path):
        ps = file_to_matrix(path)
        rows = np.vstack((ps, rows))
        os.remove(path)
    matrix_to_file(rows, path)

################


def matrix_to_file_double(matrix, file_path):
    folder = os.path.dirname(file_path)
    Path(folder).mkdir(parents=True, exist_ok=True)

    values = matrix.flatten().tolist()
    values = list(matrix.shape) + values

    buf = doubles_to_binary(values)
    file = open(file_path, 'wb')
    file.write(buf)
    file.close()


def file_to_matrix_double(file_path):
    f = open(file_path, 'rb')
    raw = f.read()
    n_doubles = int(len(raw) / 8)
    buf = struct.unpack('d'*n_doubles, raw)

    assert len(buf) == n_doubles

    mat = np.reshape(buf[2:], newshape=(int(buf[0]), int(buf[1]))).astype(np.double)

    return mat


def append_rows_to_file_double(path, rows):
    if os.path.exists(path):
        ps = file_to_matrix_double(path)
        rows = np.vstack((ps, rows))
        os.remove(path)
    matrix_to_file_double(rows, path)


if __name__ == "__main__":
    mat = np.zeros((4, 20))
    mat[0, 0] = 42
    mat[0, 1] = 84
    mat[1, 0] = 35
    mat[1, 1] = 23

    file_path = "temp.bin"
    mat = mat.astype(np.float)
    matrix_to_file(mat, file_path)
    x = file_to_matrix(file_path)

    assert np.array_equal(mat, x)

    mat = mat.astype(np.double)
    matrix_to_file_double(mat, file_path)
    x = file_to_matrix_double(file_path)

    os.remove(file_path)

    assert np.array_equal(mat, x)
    print("Matrix encoding / decoding works")