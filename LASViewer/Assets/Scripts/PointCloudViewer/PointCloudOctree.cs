using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;

class PointCloudOctreeNode{
    public static Vector3 JSONNode2Vector3(JSONNode node)
    {
        return new Vector3(node[0].AsFloat, node[1].AsFloat, node[2].AsFloat);
    }

    public static PointCloudOctreeNode parse(JSONNode node){
        if (node["filename"] != null){
            return new PointCloudOctreeLeafNode(node);
        }else{
            return new PointCloudOctreeParentNode(node["children"].AsArray);
        }
    }
}

class PointCloudOctreeLeafNode : PointCloudOctreeNode
{

    string fileName;
    Vector3 min;
    Vector3 max;
    public PointCloudOctreeLeafNode(JSONNode node)
    {
        fileName = node["filename"];
        min = JSONNode2Vector3(node["min"]);
        max = JSONNode2Vector3(node["max"]);
    }
}

class PointCloudOctreeParentNode: PointCloudOctreeNode
{

    PointCloudOctreeNode[] children;


    public PointCloudOctreeParentNode(JSONArray childrenJSON)
    {
        children = new PointCloudOctreeNode[childrenJSON.Count];
        for (int i = 0; i < childrenJSON.Count; i++){
            children[i] = PointCloudOctreeNode.parse(childrenJSON);
        }
    }
}

public class PointCloudOctree {

    PointCloudOctreeNode[] topNodes = null;

    public PointCloudOctree(string indexFilePath){

        StreamReader reader = new StreamReader(indexFilePath);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        topNodes = new PointCloudOctreeNode[json.AsArray.Count];

        foreach (JSONNode node in json.AsArray)
        {

        }

    }
}


