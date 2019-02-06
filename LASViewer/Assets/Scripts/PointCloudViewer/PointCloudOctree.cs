using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;
using UnityEditor;

class PointCloudOctreeNode : MonoBehaviour
{

    BoxCollider box;

    //public PointCloudOctreeNode(JSONNode node)
    //{
    //    Vector3 min = JSONNode2Vector3(node["min"]);
    //    Vector3 max = JSONNode2Vector3(node["max"]);

    //    gameObject.AddComponent<BoxCollider>();
    //    box = gameObject.GetComponent<BoxCollider>();
    //    box.center = (min + max) / 2.0f;
    //    box.size = max - min;
    //}

    public void initBoxCollider(JSONNode node){
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

    public static bool isLeaf(JSONNode node){
        return node["children"].AsArray.Count == 0 && !node["filename"].Equals("");
    }

    //public static PointCloudOctreeNode parse(JSONNode node, DirectoryInfo directory){
    //    if (node["filename"] != ""){
    //        return new PointCloudOctreeLeafNode(node, directory);
    //    }else{
    //        Debug.Log(node);
    //        return new PointCloudOctreeParentNode(node["children"], directory);
    //    }
    //}

    public static void addNode(JSONNode node, DirectoryInfo directory, GameObject gameObject, IMaterialProvider materialProvider)
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

class PointCloudOctreeLeafNode : PointCloudOctreeNode
{
    FileInfo fileInfo;
    //public PointCloudOctreeLeafNode(JSONNode node, DirectoryInfo directory):
    //base(node)
    //{
    //    fileInfo = directory.GetFiles(node["filename"])[0];
    //    Debug.Assert(fileInfo != null, "File not found:" + node["filename"]);
    //}

    public void init(JSONNode node, DirectoryInfo directory, IMaterialProvider materialProvider)
    {
        string filename = node["filename"];
        fileInfo = directory.GetFiles(filename)[0];
        Debug.Assert(fileInfo != null, "File not found:" + node["filename"]);
        initBoxCollider(node);

        PointCloudPart pc = gameObject.AddComponent<PointCloudPart>();
        pc.initWithFilePath(fileInfo.FullName, materialProvider);
    }
}

class PointCloudOctreeParentNode: PointCloudOctreeNode
{

    //readonly PointCloudOctreeNode[] children;
    //public PointCloudOctreeParentNode(JSONNode node, DirectoryInfo directory):
    //base(node)
    //{
    //    JSONArray childrenJSON = node["children"].AsArray;
    //    children = new PointCloudOctreeNode[childrenJSON.Count];
    //    for (int i = 0; i < childrenJSON.Count; i++){
    //        children[i] = PointCloudOctreeNode.parse(childrenJSON, directory);
    //    }
    //}

    public void init(JSONNode node, DirectoryInfo directory, IMaterialProvider materialProvider)
    {
        JSONArray childrenJSON = node["children"].AsArray;
        for (int i = 0; i < childrenJSON.Count; i++)
        {
            PointCloudOctreeNode.addNode(childrenJSON[i], directory, gameObject, materialProvider);
        }
    }
}

public class PointCloudOctree : MonoBehaviour, IMaterialProvider
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
        return new DirectoryInfo("../Models/7");
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

    //------------------------------------------------------------

    public float hdMaxDistance = 5000.0f;
    public Material hdMaterial = null;
    public Material ldMaterial = null;

    Material IMaterialProvider.getMaterialForDistance(float distance)
    {
        return (distance < hdMaxDistance) ? hdMaterial : ldMaterial;
    }

    Material IMaterialProvider.getMaterialForBoundingBox(BoxCollider box)
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
}



