using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;
using UnityEditor;

class PointCloudOctreeNode : MonoBehaviour
{

    protected BoxCollider box;

    public void initBoxCollider(JSONNode node)
    {
        Vector3 min = JSONNode2Vector3(node["min"]);
        Vector3 max = JSONNode2Vector3(node["max"]);

        gameObject.AddComponent<BoxCollider>();
        box = gameObject.GetComponent<BoxCollider>();
        box.center = (min + max) / 2.0f;
        box.size = max - min;
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
            PointCloudOctreeLeafNode leaf = child.AddComponent<PointCloudOctreeLeafNode>();
            leaf.init(node, directory, materialProvider);
        }
        else
        {
            PointCloudOctreeParentNode parent = child.AddComponent<PointCloudOctreeParentNode>();
            parent.init(node, directory, materialProvider);
        }
    }
}



class PointCloudOctreeParentNode : PointCloudOctreeNode
{

    public void init(JSONNode node, DirectoryInfo directory, IPointCloudManager materialProvider)
    {
        JSONArray childrenJSON = node["children"].AsArray;
        for (int i = 0; i < childrenJSON.Count; i++)
        {
            PointCloudOctreeNode.addNode(childrenJSON[i], directory, gameObject, materialProvider);
        }
    }
}

public partial class PointCloudOctree : MonoBehaviour, IPointCloudManager
{
    DirectoryInfo directory = null;

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
            PointCloudOctreeNode.addNode(node, this.directory, gameObject, this);
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

    Material IPointCloudManager.getMaterialForBoundingBox(BoxCollider box)
    {
        Vector3 camPos = Camera.main.transform.position;
        if (box.bounds.Contains(camPos))
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