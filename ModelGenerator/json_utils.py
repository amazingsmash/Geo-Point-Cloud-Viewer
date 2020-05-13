import json

import os


def read_json(filename):
    with open(filename) as data_file:
        data = json.load(data_file)
    return data


def write_json(data, filename):
    folder = os.path.dirname(filename)
    if not os.path.isdir(folder): os.makedirs(folder)

    with open(filename, 'w') as outfile:
        json.dump(data, outfile, indent=4)