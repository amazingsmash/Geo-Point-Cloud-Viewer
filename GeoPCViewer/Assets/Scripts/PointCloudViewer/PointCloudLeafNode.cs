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
    MeshRenderer meshRenderer = null;
    MeshFilter meshFilter = null;
    private MeshState currentMeshState = MeshState.NOT_LOADED;

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
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = null;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;

        this.pointCloudManager = manager;
        string filename = node["filename"];
        fileInfo = directory.GetFiles(filename)[0];
        Debug.Assert(this.fileInfo != null, "File not found:" + node["filename"]);
        Debug.Assert(this.pointCloudManager != null, "No PCManager");
        InitializeFromJSON(node);

        return true;
    }

    void FetchMesh()
    {
        float dist = boundingSphere.DistanceTo(Camera.main.transform.position);
        float priority = Camera.main.farClipPlane - dist;

        Mesh mesh = pointCloudManager.GetMeshManager().CreateMesh(fileInfo, pointCloudManager.GetColorForClass, priority);
        if (mesh != null)
        {
            meshFilter.mesh = mesh;
            currentMeshState = MeshState.LOADED;
        }
    }

    private void RemoveMesh()
    {
        pointCloudManager.GetMeshManager().ReleaseMesh(meshFilter.mesh); //Returning Mesh
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
            pointCloudManager.ModifyRendererBasedOnBounds(boundsInModelSpace, meshRenderer);
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

        if (meshRenderer.materials.Length > 1)
        {
            Gizmos.color = Color.green;
        }

        Bounds b = boundsInModelSpace;
        Gizmos.DrawWireCube(b.center, b.size);

        //Gizmos.DrawWireSphere(boundingSphere.position, boundingSphere.radius);
    }

    public override void ComputeNodeState(ref List<PointCloudLeafNode.NodeAndDistance> visibleLeafNodesAndDistances,
                                        Vector3 camPosition, 
                                        float zFar)
    {
        float dist = EstimatedDistance(camPosition);
        if (dist <= zFar)
        {
            NodeAndDistance nodeAndDistance = new NodeAndDistance();
            nodeAndDistance.node = this;
            nodeAndDistance.estimatedDistanceToCamera = dist;
            visibleLeafNodesAndDistances.Add(nodeAndDistance);

            //Material
            //meshRenderer.materials = pointCloudManager.GetMaterialsForDistance(dist, boundsInModelSpace.MaxDistance(camPosition));
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
                                                    ref Color colorClosestHit,
                                                float sqrMaxScreenDistance)
        {
            if (State == PCNodeState.INVISIBLE || currentMeshState != MeshState.LOADED)
            {
                return;
            }

            Mesh mesh = meshFilter.mesh;
            if (mesh == null)
            {
                return;
            }
            Bounds meshBounds = boundsInModelSpace;
            if (meshBounds.Contains(ray.origin) || meshBounds.IntersectRay(ray))
            {
                //print("Scanning Point Cloud with " + mesh.vertices.Length + " vertices.");
                int i = 0;
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
                            colorClosestHit = mesh.colors[i];
                            maxDist = distancePointToCamera;
                        }
                    }
                    i++;
                }
            }
        }
}