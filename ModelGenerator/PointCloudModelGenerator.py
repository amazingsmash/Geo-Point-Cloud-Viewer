from laspy.file import File
import numpy as np

class PointCloudModel:

    def __init__(self, name, lasFilePaths):

        self.lasFilePaths = lasFilePaths
        self.name = name

    def addLASFilePath(self, path):
        self.lasFilePaths += [path]


    def generate(self, outPath=""):

        for file in self.lasFilePaths:

            inFile = File(file, mode='r')


if __name__ == "__main__":

    model = PointCloudModel("Model", ["000018.las"])

    model.generate();

    print("DONE")