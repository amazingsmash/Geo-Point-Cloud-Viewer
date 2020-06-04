using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Assertions;
using static GeoPCViewer;

public partial class GeoPCNode : MonoBehaviour
{
    public enum RenderType
    {
        UNKNOWN, FAR, NEAR, MIXED
    };

    public enum State
    {
        NOT_INIT, INIT, FETCHING_MESH, RENDERING
    }

    [SerializeField] private float lodFactor = 1f;
    [SerializeField] private Material farMat;
    [SerializeField] private Material nearMat;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private NodeData data;
    private GeoPCViewer viewer;
    private float minDistanceToCam;
    private float maxDistanceToCam;
    private bool needsChildren;
    private Bounds worldSpaceBounds;
    private RenderType renderType = RenderType.UNKNOWN;
    private float creationTime;
    private State state = State.NOT_INIT;

    private List<GeoPCNode> children = new List<GeoPCNode>();
    private GeoPCNodeMeshLoadingJob meshJob = null;

    private static AsyncJobManager meshLoaderJM = new AsyncJobManager();

    #region Life Cycle

    // Start is called before the first frame update
    IEnumerator Start()
    {
        Assert.IsNotNull(viewer, "GeoPCNode not initilized"); 

        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        if (data.pcFile != null)
        {
            //Mesh Loading
            meshRenderer.material = new Material(meshRenderer.material);
            yield return null;
            GeoPCNodeMeshLoadingJob meshJob = new GeoPCNodeMeshLoadingJob(this);
            meshLoaderJM.RunJob(meshJob, 100);
            state = State.FETCHING_MESH;
            while (!meshJob.IsDone)
            {
                yield return null;
            }
            meshFilter.mesh = new Mesh()
            {
                vertices = meshJob.points,
                colors = meshJob.colors
            };
            yield return null;
            meshFilter.mesh.SetIndices(meshJob.indices, MeshTopology.Points, 0);
            yield return null;
            meshRenderer.material = farMat;
        }
        else
        {
            meshRenderer.enabled = false;
        }
        state = State.RENDERING;
        creationTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        if (state == State.RENDERING)
        {
            RecalculateLoDParameters();
            CheckMaterial();

            bool increaseLoD = viewer.DetailControlKeys ? Input.GetKeyUp(KeyCode.I) : needsChildren;
            bool decreaseLoD = viewer.DetailControlKeys ? Input.GetKeyUp(KeyCode.O) : !needsChildren;

            if (increaseLoD && children.Count == 0)
            {
                LoadChildren();
            }
            else
            {
                if (decreaseLoD && children.Count > 0)
                {
                    RemoveChildren();
                }
            }
        }

    }

    private void OnApplicationQuit()
    {
        meshLoaderJM.Stop();
    }

    #endregion

    #region Initialization

    public void Init(NodeData data, GeoPCViewer viewer)
    {
        this.data = data;
        this.viewer = viewer;

        Vector3d degreesToMeters = new Vector3d(viewer.metersPerDegree, 1, viewer.metersPerDegree);
        Vector3d disp = Vector3d.Scale(data.cellData.lonHLatMin, degreesToMeters);

        Vector3d worldPosition = disp - viewer.XYZOffset;
        Vector3d size = Vector3d.Scale(data.cellData.lonHLatDelta, degreesToMeters);

        transform.position = (Vector3)worldPosition;
        transform.localScale = (Vector3)size;

        Vector3d pointsBoundMin = Vector3d.Scale(data.minPoints, size);
        Vector3d pointsBoundMax = Vector3d.Scale(data.maxPoints, size);
        Vector3d pointsBoundSize = pointsBoundMax - pointsBoundMin;
        Vector3d pointsBoundCenter = (pointsBoundMax + pointsBoundMin) / 2;
        worldSpaceBounds = new Bounds((Vector3)(pointsBoundCenter + worldPosition), (Vector3)pointsBoundSize);

        state = State.INIT;
    }

    #endregion

    #region LoD

    public void LoadChildren()
    {
        if (children.Count == 0)
        {
            if (data.children != null)
            {
                foreach (var child in data.children)
                {
                    children.Add(viewer.CreateNode(child));
                }
            }
        }
    }

    private void RemoveChildren()
    {
        if (children.Count > 0)
        {
            foreach (var child in children)
            {
                Destroy(child.gameObject);
            }
            children.Clear();
        }
    }

    private void RecalculateLoDParameters()
    {
        Vector3 camPos = Camera.main.transform.position;
        bool camInside = worldSpaceBounds.Contains(camPos);
        minDistanceToCam = camInside ? 0 : worldSpaceBounds.MinDistance(camPos);
        maxDistanceToCam = worldSpaceBounds.MaxDistance(camPos);

        if (camInside)
        {
            needsChildren = true;
        }
        else
        {
            double worldSpaceAvgDist = viewer.metersPerDegree * data.avgPointDistance;
            needsChildren = (worldSpaceAvgDist * lodFactor) > minDistanceToCam;
        }
    }

    private void CheckMaterial()
    {
        RenderType newRT = GetRenderType();

        if (renderType != newRT)
        {
            renderType = newRT;

            //TODO Do Multipass
            /*
            string p0 = meshRenderer.material.GetPassName(0);
            string p1 = meshRenderer.material.GetPassName(1);
            //meshRenderer.material.SetShaderPassEnabled(p0, false);
            //meshRenderer.material.SetShaderPassEnabled(p1, false);
            meshRenderer.material.SetPass(0);
            meshRenderer.material.SetPass(1);
            bool b1 = meshRenderer.material.GetShaderPassEnabled(p0);
            //Debug.Log(b1);
            */

            switch (renderType)
            {
                case RenderType.FAR:
                    {
                        meshRenderer.material = farMat;

                        //float minD = pcManager.nearDistanceThreshold();
                        //float maxD = meshLoadingDistance;
                        //if (minD > maxD) { minD = maxD; }
                        //meshRenderer.material.SetFloat("_MaxDistance", maxD);
                        //meshRenderer.material.SetFloat("_MinDistance", minD);
                        break;
                    }
                case RenderType.NEAR:
                    {
                        meshRenderer.material = nearMat;
                        break;
                    }
                default:
                    {
                        meshRenderer.materials = new Material[] { farMat, nearMat };
                        break;
                    }
            }
        }

        /*
        if (renderType == RenderType.FDM)
        {
            float timeAlpha = (Time.time - creationTime) / 5.0f;
            meshRenderer.material.SetFloat("_Transparency", timeAlpha);
        }
        */
    }

    private RenderType GetRenderType()
    {

        if (needsChildren)
        {
            return RenderType.MIXED;
        }

        if (minDistanceToCam > viewer.distanceThreshold) //Closest Point too far
        {
            return RenderType.FAR;
        }
        else if (maxDistanceToCam < viewer.distanceThreshold)
        {
            return RenderType.NEAR;
        }
        else
        {
            return RenderType.MIXED;
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        switch (renderType)
        {
            case RenderType.MIXED: { Gizmos.color = Color.green; break; }
            case RenderType.FAR: { Gizmos.color = Color.red; break; }
            case RenderType.NEAR: { Gizmos.color = Color.blue; break; }
        }

        Gizmos.DrawWireCube(worldSpaceBounds.center, worldSpaceBounds.size);
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
        Mesh mesh = meshFilter.mesh;
        if (mesh == null)
        {
            return;
        }
        Bounds meshBounds = worldSpaceBounds;
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
