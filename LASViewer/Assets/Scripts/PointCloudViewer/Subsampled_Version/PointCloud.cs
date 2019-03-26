using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;
using UnityEditor;

using System.Diagnostics;

public partial class PointCloud : MonoBehaviour, IPointCloudManager
{
    public string folderPath = null;
    public GameObject listenerGO = null;
    public bool moveCameraToCenter = false;
    public float stateUpdateDeltaTime = 0.3f;

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
    PCNode[] topNodes = null;

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
        if (!transform.lossyScale.NearlyEquals(Vector3.one))
        {
            UnityEngine.Debug.Log("PointCloud must be not to scale.");
            return;
        }


        //DirectoryInfo dir = getModelDirectoryFromDialog();
        //DirectoryInfo dir = new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/92.las - BITREE");
        DirectoryInfo dir = (folderPath == null)? getModelDirectoryFromDialog() : new DirectoryInfo(folderPath);
        //DirectoryInfo dir = new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/LAS MODEL MINI");
        //"/Users/josemiguelsn/Desktop/repos/LASViewer/Models/18 - BITREE";
        InitIPointCloudManager();
        InitializeTree(dir);

        InvokeRepeating("CheckNodeRenderState", 0.0f, stateUpdateDeltaTime);
    }

    private JSONNode ReadJSON(string filePath)
    {
        StreamReader reader = new StreamReader(filePath);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);
        return json;
    }

    private JSONNode ReadJSON(DirectoryInfo dir, string fileName)
    {
        try
        {
            FileInfo index = directory.GetFiles(fileName)[0];
            JSONNode json = ReadJSON(index.FullName);
            return json;
        }
        catch
        {
            throw new System.Exception("Error reading JSON from " + fileName);
        }
    }

    private void InitializeTree(DirectoryInfo dir)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();


        this.directory = dir;
        meshManager = new MeshManager(numberOfMeshes, numberOfMeshLoadingJobs);
        JSONNode modelJSON = ReadJSON(this.directory, "pc_model.json");
        JSONArray trees = modelJSON["nodes"].AsArray;
        InitColorPalette(modelJSON["classes"].AsArray);

        topNodes = new PCNode[trees.Count];

        int i = 0;
        foreach (JSONNode tree in trees)
        {
            string fileName = tree.ToString().Trim('"');
            JSONNode treeJSON = ReadJSON(this.directory, fileName);
            topNodes[i++] = PCNode.AddNode(treeJSON, this.directory, gameObject, this);
        }

        stopwatch.Stop();
        UnityEngine.Debug.Log("PC Model Loaded in ms: " + stopwatch.ElapsedMilliseconds);

        if (moveCameraToCenter)
        {
            Camera.main.transform.position = topNodes[0].boundsInModelSpace.center;
        }

        System.GC.Collect(); //Garbage Collection
    }


    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition = Input.mousePosition;
            SelectPoint(mousePosition, 10.0f);
        }
    }

    void OnDestroy()
    {
        meshManager.StopLoaderThread();
    }

    List<PCNode.NodeAndDistance> distanceVisibleNodeList = new List<PCNode.NodeAndDistance>();
    void UpdateVisibleNodeList()
    {
        distanceVisibleNodeList.Clear();
        Vector3 camPos = Camera.main.transform.position;
        float zFar = Camera.main.farClipPlane;
        foreach (PCNode node in topNodes)
        {
            node.ComputeNodeState(ref distanceVisibleNodeList, camPos, zFar);
        }

        distanceVisibleNodeList.Sort();
    }

    private void CheckNodeRenderState()
    {
        UpdateVisibleNodeList();

        int visibleMeshesCount = 0;
        if (meshManager != null)
        {
            int nMeshes = meshManager.NAvailableMeshes;
            for (int i = distanceVisibleNodeList.Count - 1; i > -1; i--)
            {
                var n = ((PCNode.NodeAndDistance)distanceVisibleNodeList[i]);
                n.node.State = (visibleMeshesCount < nMeshes) ? PCNode.PCNodeState.VISIBLE : PCNode.PCNodeState.INVISIBLE;
                visibleMeshesCount++;
            }
        }

        //foreach(PCNode node in topNodes)
        //{
        //    node.CheckMeshState();
        //}
    }


    void SelectPoint(Vector2 screenPosition, float maxScreenDistance)
    {
        if (pcListener == null)
        {
            return;
        }

        UnityEngine.Debug.Log("Finding selected point.");

        MeshFilter[] mf = GetComponentsInChildren<MeshFilter>();
        float maxDist = 10000000.0f;

        Vector3 closestHit = Vector3.negativeInfinity;
        Color colorClosestHit = Color.black;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            PCNode node = child.GetComponent<PCNode>();
            if (node != null)
            {
                UnityEngine.Debug.Log("Finding selected point on node.");
                node.GetClosestPointOnRay(ray,
                                          screenPosition,
                                          ref maxDist,
                                          ref closestHit,
                                          ref colorClosestHit,
                                          maxScreenDistance * maxScreenDistance);
            }
        }

        if (!closestHit.Equals(Vector3.negativeInfinity))
        {
            float classCode = GetClassCodeForColor(colorClosestHit);
            pcListener.onPointSelected(closestHit, classCode);
        }
    }

}

//IPointCloudManager
public partial class PointCloud: MonoBehaviour, IPointCloudManager
{
    public Material farDistanceMat = null;
    public Material nearDistanceMat = null;

    public int numberOfMeshes = 400;
    public int numberOfMeshLoadingJobs = 20;
    private MeshManager meshManager = null;
    public float pointPhysicalSize = 0.1f; //Round point size

    float distanceThreshold = 100.0f;

    Material[] ldmats = null;
    Material[] hdmats = null;
    Material[] allMats = null;

    static Dictionary<int, Color> classColor = null;

    private void InitColorPalette(JSONArray classes)
    {
        classColor = new Dictionary<int, Color>();
        foreach (JSONNode c in classes)
        {
            int pointClass = (int)c["class"].AsFloat;
            double[] v = c["color"].AsArray.AsDoubles();
            classColor[pointClass] = new Color((float)v[0],
                                            (float)v[1], 
                                            (float)v[2]);
        }
    }

    private void InitIPointCloudManager()
    {
        //Class colors
        //classColor = new Dictionary<int, Color>();
        //classColor[3] = new Color(178.0f / 255.0f, 149.0f / 255.0f, 82.0f / 255.0f);
        //classColor[23] = new Color(139.0f / 255.0f, 196.0f / 255.0f, 60.0f / 255.0f);
        //classColor[16] = Color.blue;
        //classColor[19] = Color.blue;
        //classColor[17] = Color.red;
        //classColor[20] = Color.green;
        //classColor[31] = new Color(244.0f / 255.0f, 191.0f / 255.0f, 66.0f / 255.0f);
        //classColor[29] = Color.black;
        //classColor[30] = new Color(244.0f / 255.0f, 65.0f / 255.0f, 244.0f / 255.0f);

        //Materials

        hdmats = new Material[] { farDistanceMat };
        ldmats = new Material[] { nearDistanceMat };
        allMats = new Material[] { farDistanceMat, nearDistanceMat };


        distanceThreshold = Camera.main.GetDistanceForLenghtToScreenSize(pointPhysicalSize, 1);
    }

    Color IPointCloudManager.GetColorForClass(int classification)
    {
        return (classColor.ContainsKey(classification)) ? classColor[classification] : Color.gray;
    }

    float GetClassCodeForColor(Color color)
    {
        foreach(var entry in classColor)
        {
            if (entry.Value.IsEqualsTo(color))
            {
                return entry.Key;
            }
        }
        return 0.0f;
    }

    MeshManager IPointCloudManager.GetMeshManager()
    {
        return meshManager;
    }

    void IPointCloudManager.ModifyRendererBasedOnBounds(Bounds bounds, MeshRenderer meshRenderer)
    {
        float maxDistance = bounds.MaxDistance(Camera.main.transform.position);
        float minDistance = bounds.MinDistance(Camera.main.transform.position);
        meshRenderer.material = (maxDistance < distanceThreshold)? farDistanceMat : nearDistanceMat;

        if (minDistance > distanceThreshold) //Closest Point too far
        {
            meshRenderer.materials = ldmats;
        }
        else if (maxDistance < distanceThreshold)
        {
            meshRenderer.materials = hdmats;
        }
        else
        {
            meshRenderer.materials = allMats;
        }
    }


    public Material GetNDM()
    {
        return farDistanceMat;
    }

    public Material GetFDM()
    {
        return nearDistanceMat;
    }

    public float nearDistanceThreshold()
    {
        return distanceThreshold;
    }

    public float farDistanceThreshold()
    {
        return Camera.main.farClipPlane;
    }
}