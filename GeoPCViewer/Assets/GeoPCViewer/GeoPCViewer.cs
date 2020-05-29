using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using UnityEngine;

public class GeoPCViewer : MonoBehaviour
{
    public struct CellData
    {
        public readonly Vector2d minLonLat, maxLonLat, lonLatDelta;
        public readonly double minHeight, maxHeight;
        public readonly DirectoryInfo directoryInfo;
        public Vector3d lonHLatDelta;
        public Vector3d lonHLatMin;

        public CellData(JSONNode cellJSON, DirectoryInfo modelDir)
        {
            string cn = cellJSON["directory"].Value;
            directoryInfo = modelDir.GetDirectories(cn)[0];

            minLonLat = JSONToVector2d(cellJSON["cell_min_lon_lat"].AsArray);
            maxLonLat = JSONToVector2d(cellJSON["cell_max_lon_lat"].AsArray);
            lonLatDelta = maxLonLat - minLonLat;

            minHeight = cellJSON["min_lon_lat_height"][2].AsDouble;
            maxHeight = cellJSON["max_lon_lat_height"][2].AsDouble;

            double deltaH = maxHeight - minHeight;
            lonHLatDelta = new Vector3d(lonLatDelta.x, deltaH, lonLatDelta.y);
            lonHLatMin =new Vector3d(minLonLat[0], minHeight, minLonLat[1]);

        }
    }

    public struct NodeData
    {
        public readonly CellData cellData;
        public readonly string filename;
        public readonly double avgPointDistance;
        public readonly Vector3d minPoints;
        public readonly Vector3d maxPoints;
        public readonly FileInfo pcFile;
        public readonly int[] indices;
        public readonly NodeData[] children;
        public readonly string name;
        public NodeData(JSONNode nodeJSON, CellData cellData)
        {
            this.cellData = cellData;

            //TODO maybe no file
            filename = nodeJSON["filename"].Value;
            pcFile = cellData.directoryInfo.GetFiles(filename)[0];


            avgPointDistance = nodeJSON["avgDistance"].AsDouble;
            minPoints = JSONToVector3d(nodeJSON["min"].AsArray);
            maxPoints = JSONToVector3d(nodeJSON["max"].AsArray);

            name = "Node";
            var ind = nodeJSON["indices"].AsArray;
            indices = new int[ind.Count];
            for (int i = 0; i < ind.Count; i++)
            {
                indices[i] = ind[i].AsInt;
                name += "_" + indices[i];
            }

            var cs = nodeJSON["children"].AsArray;
            if (cs.Count > 0)
            {
                children = new NodeData[cs.Count];
                for (int i = 0; i < cs.Count; i++)
                {
                    children[i] = new NodeData(cs[i], cellData);
                }
            }
            else
            {
                children = null;
            }

        }
    }


    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private string directory;
    [SerializeField] private Dictionary<int, Color> classColor = null;
    public int numberOfMeshes = 400;
    public int numberOfMeshLoadingJobs = 20;
    public double metersPerDegree = 11111.11;
    public float distanceThreshold;
    private MeshManager meshManager;
    public Vector3d XYZOffset { get; private set; } = default;
    public float nearMatDistance = 100;
    public float pointPhysicalSize = 0.1f; //Round point size

    public delegate void OnPointSelected(Vector3 point, float classCode);
    public OnPointSelected onPointSelected = null;

    #region life cycle

    // Start is called before the first frame update
    void Start()
    {
        distanceThreshold = Camera.main.GetDistanceForLenghtToScreenSize(pointPhysicalSize, 1);

        meshManager = new MeshManager(numberOfMeshes, numberOfMeshLoadingJobs);
        InitIPointCloudManager();

        DirectoryInfo modelDir = new DirectoryInfo(directory);
        FileInfo index = modelDir.GetFiles("pc_model.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        int cellCounter = 0;
        foreach(JSONNode c in json["cells"].AsArray)
        {
            CellData cellData = new CellData(c, modelDir);
            InitCell(cellData, cellCounter++);
        }
    }



    // Update is called once per frame
    void Update()
    {
    }

    #endregion

    public static Vector3d JSONToVector3d(JSONArray lonLatHeigth)
    {
        return new Vector3d(lonLatHeigth[0], lonLatHeigth[2], lonLatHeigth[1]);
    }

    public static Vector2d JSONToVector2d(JSONArray lonLat)
    {
        return new Vector2d(lonLat[0], lonLat[1]);
    }

    private void InitCell(CellData cellData, int nodeNumber)
    {
        FileInfo index = cellData.directoryInfo.GetFiles("cell.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        if (nodeNumber == 0)
        {
            XYZOffset = new Vector3d(cellData.minLonLat[0] * metersPerDegree,
                                    cellData.minHeight,
                                    cellData.minLonLat[1] * metersPerDegree);
        }
        NodeData nodeData = new NodeData(json, cellData);
        LoadNodeTree(nodeData, cellData, maxLevel: 1);
    }

    private void LoadNodeTree(NodeData rootNodeData, CellData cellData, uint maxLevel)
    {
        CreateNode(rootNodeData);
        if (rootNodeData.indices.Length < maxLevel)
        {
            foreach(var child in rootNodeData.children)
            {
                LoadNodeTree(child, cellData, maxLevel);
            }
        }
    }

    public GeoPCNode CreateNode(NodeData nodeData)
    {
        GeoPCNode node = Instantiate(nodePrefab).GetComponent<GeoPCNode>();
        node.name = nodeData.name;
        node.Init(nodeData, meshManager, this);
        return node;
    }

    #region Colors

    private void InitIPointCloudManager()
    {
        //Class colors
        classColor = new Dictionary<int, Color>();
        classColor[3] = new Color(178.0f / 255.0f, 149.0f / 255.0f, 82.0f / 255.0f);
        classColor[23] = new Color(139.0f / 255.0f, 196.0f / 255.0f, 60.0f / 255.0f);
        classColor[16] = Color.blue;
        classColor[19] = Color.blue;
        classColor[17] = Color.red;
        classColor[20] = Color.green;
        classColor[31] = new Color(244.0f / 255.0f, 191.0f / 255.0f, 66.0f / 255.0f);
        classColor[29] = Color.black;
        classColor[30] = new Color(244.0f / 255.0f, 65.0f / 255.0f, 244.0f / 255.0f);
    }

    public Color GetColorForClass(int c)
    {
        return classColor.TryGetValue(c, out Color color) ? color : Color.white;
    }

    float GetClassCodeForColor(Color color)
    {
        foreach (var entry in classColor)
        {
            if (entry.Value.IsEqualsTo(color))
            {
                return entry.Key;
            }
        }
        return 0.0f;
    }

    #endregion

    #region Pointpicking

    void SelectPoint(Vector2 screenPosition, float maxScreenDistance)
    {
        if (onPointSelected == null)
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
            onPointSelected.Invoke(closestHit, classCode);
        }
    }

    #endregion
}
