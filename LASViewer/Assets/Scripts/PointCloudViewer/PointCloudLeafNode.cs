using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;
using System.IO;
using UnityEditor;

class PointCloudLeafNode : PointCloudNode
{
    FileInfo fileInfo = null;
    IPointCloudManager pointCloudManager = null;
    private MeshState currentMeshState = MeshState.NOT_LOADED;

    GameObject hdChild = null;
    GameObject ldChild = null;

    public LODGroup LoDGroup;

    private enum MeshState
    {
        LOADED, NOT_LOADED
    }

    public struct NodeAndDistance : System.IComparable<NodeAndDistance>
    {
        public PointCloudLeafNode node;
        public float estimatedDistanceToCamera;

        public int CompareTo(NodeAndDistance other)
        {
            return other.estimatedDistanceToCamera.CompareTo(estimatedDistanceToCamera);
        }
    }

    public override bool Initialize(JSONNode node, DirectoryInfo directory, IPointCloudManager manager)
    {
        gameObject.name = "PointCloudOctreeLeafNode";
        this.pointCloudManager = manager;
        string filename = node["filename"];
        fileInfo = directory.GetFiles(filename)[0];
        Debug.Assert(this.fileInfo != null, "File not found:" + node["filename"]);
        Debug.Assert(this.pointCloudManager != null, "No PCManager");
        InitializeFromJSON(node);

        //Creating LODS
        LoDGroup = gameObject.AddComponent<LODGroup>();
        LoDGroup.animateCrossFading = false;
        LODGroup.crossFadeAnimationDuration = 6.0f;
        LoDGroup.fadeMode = LODFadeMode.CrossFade;

        LOD[] lods = new LOD[2];
        lods[0] = CreateLoD("HD Version",
                            manager.HDHorizontalRelativeScreenSize,
                            manager.HDMaterial,
                            out hdChild);
        lods[1] = CreateLoD("LD Version", 
                            0.0f, 
                            manager.LDMaterial, 
                            out ldChild);
        LoDGroup.SetLODs(lods);

        return true;
    }

    LOD CreateLoD(string childName, float screenHeight, Material material, out GameObject child)
    {
        child = new GameObject(childName);
        child.isStatic = true;
        child.transform.SetParent(gameObject.transform, false);
        MeshFilter meshFilter = child.AddComponent<MeshFilter>();
        meshFilter.mesh = null;
        MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
        meshRenderer.material = material;

        LOD lod = new LOD(screenHeight, new Renderer[] { meshRenderer });
        return lod;
    }

    void FetchMesh()
    {
        float dist = boundingSphere.DistanceTo(Camera.main.transform.position);
        float priority = Camera.main.farClipPlane - dist;

        Mesh mesh = pointCloudManager.GetMeshManager().CreateMesh(fileInfo, pointCloudManager, priority);
        if (mesh != null)
        {

            hdChild.GetComponent<MeshFilter>().mesh = mesh;
            ldChild.GetComponent<MeshFilter>().mesh = mesh;
            LoDGroup.RecalculateBounds();

            LoDGroup.enabled = true;
            hdChild.SetActive(true);
            ldChild.SetActive(true);

            currentMeshState = MeshState.LOADED;
        }
    }

    private void RemoveMesh()
    {
        LoDGroup.enabled = false;
        hdChild.SetActive(false);
        ldChild.SetActive(false);

        pointCloudManager.GetMeshManager().ReleaseMesh(hdChild.GetComponent<MeshFilter>().mesh); //Returning Mesh
        hdChild.GetComponent<MeshFilter>().mesh = null;
        ldChild.GetComponent<MeshFilter>().mesh = null;
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
        }
        else
        {
            if (currentMeshState == MeshState.LOADED)
            {
                RemoveMesh();
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = (State == PCNodeState.VISIBLE) ? Color.red : Color.blue;

        if (Selection.Contains(gameObject) || Selection.Contains(hdChild) || Selection.Contains(ldChild))
        {
            Gizmos.color = Color.green;
        }

        Bounds b = boundsInModelSpace;// GetBoundsInWorldSpace();
        Gizmos.DrawWireCube(b.center, b.size);

        //Gizmos.DrawWireSphere(boundingSphere.position, boundingSphere.radius);
    }

    public override void ComputeNodeState(ref List<PointCloudLeafNode.NodeAndDistance> visibleLeafNodesAndDistances, Vector3 camPosition, float zFar)
    {
        float dist = EstimatedDistance(camPosition);
        if (dist <= zFar)
        {
            NodeAndDistance nodeAndDistance = new NodeAndDistance();
            nodeAndDistance.node = this;
            nodeAndDistance.estimatedDistanceToCamera = dist;
            visibleLeafNodesAndDistances.Add(nodeAndDistance);

            //int activeLoD = LoDGroup.ActiveLoD();
            //if (activeLoD > -1)
            //{
            //    Debug.Log("LoD " + LoDGroup.ActiveLoD());
            //}
        }
    }


    public override void OnStateChanged()
    {
        //switch (State)
        //{
        //    case PCNodeState.INVISIBLE:
        //        Debug.Log("Leaf Node set invisible.");
        //        break;
        //    case PCNodeState.VISIBLE:
        //        Debug.Log("Leaf Node set invisible.");
        //        break;
        //}
    }

    public override void GetClosestPointOnRay(Ray ray,
                                                Vector2 screenPos,
                                                ref float maxDist,
                                                ref Vector3 closestHit,
                                            float sqrMaxScreenDistance)
    {
        if (State == PCNodeState.INVISIBLE || currentMeshState != MeshState.LOADED)
        {
            return;
        }

        Mesh mesh = hdChild.GetComponent<MeshFilter>().mesh;
        if (mesh == null)
        {
            return;
        }
        Bounds meshBounds = boundsInModelSpace;
        if (meshBounds.Contains(ray.origin) || meshBounds.IntersectRay(ray))
        {
            //print("Scanning Point Cloud with " + mesh.vertices.Length + " vertices.");
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