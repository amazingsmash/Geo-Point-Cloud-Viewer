using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;

class PointCloudOctreeNode : MonoBehaviour
{

    BoxCollider box;

    public PointCloudOctreeNode(JSONNode node)
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

    public static PointCloudOctreeNode parse(JSONNode node, DirectoryInfo directory){
        if (node["filename"] != ""){
            return new PointCloudOctreeLeafNode(node, directory);
        }else{
            Debug.Log(node);
            return new PointCloudOctreeParentNode(node["children"], directory);
        }
    }
}

class PointCloudOctreeLeafNode : PointCloudOctreeNode
{
    readonly FileInfo fileInfo;
    public PointCloudOctreeLeafNode(JSONNode node, DirectoryInfo directory):
    base(node)
    {
        fileInfo = directory.GetFiles(node["filename"])[0];
        Debug.Assert(fileInfo != null, "File not found:" + node["filename"]);
    }
}

class PointCloudOctreeParentNode: PointCloudOctreeNode
{

    readonly PointCloudOctreeNode[] children;
    public PointCloudOctreeParentNode(JSONNode node, DirectoryInfo directory):
    base(node)
    {
        JSONArray childrenJSON = node["children"].AsArray;
        children = new PointCloudOctreeNode[childrenJSON.Count];
        for (int i = 0; i < childrenJSON.Count; i++){
            children[i] = PointCloudOctreeNode.parse(childrenJSON, directory);
        }
    }
}

public class PointCloudOctree {

    readonly PointCloudOctreeNode[] topNodes = null;
    readonly DirectoryInfo directory;

    public PointCloudOctree(DirectoryInfo directory)
    {
        this.directory = directory;
        FileInfo index = directory.GetFiles("voxelIndex.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        topNodes = new PointCloudOctreeNode[json.AsArray.Count];

        for (int i = 0; i < topNodes.Length; i++){
            topNodes[i] = PointCloudOctreeNode.parse(json.AsArray[i], directory);
        }

    }
    
}


