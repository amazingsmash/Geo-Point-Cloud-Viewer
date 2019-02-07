using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;
using UnityEditor;

abstract class PointCloudNode : MonoBehaviour
{
    public enum PCNodeRenderState
    {
        VISIBLE,
        INVISIBLE
    };

    //protected Bounds bounds;
    protected BoundingSphere boundingSphere;

    static public int nVisibleNodes = 0;
    static public int nInvisibleNodes = 0;

    private PCNodeRenderState _state = PCNodeRenderState.INVISIBLE;
    protected PCNodeRenderState State
    {
        get { return _state; }
        set
        {
            _state = value;
            if (value == PCNodeRenderState.VISIBLE)
            {
                nVisibleNodes++;
            }
            else
            {
                nInvisibleNodes++;
            }
        }
    }

    protected PointCloudNode[] ChildNodes{
        get
        {
            PointCloudNode[] nodes = new PointCloudNode[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                nodes[i] = child.GetComponent<PointCloudNode>();
            }
            return nodes;
        }
    }

    public abstract Bounds getBoundsInWorldCoordinates();

    public abstract void GetClosestPointOnRay(Ray ray,
                                     Vector2 screenPos,
                                     ref float maxDist,
                                     ref Vector3 closestHit,
                                              float sqrMaxScreenDistance);

    public void initBounds(JSONNode node)
    {
        Vector3 min = JSONNode2Vector3(node["min"]);
        Vector3 max = JSONNode2Vector3(node["max"]);
        Vector3 center = (min + max) / 2.0f;
        Vector3 size = max - min;
        //bounds = new Bounds(center, size);
        //Debug.Log(box);

        boundingSphere = new BoundingSphere(center, size.magnitude);
    }
    public bool closerThan(Vector3 position, float minDistance)
    {
        //TODO: Bounding volumes must resize with cloud transformations
        float distSphere = ((boundingSphere.position - position).magnitude) - boundingSphere.radius;
        if (distSphere <= minDistance) return true;

        //TODO: Check with box
        return false;
    }

    public void testRenderState(PCNodeRenderState parentState,
                                Vector3 cameraInObjSpacePosition,
                                float sqrVisibleDistance)
    {
        //if (this is PointCloudLeafNode){
        //    Debug.Log("Leaf Node");
        //}

        //Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        if (parentState == PCNodeRenderState.INVISIBLE)
        {
            State = PCNodeRenderState.INVISIBLE;
        }
        else
        {
            //float sqrDist = box.sqrDistance(cameraInObjSpacePosition);
            //state = (sqrDist <= sqrVisibleDistance) ? PCNodeRenderState.VISIBLE : PCNodeRenderState.INVISIBLE;

            //float dist = bounds.distance(cameraInObjSpacePosition);
            //float maxDist = Mathf.Sqrt(sqrVisibleDistance);
            //state = (dist <= maxDist) ? PCNodeRenderState.VISIBLE : PCNodeRenderState.INVISIBLE;

            float maxDist = Mathf.Sqrt(sqrVisibleDistance);
            bool close = closerThan(cameraInObjSpacePosition, maxDist);
            State = close ? PCNodeRenderState.VISIBLE : PCNodeRenderState.INVISIBLE;
        }

        PointCloudNode[] children = ChildNodes;
        foreach (PointCloudNode node in children)
        {
            node.testRenderState(State, cameraInObjSpacePosition, sqrVisibleDistance);
        }
    }

    public static Vector3 JSONNode2Vector3(JSONNode node)
    {
        return new Vector3(node[0].AsFloat, node[1].AsFloat, node[2].AsFloat);
    }

    public static bool isLeaf(JSONNode node)
    {
        return node["children"].AsArray.Count == 0 && !node["filename"].Equals("");
    }

    public static void addNode(JSONNode node, DirectoryInfo directory, GameObject gameObject, IPointCloudManager materialProvider)
    {
        GameObject child = new GameObject("PC Node");
        child.transform.SetParent(gameObject.transform, false);

        if (isLeaf(node))
        {
            PointCloudLeafNode leaf = child.AddComponent<PointCloudLeafNode>();
            leaf.init(node, directory, materialProvider);
        }
        else
        {
            PointCloudParentNode parent = child.AddComponent<PointCloudParentNode>();
            parent.init(node, directory, materialProvider);
        }
    }
}



class PointCloudParentNode : PointCloudNode
{

    public override Bounds getBoundsInWorldCoordinates(){
        Bounds b = new Bounds(transform.position, Vector3.zero);
        PointCloudNode[] children = ChildNodes;
        foreach (PointCloudNode node in children)
        {
            Bounds cb = node.getBoundsInWorldCoordinates();
            if (cb.size.sqrMagnitude > 0)
            {
                cb.Encapsulate(cb);
            }
        }
        return b;
    }

    public void init(JSONNode node, DirectoryInfo directory, IPointCloudManager materialProvider)
    {
        initBounds(node);

        JSONArray childrenJSON = node["children"].AsArray;
        for (int i = 0; i < childrenJSON.Count; i++)
        {
            PointCloudNode.addNode(childrenJSON[i], directory, gameObject, materialProvider);
        }
    }

    public override void GetClosestPointOnRay(Ray ray,
                             Vector2 screenPos,
                             ref float maxDist,
                             ref Vector3 closestHit,
                                      float sqrMaxScreenDistance)
    {

    PointCloudNode[] children = ChildNodes;
        foreach (PointCloudNode node in children)
        {
            node.GetClosestPointOnRay(ray,
                                      screenPos,
                                      ref maxDist,
                                      ref closestHit,
                                      sqrMaxScreenDistance);
        }
    }
}

public partial class PointCloudOctree : MonoBehaviour, IPointCloudManager
{
    DirectoryInfo directory = null;
    float secondsSinceLastVisibilityCheck = 0;

    DirectoryInfo getModelDirectory()
    {
        return new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/MINI OCTREE");
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
        DirectoryInfo dir = getModelDirectory();
        init(dir);
    }

    public void init(DirectoryInfo directory)
    {
        this.directory = directory;
        FileInfo index = directory.GetFiles("voxelIndex.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        if (json.IsArray)
        {
            foreach (JSONNode node in json.AsArray)
            {
                PointCloudNode.addNode(node, this.directory, gameObject, this);
            }
        }
        else
        {
            PointCloudNode.addNode(json, this.directory, gameObject, this);
        }
    }


    void Update()
    {
        checkNodeRenderState();
        selectPoint();
    }

    private void checkNodeRenderState()
    {
        secondsSinceLastVisibilityCheck += Time.deltaTime;
        if (secondsSinceLastVisibilityCheck > 0.25f)
        {
            float sqrVisibleDistance = (float)(Camera.main.farClipPlane * 1.2);
            sqrVisibleDistance = sqrVisibleDistance * sqrVisibleDistance;

            Vector3 camPos = Camera.main.transform.position;
            Vector3 cameraInObjSpacePosition = gameObject.transform.TransformPoint(camPos);
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                PointCloudNode node = child.GetComponent<PointCloudNode>();
                if (node != null)
                {
                    node.testRenderState(PointCloudNode.PCNodeRenderState.VISIBLE, 
                                         cameraInObjSpacePosition, 
                                         sqrVisibleDistance);
                }
            }

            //Debug.Log("N Vis-Inv Nodes" + PointCloudNode.nVisibleNodes + " " + PointCloudNode.nInvisibleNodes);
            PointCloudNode.nVisibleNodes = 0;
            PointCloudNode.nInvisibleNodes = 0;

            secondsSinceLastVisibilityCheck = 0.0f;
        }
    }


    void selectPoint()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Finding selected point.");

            float maxScreenDistance = 20.0f;

            Vector3 mousePosition = Input.mousePosition;
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
                                              mousePosition,
                                              ref maxDist, 
                                              ref closestHit, 
                                              maxScreenDistance * maxScreenDistance);
                }
            }

            if (!closestHit.Equals(Vector3.negativeInfinity))
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = closestHit;
                sphere.transform.localScale = new Vector3(4.0f, 4.0f, 4.0f);
            }

        }
    }

    private void OnDrawGizmos()
    {
        Bounds b = new Bounds(Vector3.zero, new Vector3(1000.0f, 1000.0f, 1000.0f));
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}

//IPointCloudManager
public partial class PointCloudOctree : MonoBehaviour, IPointCloudManager
{
    public float hdMaxDistance = 5000.0f;
    public Material hdMaterial = null;
    public Material ldMaterial = null;

    Material IPointCloudManager.getMaterialForDistance(float distance)
    {
        return (distance < hdMaxDistance) ? hdMaterial : ldMaterial;
    }

    Material IPointCloudManager.getMaterialForBoundingBox(Bounds box)
    {
        Vector3 camPos = Camera.main.transform.position;
        if (box.Contains(camPos))
        {
            return hdMaterial;
        }
        else
        {
            Vector3 p = box.ClosestPoint(camPos);
            float sqrDist = (p - camPos).sqrMagnitude;
            return (sqrDist < (hdMaxDistance * hdMaxDistance)) ? hdMaterial : ldMaterial;
        }
    }

    static Dictionary<float, Color> classColor = null;
    Color IPointCloudManager.getColorForClass(float classification)
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
}