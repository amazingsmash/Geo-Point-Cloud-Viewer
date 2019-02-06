using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;
using UnityEditor;

class PointCloudNode : MonoBehaviour
{
    public enum PCNodeRenderState
    {
        VISIBLE,
        INVISIBLE
    };

    protected Bounds box;
    protected PCNodeRenderState state = PCNodeRenderState.INVISIBLE;
    protected float distanceToCameraUpperBound = float.PositiveInfinity;

    public void initBounds(JSONNode node)
    {
        Vector3 min = JSONNode2Vector3(node["min"]);
        Vector3 max = JSONNode2Vector3(node["max"]);
        Vector3 center = (min + max) / 2.0f;
        Vector3 size = max - min;
        box = new Bounds(center, size);
        //Debug.Log(box);
    }

    public void testRenderState(PCNodeRenderState parentState, 
                                Vector3 cameraInObjSpacePosition, 
                                float sqrVisibleDistance)
    {
        //Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        if (parentState == PCNodeRenderState.INVISIBLE)
        {
            state = PCNodeRenderState.INVISIBLE;
        }
        else{
            //float sqrDist = box.sqrDistance(cameraInObjSpacePosition);
            //state = (sqrDist <= sqrVisibleDistance) ? PCNodeRenderState.VISIBLE : PCNodeRenderState.INVISIBLE;

            float dist = box.distance(cameraInObjSpacePosition);
            float maxDist = Mathf.Sqrt(sqrVisibleDistance);
            state = (dist <= maxDist) ? PCNodeRenderState.VISIBLE : PCNodeRenderState.INVISIBLE;
        }

        for (int i = 0; i < transform.childCount; i++){
            GameObject child = transform.GetChild(i).gameObject;
            PointCloudNode node = child.GetComponent<PointCloudNode>();
            node.testRenderState(state, cameraInObjSpacePosition, sqrVisibleDistance);
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

    public void init(JSONNode node, DirectoryInfo directory, IPointCloudManager materialProvider)
    {
        initBounds(node);

        JSONArray childrenJSON = node["children"].AsArray;
        for (int i = 0; i < childrenJSON.Count; i++)
        {
            PointCloudNode.addNode(childrenJSON[i], directory, gameObject, materialProvider);
        }
    }
}

public partial class PointCloudOctree : MonoBehaviour, IPointCloudManager
{
    DirectoryInfo directory = null;
    float secondsSinceLastVisibilityCheck = 0;

    DirectoryInfo getModelDirectory()
    {
        return new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/LAS MODEL");
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

        foreach (JSONNode node in json.AsArray)
        {
            PointCloudNode.addNode(node, this.directory, gameObject, this);
        }
    }

    
    void Update()
    {
        checkNodeRenderState();
    }

    private void checkNodeRenderState()
    {
        secondsSinceLastVisibilityCheck += Time.deltaTime;
        if (secondsSinceLastVisibilityCheck > 1.0f)
        {
            float sqrVisibleDistance = (float)(Camera.main.farClipPlane * 1.2);
            sqrVisibleDistance = sqrVisibleDistance * sqrVisibleDistance;

            Vector3 cameraInObjSpacePosition = gameObject.transform.TransformPoint(Camera.main.transform.position);
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                PointCloudNode node = child.GetComponent<PointCloudNode>();
                if (node != null)
                {
                    node.testRenderState(PointCloudNode.PCNodeRenderState.VISIBLE, cameraInObjSpacePosition, sqrVisibleDistance);
                }
            }

            secondsSinceLastVisibilityCheck = 0.0f;
        }
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