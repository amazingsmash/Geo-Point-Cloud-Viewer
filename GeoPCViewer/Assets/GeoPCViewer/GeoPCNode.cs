using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using UnityEngine;
using static GeoPCViewer;

public class GeoPCNode : MonoBehaviour
{
    public enum RenderType
    {
        FDM, NDM, BOTH
    };

    [SerializeField] private float lodFactor = 1f;
    [SerializeField] private Material farMat;
    [SerializeField] private Material nearMat;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    NodeData data;
    CellData cellData;
    GeoPCViewer viewer;

    bool childrenLoaded = false;
    private float minDistanceToCam;
    private float maxDistanceToCam;
    private bool needsChildren;
    private Bounds worldSpaceBounds;
    private RenderType renderType;
    private float creationTime;

    // Start is called before the first frame update
    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
    }

    // Update is called once per frame
    void Update()
    {
        RecalculateLoDParameters();
        CheckMaterial();

        if (Input.GetKeyUp(KeyCode.I) || needsChildren)
        {
            LoadChildren();
        }

    }

    public void Init(NodeData data,
        CellData cellData,
        MeshManager meshManager,
        GeoPCViewer viewer)
    {
        this.data = data;
        this.cellData = cellData;
        this.viewer = viewer;

        var fi = cellData.directoryInfo.GetFiles(data.filename)[0];
        StartCoroutine(LoadGeometryCoroutine(fi,
                                            meshManager,
                                            viewer.GetColorForClass,
                                            100));

        double deltaH = cellData.maxHeight - cellData.minHeight;
        Vector3d degreesToMeters = new Vector3d(viewer.metersPerDegree, 1, viewer.metersPerDegree);
        Vector3d disp = Vector3d.Scale(cellData.lonHLatMin, degreesToMeters);

        Vector3d worldPosition = disp - viewer.XYZOffset;
        Vector3d size = Vector3d.Scale(cellData.lonHLatDelta, degreesToMeters);

        transform.position = (Vector3)worldPosition;
        transform.localScale = (Vector3)size;

        Vector3d pointsBoundMin = Vector3d.Scale(data.minPoints, size);
        Vector3d pointsBoundMax = Vector3d.Scale(data.maxPoints, size);
        Vector3d pointsBoundSize = pointsBoundMax - pointsBoundMin;
        Vector3d pointsBoundCenter = (pointsBoundMax + pointsBoundMin) / 2;
        worldSpaceBounds = new Bounds((Vector3)(pointsBoundCenter + worldPosition), (Vector3)pointsBoundSize);
    }

    private IEnumerator LoadGeometryCoroutine(FileInfo fi, MeshManager meshManager, MeshLoaderJob.GetColorForClass getColorForClass, int priority)
    {
        Mesh mesh01 = null;
        while (mesh01 == null)
        {
            mesh01 = meshManager.CreateMesh(fi, getColorForClass, priority);
            yield return null;
        }

        meshFilter.mesh = mesh01;
        meshRenderer.material = farMat;
        creationTime = Time.time;
    }

    public void LoadChildren()
    {
        if (!childrenLoaded)
        {
            foreach (var child in data.children)
            {
                viewer.CreateNode(cellData, child);
            }
            childrenLoaded = true;
        }
    }

    private void RecalculateLoDParameters()
    {
        minDistanceToCam = worldSpaceBounds.MinDistance(Camera.main.transform.position);
        maxDistanceToCam = worldSpaceBounds.MaxDistance(Camera.main.transform.position);

        if (worldSpaceBounds.Contains(Camera.main.transform.position))
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
            switch (renderType)
            {
                case RenderType.FDM:
                    {
                        meshRenderer.material = farMat;

                        //float minD = pcManager.nearDistanceThreshold();
                        //float maxD = meshLoadingDistance;
                        //if (minD > maxD) { minD = maxD; }
                        //meshRenderer.material.SetFloat("_MaxDistance", maxD);
                        //meshRenderer.material.SetFloat("_MinDistance", minD);
                        break;
                    }
                case RenderType.NDM:
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
        float ndmT = viewer.nearMatDistance;

        RenderType newRT;
        if (minDistanceToCam > ndmT)
        {
            newRT = RenderType.FDM;
        }
        else if (maxDistanceToCam < ndmT)
        {
            newRT = RenderType.NDM;
        }
        else
        {
            newRT = RenderType.BOTH;
        }

        return newRT;
    }

    #region Gizmos

    private void OnDrawGizmos()
    {
        switch (renderType)
        {
            case RenderType.BOTH: { Gizmos.color = Color.green; break; }
            case RenderType.FDM: { Gizmos.color = Color.red; break; }
            case RenderType.NDM: { Gizmos.color = Color.blue; break; }
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
