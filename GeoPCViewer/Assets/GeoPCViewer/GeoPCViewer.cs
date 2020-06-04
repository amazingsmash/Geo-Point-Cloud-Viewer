using System;
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
        public readonly int nPoints;
        public readonly FileInfo pcFile;
        public readonly int[] indices;
        public readonly NodeData[] children;
        public readonly string name;
        public readonly Dictionary<int, int> sortedClassCount;

        public NodeData(JSONNode nodeJSON, CellData cellData)
        {
            this.cellData = cellData;

            //TODO maybe no file
            filename = nodeJSON["filename"].Value;
            pcFile = cellData.directoryInfo.GetFiles(filename)[0];

            nPoints = nodeJSON["n_points"].AsInt;
            avgPointDistance = nodeJSON["avg_distance"].AsDouble;
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

            sortedClassCount = new Dictionary<int, int>();
            var sortedClassCountJSON = nodeJSON["sorted_class_count"];
            foreach (string c in sortedClassCountJSON.Keys)
            {
                int pointClass = (int)float.Parse(c);
                sortedClassCount[pointClass] = sortedClassCountJSON[c].AsInt;
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

        public Color[] GetPointColors(Dictionary<int, Color> classColor)
        {
            var colors = new Color[nPoints];
            int p = 0;
            foreach(int pointClass in sortedClassCount.Keys)
            {
                int n = sortedClassCount[pointClass];
                Color color = classColor.TryGetValue(pointClass, out Color dictColor) ? dictColor : Color.white;
                for (int i = 0; i < n; i++)
                {
                    colors[p++] = color;
                }
            }
            return colors;
        }
    }


    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private string directory;
    public Dictionary<int, Color> classColor { get; set; } = new Dictionary<int, Color>();

    public int numberOfMeshes = 400;
    public int numberOfMeshLoadingJobs = 20;
    public double metersPerDegree = 11111.11;
    public float distanceThreshold;
    private MeshManager meshManager;
    public Vector3d XYZOffset { get; private set; } = default;
    public bool LoDControlKeys = false;

    public float nearMatDistance = 100;
    public float pointPhysicalSize = 0.1f; //Round point size

    private List<GeoPCNode> cellNodes = new List<GeoPCNode>();

    #region life cycle

    // Start is called before the first frame update
    void Start()
    {
        distanceThreshold = Camera.main.GetDistanceForLenghtToScreenSize(pointPhysicalSize, 1);

        meshManager = new MeshManager(numberOfMeshes, numberOfMeshLoadingJobs);

        DirectoryInfo modelDir = new DirectoryInfo(directory);
        FileInfo index = modelDir.GetFiles("pc_model.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        InitColorPalette(json["classes"].AsArray);

        int cellCounter = 0;
        foreach(JSONNode c in json["cells"].AsArray)
        {
            CellData cellData = new CellData(c, modelDir);
            InitCell(cellData, cellCounter++);
        }
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
        GeoPCNode n = CreateNode(nodeData);
        cellNodes.Add(n);
    }


    public GeoPCNode CreateNode(NodeData nodeData)
    {
        GeoPCNode node = Instantiate(nodePrefab).GetComponent<GeoPCNode>();
        node.name = nodeData.name;
        node.Init(nodeData, meshManager, this);
        return node;
    }

    #region Colors


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

    public bool SelectPoint(Vector2 screenPosition,
                            float maxScreenDistance,
                            out Vector3 point,
                            out float pointClass)
    {
        UnityEngine.Debug.Log("Finding selected point.");

        float maxDist = 10000000.0f;

        Vector3 closestHit = Vector3.negativeInfinity;
        Color colorClosestHit = Color.black;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        foreach(GeoPCNode node in cellNodes)
        {
            UnityEngine.Debug.Log("Finding selected point on node.");
            node.GetClosestPointOnRay(ray,
                                        screenPosition,
                                        ref maxDist,
                                        ref closestHit,
                                        ref colorClosestHit,
                                        maxScreenDistance * maxScreenDistance);
        }

        bool hit = !closestHit.Equals(Vector3.negativeInfinity);
        pointClass = hit ? GetClassCodeForColor(colorClosestHit) : default;
        point = hit? closestHit : default;
        return hit;
    }

    #endregion
}
