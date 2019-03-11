import json

import os


def readJSON(filename):
    with open(filename) as data_file:
        data = json.load(data_file)
    return data

def writeJSON(data, filename):
    folder = os.path.dirname(filename)
    if not os.path.isdir(folder): os.makedirs(folder)

    with open(filename, 'w') as outfile:
        json.dump(data, outfile, indent=4)