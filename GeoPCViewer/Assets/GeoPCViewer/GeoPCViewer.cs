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

        public CellData(JSONNode cellData, DirectoryInfo modelDir)
        {
            string cn = cellData["directory"].Value;
            directoryInfo = modelDir.GetDirectories(cn)[0];

            minLonLat = JSONToVector2d(cellData["cell_min_lon_lat"].AsArray);
            maxLonLat = JSONToVector2d(cellData["cell_max_lon_lat"].AsArray);
            lonLatDelta = maxLonLat - minLonLat;

            minHeight = cellData["min_lon_lat_height"][2].AsDouble;
            maxHeight = cellData["max_lon_lat_height"][2].AsDouble;
        }
    }

    public struct NodeData
    {
        public readonly string filename;
        public readonly int[] indices;
        public readonly NodeData[] children;
        public readonly string name;
        public NodeData(JSONNode node)
        {
            filename = node["filename"].Value;
            name = "Node";
            var ind = node["indices"].AsArray;
            indices = new int[ind.Count];
            for (int i = 0; i < ind.Count; i++)
            {
                indices[i] = ind[i].AsInt;
                name += "_" + indices[i];
            }

            var cs = node["children"].AsArray;
            if (cs.Count > 0)
            {
                children = new NodeData[cs.Count];
                for (int i = 0; i < cs.Count; i++)
                {
                    children[i] = new NodeData(cs[i]);
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
    private MeshManager meshManager;
    public Vector3d XYZOffset { get; private set; } = default;

    #region life cycle

    // Start is called before the first frame update
    void Start()
    {
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
        NodeData nodeData = new NodeData(json);
        LoadNodeTree(nodeData, cellData, maxLevel: 1);
    }

    private void LoadNodeTree(NodeData rootNodeData, CellData cellData, uint maxLevel)
    {
        CreateNode(cellData, rootNodeData);
        if (rootNodeData.indices.Length < maxLevel)
        {
            foreach(var child in rootNodeData.children)
            {
                LoadNodeTree(child, cellData, maxLevel);
            }
        }
    }

    public void CreateNode(CellData cellData, NodeData nodeData)
    {
        GeoPCNode node = Instantiate(nodePrefab).GetComponent<GeoPCNode>();
        node.name = nodeData.name;
        node.Init(nodeData, cellData, meshManager, this);
    }

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
}
