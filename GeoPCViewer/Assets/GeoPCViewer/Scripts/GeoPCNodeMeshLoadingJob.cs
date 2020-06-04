using System;
using System.IO;
using UnityEngine;

public partial class GeoPCNode : MonoBehaviour
{
    class GeoPCNodeMeshLoadingJob : Job
    {
        private readonly GeoPCNode node;
        public Vector3[] points;
        public int[] indices;
        public Color[] colors;

        public GeoPCNodeMeshLoadingJob(GeoPCNode node)
        {
            this.node = node;
        }

        public override void Execute()
        {
            byte[] buffer = File.ReadAllBytes(node.data.pcFile.FullName);
            float[,] matrix = Matrix2D.ReadFromBytes(buffer);
            int nPoints = matrix.GetLength(0);
            points = new Vector3[nPoints];
            indices = new int[nPoints];
            colors = node.data.GetPointColors(node.viewer.classColor);

            for (int i = 0; i < nPoints; i++)
            {
                points[i] = new Vector3(matrix[i, 0], matrix[i, 2], matrix[i, 1]); //XZY
                indices[i] = i;
            }

            IsDone = true;
        }

        public Mesh GenerateMesh()
        {
            var resultMesh = new Mesh
            {
                vertices = points,
                colors = colors
            };
            resultMesh.SetIndices(indices, MeshTopology.Points, 0);
            return resultMesh;
        }
    }
}
