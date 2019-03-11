import struct
import numpy as np

def floatArray2Binary(array):
    # ba = bytearray(struct.pack("f", array))
    buf = struct.pack('%sf' % len(array), *array)
    return buf

def saveMatrixToFile(matrix, filePath):

    values = np.transpose(matrix).flatten().tolist()
    values = list(matrix.shape) + values

    buf = floatArray2Binary(values)
    file = open(filePath, 'wb')
    file.write(buf)
    file.close()

