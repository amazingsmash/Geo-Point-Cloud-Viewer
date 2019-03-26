using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;
using System.IO;
using UnityEditor;

class PCNode: MonoBehaviour, System.IComparable<PCNode>
{
    #region subtypes

    private enum MeshState
    {
        LOADED, NOT_LOADED
    }

    public enum PCNodeState
    {
        VISIBLE,
        INVISIBLE
    };

    public enum RenderType
    {
        FDM, NDM, BOTH
    };

    #endregion

    #region Attributes

    FileInfo fileInfo = null;
    IPointCloudManager pcManager = null;
    MeshRenderer meshRenderer = null;
    MeshFilter meshFilter = null;
    private MeshState currentMeshState = MeshState.NOT_LOADED;
    float averagePointDistance = 0.0f;

    protected BoundingSphere boundingSphere;
    public Bounds boundsInModelSpace { get; private set; }
    protected PCNode[] children = null;

    public PCNodeState State = PCNodeState.INVISIBLE;


    private float meshLoadingDistance
    {
        get { return averagePointDistance * 4.0f; }
    }

    float minDistanceToCam = 0.0f;
    float maxDistanceToCam = 0.0f;
    float lodTestResult = 0.0f;
    RenderType renderType = RenderType.FDM;
    float meshLoadingTimeSec = 0;
    private float secSinceMeshCreated { get { return Time.time - meshLoadingTimeSec; } }

    #endregion

    #region JSON Initialization

    public void InitializeFromJSON(JSONNode node)
    {
        Vector3 min = JSONNode2Vector3(node["min"]);
        Vector3 max = JSONNode2Vector3(node["max"]);
        Vector3 center = (min + max) / 2.0f;
        Vector3 size = max - min;
        boundingSphere = new BoundingSphere(center, size.magnitude);

        averagePointDistance = node["avgDistance"];

        boundsInModelSpace = new Bounds(center, size);
    }

    public static Vector3 JSONNode2Vector3(JSONNode node)
    {
        return new Vector3(node[0].AsFloat, node[2].AsFloat, node[1].AsFloat);
    }

    public static PCNode AddNode(JSONNode node, DirectoryInfo directory, GameObject gameObject, IPointCloudManager materialProvider)
    {
        GameObject child = new GameObject("PC Node");
        child.isStatic = true;

        PCNode pcNode = child.AddComponent<PCNode>();
        pcNode.Initialize(node, directory, materialProvider);
        child.transform.SetParent(gameObject.transform, false);
        return pcNode;
    }

    public bool Initialize(JSONNode node, DirectoryInfo directory, IPointCloudManager manager)
    {
        gameObject.name = "PCNode";
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = null;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;

        this.pcManager = manager;
        string filename = node["filename"];
        fileInfo = directory.GetFiles(filename)[0];
        Debug.Assert(this.fileInfo != null, "File not found:" + node["filename"]);
        Debug.Assert(this.pcManager != null, "No PCManager");

        gameObject.name = "PC Parent Node";

        JSONArray childrenJSON = node["children"].AsArray;
        ArrayList childrenList = new ArrayList();
        //Debug.Log("N Children: " + childrenJSON.Count);
        for (int i = 0; i < childrenJSON.Count; i++)
        {
            PCNode pcNode = PCNode.AddNode(childrenJSON[i], directory, gameObject, manager);
            if (pcNode != null)
            {
                childrenList.Add(pcNode);
            }
        }
        children = (PCNode[])childrenList.ToArray(typeof(PCNode));

        InitializeFromJSON(node);

        meshRenderer.material = pcManager.GetFDM();

        return childrenJSON.Count > 0;
    }

    #endregion

    #region LOD

    void FetchMesh()
    {
        if (currentMeshState == MeshState.NOT_LOADED)
        {
            float priority = 1.0f / minDistanceToCam;

            Mesh mesh = pcManager.GetMeshManager().CreateMesh(fileInfo, pcManager, priority);
            if (mesh != null)
            {
                meshFilter.mesh = mesh;
                currentMeshState = MeshState.LOADED;
                meshLoadingTimeSec = Time.time;
                CheckMaterial();
            }
        }
    }

    private void RemoveMesh()
    {
        if (currentMeshState == MeshState.LOADED)
        {
            pcManager.GetMeshManager().ReleaseMesh(meshFilter.mesh); //Returning Mesh
            meshFilter.mesh = null;
            currentMeshState = MeshState.NOT_LOADED;
        }
    }

    public int CompareTo(PCNode other)
    {
        return other.minDistanceToCam.CompareTo(minDistanceToCam);
    }

    private void Update()
    {
        CheckCameraDistances();
        CheckMaterial();
    }

    private void CheckCameraDistances()
    {
        minDistanceToCam = boundsInModelSpace.MinDistance(Camera.main.transform.position);
        maxDistanceToCam = boundsInModelSpace.MaxDistance(Camera.main.transform.position);
    }

    private void CheckMaterial()
    {
        float ndmT = pcManager.nearDistanceThreshold();

        RenderType newRT;
        if (minDistanceToCam > ndmT) {
            newRT = RenderType.FDM;
        }
        else if (maxDistanceToCam < ndmT) {
            newRT = RenderType.NDM;
        }
        else
        {
            newRT = RenderType.BOTH;
        }

        //newRT = RenderType.FDM; //REMOVE

        //float minD = pcManager.nearDistanceThreshold() + boundsInModelSpace.MaxInnerDistance();
        //float maxD = pcManager.farDistanceThreshold();
        //if (minD > maxD) { minD = maxD; }
        //meshRenderer.material.SetFloat("_MaxDistance", maxD);
        //meshRenderer.material.SetFloat("_MinDistance", minD);

        if (renderType != newRT)
        {
            renderType = newRT;
            switch (renderType)
            {
                case RenderType.FDM:
                    {
                        meshRenderer.material = pcManager.GetFDM();

                        float minD = pcManager.nearDistanceThreshold();
                        float maxD = meshLoadingDistance;
                        if (minD > maxD) { minD = maxD; }
                        meshRenderer.material.SetFloat("_MaxDistance", maxD);
                        meshRenderer.material.SetFloat("_MinDistance", minD);
                        break;
                    }
                case RenderType.NDM:
                    {
                        meshRenderer.material = pcManager.GetNDM();
                        break;
                    }
                default:
                    {
                        meshRenderer.materials = new Material[] { pcManager.GetFDM(), pcManager.GetNDM() };
                        break;
                    }
            }
        }

        if (renderType == RenderType.FDM)
        {
            float timeAlpha = secSinceMeshCreated / 5.0f;
            meshRenderer.material.SetFloat("_Transparency", timeAlpha);
        }
    }

    public void ComputeNodeState(ref List<PCNode> visibleLeafNodesAndDistances,
                                        Vector3 camPosition, 
                                        float zFar)
    {
        CheckCameraDistances();

        lodTestResult = (meshLoadingDistance / minDistanceToCam);
        bool visible = minDistanceToCam < pcManager.farDistanceThreshold();

        State = (visible && lodTestResult > 1.0f) ? PCNodeState.VISIBLE : PCNodeState.INVISIBLE;
        if (State == PCNodeState.VISIBLE)
        {
            visibleLeafNodesAndDistances.Add(this);

            foreach (PCNode node in children)
            {
                if (!node.isActiveAndEnabled)
                {
                    node.CheckCameraDistances();
                }
                node.ComputeNodeState(ref visibleLeafNodesAndDistances, camPosition, zFar);
                node.gameObject.SetActive(node.State == PCNodeState.VISIBLE);
                if (node.State == PCNodeState.INVISIBLE)
                {
                    node.RemoveMesh();
                }
            }

            //Changing Material
            if (currentMeshState == MeshState.NOT_LOADED) {
                //Requesting mesh
                FetchMesh();
            }
        }
    }

    #endregion

    #region others

    private void OnDrawGizmos()
    {
        switch (renderType)
        {
            case RenderType.BOTH: { Gizmos.color = Color.green; break; }
            case RenderType.FDM: { Gizmos.color = Color.red; break; }
            case RenderType.NDM: { Gizmos.color = Color.blue; break; }
        }


        Bounds b = boundsInModelSpace;
        Gizmos.DrawWireCube(b.center, b.size);
    }

    #endregion

    #region Pointpicking

    public void GetClosestPointOnRay(Ray ray,
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



    #endregion
}