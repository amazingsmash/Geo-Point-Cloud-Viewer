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


    DirectoryInfo directory = null;

    class PCTree
    {
        public Bounds bounds;
        public string treeFile;
        public PCNode node;
        public bool Loaded { get { return node != null; } }
        public bool InSight
        {
            get { return (bounds.MinDistance(Camera.main.transform.position) < Camera.main.farClipPlane); }
        }

        public bool IsVisible(Vector3 camPosInModelSpace, float farClipPlane)
        {
            return (bounds.MinDistance(camPosInModelSpace) < farClipPlane);
        }

        public void Load(PointCloud pc, DirectoryInfo dir)
        {
            JSONNode treeJSON = PointCloud.ReadJSON(dir, treeFile);
            node = PCNode.AddNode(treeJSON, dir, pc.gameObject, pc);
            UnityEngine.Debug.Log("Added subtree " + treeFile);
        }

        public void Unload()
        {
            Destroy(node.gameObject);
            node = null;
        }
    };
    PCTree[] topNodes = null;

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

    static public JSONNode ReadJSON(string filePath)
    {
        StreamReader reader = new StreamReader(filePath);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);
        return json;
    }

    static public JSONNode ReadJSON(DirectoryInfo dir, string fileName)
    {
        try
        {
            FileInfo index = dir.GetFiles(fileName)[0];
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

        topNodes = new PCTree[trees.Count];

        int i = 0;
        foreach (JSONNode tree in trees)
        {
            Vector3 min = tree["min"].AsVector3();
            Vector3 max = tree["max"].AsVector3();

            PCTree t = new PCTree();
            t.bounds = new Bounds((max + min) / 2, (max - min));
            t.treeFile = tree["file"].ToString().Trim('"');
            topNodes[i++] = t;
        }

        stopwatch.Stop();
        UnityEngine.Debug.Log("PC Model Loaded in ms: " + stopwatch.ElapsedMilliseconds);

        if (moveCameraToCenter)
        {
            Camera.main.transform.position = transform.TransformPoint(topNodes[0].bounds.center);
        }

        System.GC.Collect(); //Garbage Collection
    }



    private void CheckTopNodes()
    {

        Vector3 camPos = Camera.main.transform.position;
        camPos = transform.InverseTransformPoint(camPos);

        for (int i = 0; i < topNodes.Length; i++)
        {
            PCTree t = topNodes[i];
            //bool visible = t.InSight;
            bool visible = t.IsVisible(camPos, Camera.main.farClipPlane);

            if (t.Loaded)
            {
                if (!visible)
                {
                    t.Unload();
                }
            }
            else{
                if (visible) { 
                    t.Load(this, this.directory);
                    return; //Just load one per frame
                }
            }
        }
    }


    void Update()
    {
        CheckTopNodes();

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

    List<PCNode> distanceVisibleNodeList = new List<PCNode>();
    void UpdateVisibleNodeList()
    {
        distanceVisibleNodeList.Clear();
        Vector3 camPos = Camera.main.transform.position;
        float zFar = Camera.main.farClipPlane;
        foreach (PCTree tree in topNodes)
        {
            if (tree.node != null)
            {
                tree.node.ComputeNodeState(ref distanceVisibleNodeList, camPos, zFar);
            }
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
                PCNode n = ((PCNode)distanceVisibleNodeList[i]);
                n.State = (visibleMeshesCount < nMeshes) ? PCNode.PCNodeState.VISIBLE : PCNode.PCNodeState.INVISIBLE;
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
        return nearDistanceMat;
    }

    public Material GetFDM()
    {
        return farDistanceMat;
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