using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SimpleJSON;
using UnityEngine;

public class GeoPCViewer : MonoBehaviour
{
    #region Model Parsing

    public abstract class GlobalGrid
    {
        public static GlobalGrid Parse(JSONNode json)
        {
            switch (json["type"].Value)
            {
                case "TileMapServiceGG":
                    return new TileMapServiceGG(json);
                default:
                    return null;
            }
        }
    }

    public class TileMapServiceGG : GlobalGrid {
        public readonly int level;
        public readonly int nSideTiles;
        public TileMapServiceGG(JSONNode json)
        {
            level = json["level"].AsInt;
            nSideTiles = (int)Math.Pow(2, level);
        }

        public Vector2Int GetGoogleMapsIndex(Vector2Int tmsIndex)
        {
            return new Vector2Int(tmsIndex.x,
                nSideTiles + 1 - tmsIndex.y);
        }
    }

    public struct Box{
        public readonly Vector3d Min, Max;
        public Box(Vector3d min, Vector3d max)
        {
            this.Min = min;
            this.Max = max;
        }

        public Vector3d Center { get => (Max + Min) / 2; }
        public Vector3d Size { get => (Max - Min); }
    }

    public class ModelData
    {
        public readonly DirectoryInfo directory;
        public readonly string name;
        public readonly GlobalGrid globalGrid;
        public readonly int maxNodePoints;
        public readonly bool parentSampling;
        public readonly string partitioningMethod;
        public readonly CellData[] cells;
        public readonly Dictionary<int, Color> classColor;
        public readonly Box pcBounds;

        public ModelData(JSONNode modelJSON, DirectoryInfo modelDir)
        {
            directory = modelDir;
            name = modelJSON["model_name"].Value;
            globalGrid = GlobalGrid.Parse( modelJSON["global_grid"] );
            maxNodePoints = modelJSON["max_node_points"].AsInt;
            parentSampling = modelJSON["parent_sampling"].AsBool;
            partitioningMethod = modelJSON["partitioning_method"].Value;

            var classesJSON = modelJSON["classes"].AsArray;
            classColor = new Dictionary<int, Color>();
            foreach (JSONNode c in classesJSON)
            {
                int pointClass = (int)c["class"].AsFloat;
                double[] v = c["color"].AsArray.AsDoubles();
                classColor[pointClass] = new Color((float)v[0],
                                                (float)v[1],
                                                (float)v[2]);
            }

            var cellDataJSONs = modelJSON["cells"].AsArray;
            cells = new CellData[cellDataJSONs.Count];

            var pcBoundsMin = new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue);
            var pcBoundsMax = new Vector3d(double.MinValue, double.MinValue, double.MinValue);
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new CellData(cellDataJSONs[i], this);
                pcBoundsMin = Vector3d.Min(pcBoundsMin, cells[i].pcBounds.Min);
                pcBoundsMax = Vector3d.Min(pcBoundsMax, cells[i].pcBounds.Max);
            }

            pcBounds = new Box(pcBoundsMin, pcBoundsMax);
        }
    }

    public class CellData
    {
        public readonly Box pcBounds, extent;
        public readonly Vector2Int index;
        public readonly DirectoryInfo directoryInfo;
        public readonly ModelData modelData;

        public CellData(JSONNode cellJSON, ModelData modelData)
        {
            this.modelData = modelData;
            string cn = cellJSON["directory"].Value;
            index = JSON_XY_ToVector2Int(cellJSON["cell_index"].AsArray);
            directoryInfo = modelData.directory.GetDirectories(cn)[0];

            extent = new Box(JSON_XZY_ToVector3d(cellJSON["cell_extent_min"].AsArray),
                JSON_XZY_ToVector3d(cellJSON["cell_extent_max"].AsArray));

            pcBounds = new Box(JSON_XZY_ToVector3d(cellJSON["pc_bounds_min"].AsArray),
                JSON_XZY_ToVector3d(cellJSON["pc_bounds_max"].AsArray));
        }

        public string GetOSMTileURL()
        {
            if (modelData.globalGrid is TileMapServiceGG gg)
            {
                var i = gg.GetGoogleMapsIndex(index);
                return $"https://b.tile.openstreetmap.org/{gg.level}/{i.x}/{i.y}.png";
            }
            return null;
        }

        public string GetARCGISWorldImageryTileURL()
        {
            if (modelData.globalGrid is TileMapServiceGG gg)
            {
                var i = gg.GetGoogleMapsIndex(index);
                return $"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{gg.level}/{i.y}/{i.x}.png";
            }
            return null;

        
        }
    }

    public struct NodeData
    {
        public readonly CellData cellData;
        public readonly double avgPointDistance;
        public readonly Box pcBounds;
        public readonly int nPoints;
        public readonly FileInfo pcFile;
        public readonly int[] indices;
        public readonly NodeData[] children;
        public readonly string name;
        public readonly Dictionary<int, int> sortedClassCount;

        public NodeData(JSONNode nodeJSON, CellData cellData)
        {
            this.cellData = cellData;

            JSONNode fn = nodeJSON["filename"];
            pcFile = fn == null? null : cellData.directoryInfo.GetFiles(fn.Value)[0];

            nPoints = nodeJSON["n_points"].AsInt;
            avgPointDistance = nodeJSON["avg_distance"].AsDouble;

            var min01 = JSON_XZY_ToVector3d(nodeJSON["min"].AsArray);
            var max01 = JSON_XZY_ToVector3d(nodeJSON["max"].AsArray);

            pcBounds = new Box( Vector3d.Scale(min01, cellData.extent.Size) + cellData.extent.Min,
                                Vector3d.Scale(max01, cellData.extent.Size) + cellData.extent.Min);

            var ind = nodeJSON["indices"].AsArray;
            indices = new int[ind.Count];
            for (int i = 0; i < ind.Count; i++)
            {
                indices[i] = ind[i].AsInt;
            }
            name = $"Node_{string.Join<int>("_", indices)}";

            sortedClassCount = new Dictionary<int, int>();
            var sortedClassCountJSON = nodeJSON["sorted_class_count"];
            foreach (string c in sortedClassCountJSON.Keys)
            {
                int pointClass = (int)float.Parse(c);
                sortedClassCount[pointClass] = sortedClassCountJSON[c].AsInt;
            }

            var cs = nodeJSON["children"].AsArray;
            if (cs != null && cs.Count > 0)
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

    public static Vector3d JSON_XZY_ToVector3d(JSONArray v)
    {
        return new Vector3d(v[0].AsDouble,
                            v[2].AsDouble,
                            v[1].AsDouble);
    }

    public static Vector2d JSON_XY_ToVector2d(JSONArray v)
    {
        return new Vector2d(v[0].AsDouble, v[1].AsDouble);
    }

    public static Vector2Int JSON_XY_ToVector2Int(JSONArray v)
    {
        return new Vector2Int(v[0].AsInt, v[1].AsInt);
    }

    #endregion

    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private FlatTile flatTilePrefab;
    [SerializeField] private string directory;

    public int numberOfMeshes = 400;
    public int numberOfMeshLoadingJobs = 20;
    public float distanceThreshold;
    public Vector3d XYZOffset { get; private set; } = default;
    public bool DetailControlKeys = false;

    public float nearMatDistance = 100;
    public float pointPhysicalSize = 0.1f; //Round point size
    public ModelData Model { private set; get; } = null;
    public Dictionary<int, Color> ClassColorDictionary
    {
        get => Model.classColor;
        private set { }
    }

    private List<GeoPCNode> visibleTopLevelNodes = new List<GeoPCNode>();

    #region Life cycle

    // Start is called before the first frame update
    void Start()
    {
        distanceThreshold = Camera.main.GetDistanceForLenghtToScreenSize(pointPhysicalSize, 1);

        DirectoryInfo modelDir = new DirectoryInfo(directory);
        FileInfo index = modelDir.GetFiles("pc_model.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);
        Model = new ModelData(json, modelDir);

        int cellCounter = 0;
        foreach (CellData cellData in Model.cells)
        {
            InitCell(cellData, cellCounter++);

            if (flatTilePrefab != null)
            {
                var t = Instantiate(flatTilePrefab.gameObject).GetComponent<FlatTile>();
                t.viewer = this;
                //t.url = cellData.GetOSMTileURL();
                t.url = cellData.GetARCGISWorldImageryTileURL();
                t.cellExtentMin = cellData.extent.Min;
                t.cellExtentMax = cellData.extent.Max;
            }
        }
    }

    #endregion

    #region Init

    private void InitCell(CellData cellData, int nodeNumber)
    {
        FileInfo index = cellData.directoryInfo.GetFiles("cell.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        if (nodeNumber == 0)
        {
            Vector3d min = cellData.extent.Min;
            XYZOffset = new Vector3d(min.x, 0, min.z);
        }
        NodeData nodeData = new NodeData(json, cellData);
        GeoPCNode n = CreateNode(nodeData);
        visibleTopLevelNodes.Add(n);
    }


    public GeoPCNode CreateNode(NodeData nodeData)
    {
        GeoPCNode node = Instantiate(nodePrefab).GetComponent<GeoPCNode>();
        node.name = nodeData.name;
        node.Init(nodeData, this);
        return node;
    }

    #endregion

    #region Colors

    float GetClassCodeForColor(Color color)
    {
        foreach (var entry in Model.classColor)
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

        foreach(GeoPCNode node in visibleTopLevelNodes)
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

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (Model != null && Model.cells != null)
        {
            foreach (var c in Model.cells)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube((Vector3)(c.extent.Center - XYZOffset), (Vector3)c.extent.Size);
            }
        }
    }

    #endregion
}
