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
    IPointCloudManager pointCloudManager = null;
    Renderer meshRenderer = null;
    MeshFilter meshFilter = null;

    private enum MeshState{
        LOADED, NOT_LOADED
    }
    private MeshState currentMeshState = MeshState.NOT_LOADED;

    public void Initialize(JSONNode node, DirectoryInfo directory, IPointCloudManager manager)
    {
        gameObject.name = "PointCloudOctreeLeafNode";
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = null;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        this.pointCloudManager = manager;
        string filename = node["filename"];
        fileInfo = directory.GetFiles(filename)[0];
        Debug.Assert(this.fileInfo != null, "File not found:" + node["filename"]);
        Debug.Assert(this.pointCloudManager != null, "No PCManager");
        InitializeFromJSON(node);
    }

    void FetchMesh(){
        float dist = boundingSphere.DistanceTo(Camera.main.transform.position);
        float priority = Camera.main.farClipPlane - dist;

        Mesh mesh = MeshManager.CreateMesh(fileInfo, pointCloudManager, priority);
        if (mesh != null)
        {
            meshFilter.mesh = mesh;
            currentMeshState = MeshState.LOADED;
        }
    }

    private void RemoveMesh(){
        MeshManager.ReleaseMesh(meshFilter.mesh); //Returning Mesh
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
                FetchMesh();
            }
            Bounds bounds = GetBoundsInWorldSpace();
            meshRenderer.material = pointCloudManager.getMaterialForBoundingBox(bounds);
        }
        else
        {
            if (currentMeshState == MeshState.LOADED){
                RemoveMesh();
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = (State == PCNodeState.VISIBLE)? Color.red : Color.blue;
        //Bounds b = GetBoundsInWorldSpace();
        //Gizmos.DrawWireCube(b.center, b.size);

        Gizmos.DrawWireSphere(boundingSphere.position, boundingSphere.radius);
    }

    //-------------


    public override Bounds GetBoundsInWorldSpace()
    {
        if (currentMeshState == MeshState.NOT_LOADED)
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