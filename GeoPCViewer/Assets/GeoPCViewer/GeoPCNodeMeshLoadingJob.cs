using System;
using System.IO;
using UnityEngine;

public partial class GeoPCNode : MonoBehaviour
{
    class GeoPCNodeMeshLoadingJob : Job
    {
        public Mesh resultMesh = null;
        private readonly GeoPCNode node;

        public GeoPCNodeMeshLoadingJob(GeoPCNode node)
        {
            this.node = node;
        }

        public override void Execute()
        {
            byte[] buffer = File.ReadAllBytes(node.data.pcFile.FullName);
            float[,] matrix = Matrix2D.ReadFromBytes(buffer);
            int nPoints = matrix.GetLength(0);
            var points = new Vector3[nPoints];
            var indices = new int[nPoints];
            var colors = node.data.GetPointColors(node.viewer.classColor);

            for (int i = 0; i < nPoints; i++)
            {
                points[i] = new Vector3(matrix[i, 0], matrix[i, 2], matrix[i, 1]); //XZY
                indices[i] = i;
            }

            resultMesh = new Mesh
            {
                vertices = points,
                colors = colors
            };
            resultMesh.SetIndices(indices, MeshTopology.Points, 0);

            IsDone = true;
        }
    }
}
