using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;

public class MeshLoaderJob
{

    private System.Threading.Thread m_Thread = null;

    private Vector3[] points = null;
    private int[] indices = null;
    private Color[] colors = null;
    public bool IsDone { get; private set;}


    private readonly FileInfo fileInfo;
    private readonly IPointCloudManager manager;
    public MeshLoaderJob(FileInfo fileInfo, IPointCloudManager manager){
        this.fileInfo = fileInfo;
        this.manager = manager;
        IsDone = false;
    }

    public virtual void Start()
    {
        m_Thread = new System.Threading.Thread(Run);
        m_Thread.Start();
    }

    private void Run()
    {
        byte[] buffer = File.ReadAllBytes(fileInfo.FullName);
        Matrix2D m = Matrix2D.readFromBytes(buffer);
        CreateMeshFromLASMatrix(m.values);
    }

    public Mesh createMesh(){
        Mesh pointCloud = new Mesh();
        pointCloud.vertices = points;
        pointCloud.colors = colors;
        pointCloud.SetIndices(indices, MeshTopology.Points, 0);

        Debug.Log("Loaded Point Cloud Mesh with " + points.Length + " points.");

        return pointCloud;
    }

    private void CreateMeshFromLASMatrix(float[,] matrix)
    {
        int nPoints = matrix.GetLength(0);
        points = new Vector3[nPoints];
        indices = new int[nPoints];
        colors = new Color[nPoints];

        for (int i = 0; i < nPoints; i++)
        {
            points[i] = new Vector3(matrix[i, 0], matrix[i, 1], matrix[i, 2]);
            indices[i] = i;
            float classification = matrix[i, 3];
            colors[i] = manager.getColorForClass(classification);
        }
        IsDone = true;
    }

}
