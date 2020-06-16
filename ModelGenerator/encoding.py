import struct
import numpy as np
import os


def floatArray2Binary(array):
    # ba = bytearray(struct.pack("f", array))
    buf = struct.pack('%sf' % len(array), *array)
    assert len(buf) == len(array) * 4
    return buf


def matrix_to_file(matrix, file_path):

    values = matrix.flatten().tolist()
    values = list(matrix.shape) + values

    buf = floatArray2Binary(values)
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


if __name__ == "__main__":
    mat = np.zeros((4, 20))
    mat[0, 0] = 42
    mat[0, 1] = 84
    mat[1, 0] = 35
    mat[1, 1] = 23

    file_path = "temp.bin"
    matrix_to_file(mat, file_path)

    x = file_to_matrix(file_path)
    os.remove(file_path)

    assert np.array_equal(mat, x)
    print("Matrix encoding / decoding works")