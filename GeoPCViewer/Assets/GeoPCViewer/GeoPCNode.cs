using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using UnityEngine;
using static GeoPCViewer;

public class GeoPCNode : MonoBehaviour
{
    [SerializeField] private Material farMat;
    [SerializeField] private Material nearMat;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    NodeData data;
    CellData cellData;
    GeoPCViewer viewer;

    bool childrenLoaded = false;

    // Start is called before the first frame update
    void Start()
    {

        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.A))
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
        Vector3d disp = new Vector3d(cellData.minLonLat[0] * viewer.metersPerDegree,
                                     cellData.minHeight,
                                     cellData.minLonLat[1] * viewer.metersPerDegree);

        transform.position = (Vector3)(disp - viewer.XYZOffset);
        transform.localScale = new Vector3((float)(cellData.lonLatDelta.x * viewer.metersPerDegree),
                                           (float)deltaH,
                                           (float)(cellData.lonLatDelta.y * viewer.metersPerDegree));
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
}
