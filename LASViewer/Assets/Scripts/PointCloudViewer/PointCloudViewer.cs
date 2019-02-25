using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;
using UnityEditor;

public interface IPointCloudListener
{
    void onPointSelected(Vector3 point);
}

public interface IPointCloudManager
{
    Color GetColorForClass(float classification);
    MeshManager GetMeshManager();
    float HDHorizontalRelativeScreenSize { get; }
    Material HDMaterial { get; }
    Material LDMaterial { get; }
}

public partial class PointCloudViewer : MonoBehaviour, IPointCloudManager
{
    public string folderPath = null;
    public GameObject listenerGO = null;
    public bool moveCameraToCenter = false;

    private IPointCloudListener pcListener
    {
        get
        {
            if (listenerGO != null)
            {
                return listenerGO.GetComponent<IPointCloudListener>();
            }
            return null;
        }
    }


    DirectoryInfo directory = null;
    PointCloudNode[] topNodes = null;

    DirectoryInfo getModelDirectoryFromDialog()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFolderPanel("Select Model Folder", "", "");
        if (path.Length > 0)
        {
            return new DirectoryInfo(path);
        }
        return null;
#else
        return new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/LAS MODEL");
#endif
    }

    // Use this for initialization
    void Start()
    {
        //DirectoryInfo dir = getModelDirectoryFromDialog();
        //DirectoryInfo dir = new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/92.las - BITREE");
        DirectoryInfo dir = (folderPath == null)? getModelDirectoryFromDialog() : new DirectoryInfo(folderPath);
        //DirectoryInfo dir = new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/LAS MODEL MINI");
        //"/Users/josemiguelsn/Desktop/repos/LASViewer/Models/18 - BITREE";
        Initialize(dir);

        InvokeRepeating("CheckNodeRenderState", 0.0f, 0.3f);
    }

    public void Initialize(DirectoryInfo directory)
    {
        if (!transform.lossyScale.NearlyEquals(Vector3.one))
        {
            Debug.LogError("PointCloud must be not to scale.");
            return;
        }

        this.directory = directory;
        meshManager = new MeshManager(numberOfMeshes, numberOfMeshLoadingJobs);
        FileInfo index = directory.GetFiles("voxelIndex.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        if (json.IsArray)
        {
            topNodes = new PointCloudNode[json.AsArray.Count];
            for (int i = 0; i < json.AsArray.Count; i++)
            {
                topNodes[i] = PointCloudNode.AddNode(json.AsArray[i], this.directory, gameObject, this);
            }
        }
        else
        {
            topNodes = new PointCloudNode[1];
            topNodes[0] = PointCloudNode.AddNode(json, this.directory, gameObject, this);
        }

        if (moveCameraToCenter)
        {
            Camera.main.transform.position = topNodes[0].boundsInModelSpace.center;
        }

        System.GC.Collect(); //Garbage Collection
    }


    void Update()
    {
        CheckNodeRenderState();

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition = Input.mousePosition;
            SelectPoint(mousePosition, 10.0f);
        }
    }

    List<PointCloudLeafNode.NodeAndDistance> distanceVisibleNodeList = new List<PointCloudLeafNode.NodeAndDistance>();
    void UpdateVisibleLeafNodesList()
    {
        distanceVisibleNodeList.Clear();
        Vector3 camPos = Camera.main.transform.position;
        float zFar = Camera.main.farClipPlane;
        foreach (PointCloudNode node in topNodes)
        {
            node.ComputeNodeState(ref distanceVisibleNodeList, camPos, zFar);
        }

        distanceVisibleNodeList.Sort();
    }

    private void CheckNodeRenderState()
    {
        UpdateVisibleLeafNodesList();

        int visibleMeshesCount = 0;
        if (meshManager != null)
        {
            int nMeshes = meshManager.NAvailableMeshes;
            for (int i = distanceVisibleNodeList.Count - 1; i > -1; i--)
            {
                var n = ((PointCloudLeafNode.NodeAndDistance)distanceVisibleNodeList[i]);
                n.node.State = (visibleMeshesCount < nMeshes) ? PointCloudNode.PCNodeState.VISIBLE : PointCloudNode.PCNodeState.INVISIBLE;
                visibleMeshesCount++;
            }
        }
    }


    void SelectPoint(Vector2 screenPosition, float maxScreenDistance)
    {
        if (pcListener == null)
        {
            return;
        }

        //Debug.Log("Finding selected point.");

        MeshFilter[] mf = GetComponentsInChildren<MeshFilter>();
        float maxDist = 10000000.0f;

        Vector3 closestHit = Vector3.negativeInfinity;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            PointCloudNode node = child.GetComponent<PointCloudNode>();
            if (node != null)
            {
                node.GetClosestPointOnRay(ray,
                                          screenPosition,
                                          ref maxDist,
                                          ref closestHit,
                                          maxScreenDistance * maxScreenDistance);
            }
        }

        if (!closestHit.Equals(Vector3.negativeInfinity))
        {
            pcListener.onPointSelected(closestHit);
        }
    }
}

//IPointCloudManager
public partial class PointCloudViewer : MonoBehaviour, IPointCloudManager
{
    public Material hdMaterial = null;
    public Material ldMaterial = null;

    public int numberOfMeshes = 400;
    public float hDRelativeScreenSize = 0.9f;
    public int numberOfMeshLoadingJobs = 20;
    private MeshManager meshManager = null;

    static Dictionary<float, Color> classColor = null;
    Color IPointCloudManager.GetColorForClass(float classification)
    {
        if (classColor == null)
        {
            classColor = new Dictionary<float, Color>();
            classColor[16] = Color.blue;
            classColor[19] = Color.blue;
            classColor[17] = Color.red;
            classColor[20] = Color.green;
            classColor[31] = new Color(244.0f / 255.0f, 191.0f / 255.0f, 66.0f / 255.0f);
            classColor[29] = Color.black;
            classColor[30] = new Color(244.0f / 255.0f, 65.0f / 255.0f, 244.0f / 255.0f);
        }

        return (classColor.ContainsKey(classification)) ? classColor[classification] : Color.gray;
    }

    MeshManager IPointCloudManager.GetMeshManager()
    {
        return meshManager;
    }

    float IPointCloudManager.HDHorizontalRelativeScreenSize { get { return hDRelativeScreenSize; } }
    Material IPointCloudManager.HDMaterial { get { return hdMaterial; } }
    Material IPointCloudManager.LDMaterial { get { return ldMaterial; } }
}