using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;
using System.IO;

public interface IPointCloudManager
{
    Material getMaterialForDistance(float distance);
    Material getMaterialForBoundingBox(Bounds box);
    Color getColorForClass(float classification);
}



class PointCloudLeafNode : PointCloudNode
{
    FileInfo fileInfo = null;
    IPointCloudManager manager = null;
    Renderer meshRenderer = null;
    MeshFilter meshFilter = null;

    MeshLoaderJob job = null;


    private enum MeshState{
        LOADED, NOT_LOADED, LOADING
    }
    private MeshState currentMeshState = MeshState.NOT_LOADED;

    public void init(JSONNode node, DirectoryInfo directory, IPointCloudManager manager)
    {
        gameObject.name = "PointCloudOctreeLeafNode";
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = null;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        this.manager = manager;
        string filename = node["filename"];
        fileInfo = directory.GetFiles(filename)[0];
        Debug.Assert(fileInfo != null, "File not found:" + node["filename"]);
        InitializeFromJSON(node);
    }


    private void initMesh()
    {
        currentMeshState = MeshState.LOADING;
        byte[] buffer = File.ReadAllBytes(fileInfo.FullName);
        Matrix2D m = Matrix2D.readFromBytes(buffer);
        meshFilter.mesh = createMeshFromLASMatrix(m.values);
        currentMeshState = MeshState.LOADED;
    }

    private void startLoadingJob(){
        currentMeshState = MeshState.LOADING;
        job = new MeshLoaderJob(fileInfo, manager);
        job.Start();
    }

    private void checkLoadingJob(){
        if (job.IsDone){
            meshFilter.mesh = job.createMesh();
            job = null;
            currentMeshState = MeshState.LOADED;
        }
    }

    private void removeMesh(){
        meshFilter.mesh = null;
        currentMeshState = MeshState.NOT_LOADED;
    } 

    // Update is called once per frame
    void Update()
    {
        if (State == PCNodeState.VISIBLE)
        {
            if (currentMeshState == MeshState.NOT_LOADED)
            {
                //initMesh();
                startLoadingJob();
            }
            if (currentMeshState == MeshState.LOADING){
                checkLoadingJob();
            }

            Bounds bounds = GetBoundsInWorldSpace();
            meshRenderer.material = manager.getMaterialForBoundingBox(bounds);
        }
        else
        {
            if (currentMeshState == MeshState.LOADED){
                removeMesh();
            }
        }

    }

    private void OnDrawGizmos()
    {
        Bounds b = GetBoundsInWorldSpace();
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(b.center, b.size);
    }

    //-------------

    Mesh createMeshFromLASMatrix(float[,] matrix)
    {
        int nPoints = matrix.GetLength(0);
        Mesh pointCloud = new Mesh();
        Vector3[] points = new Vector3[nPoints];
        int[] indices = new int[nPoints];
        Color[] colors = new Color[nPoints];

        for (int i = 0; i < nPoints; i++)
        {
            points[i] = new Vector3(matrix[i, 0], matrix[i, 1], matrix[i, 2]);
            indices[i] = i;
            float classification = matrix[i, 3];
            colors[i] = manager.getColorForClass(classification);
        }

        pointCloud.vertices = points;
        pointCloud.colors = colors;
        pointCloud.SetIndices(indices, MeshTopology.Points, 0);

        Debug.Log("Loaded Point Cloud Mesh with " + nPoints + " points.");

        return pointCloud;
    }

    public override Bounds GetBoundsInWorldSpace()
    {
        Mesh mesh = meshFilter.mesh;
        if (mesh == null)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }
        return meshRenderer.bounds;
    }

    public override void GetClosestPointOnRay(Ray ray,
                                                Vector2 screenPos,
                                                ref float maxDist,
                                                ref Vector3 closestHit,
                                            float sqrMaxScreenDistance)
    {
        Mesh mesh = meshFilter.mesh;
        if (mesh == null){
            return;
        }
        Bounds meshBounds = meshRenderer.bounds;
        if (meshBounds.Contains(ray.origin) || meshBounds.IntersectRay(ray))
        {

            print("Scanning Point Cloud with " + mesh.vertices.Length + " vertices.");
            foreach (Vector3 p in mesh.vertices)
            {
                Vector3 pWorld = transform.TransformPoint(p);
                Vector3 v = Camera.main.WorldToScreenPoint(pWorld);
                float distancePointToCamera = Mathf.Abs(v.z);
                if (distancePointToCamera < maxDist)
                {
                    float sqrDistance = (new Vector2(v.x, v.y) - screenPos).sqrMagnitude;
                    if (sqrDistance < sqrMaxScreenDistance)
                    {
                        closestHit = pWorld;
                        maxDist = distancePointToCamera;
                    }
                }
            }
        }
    }
}