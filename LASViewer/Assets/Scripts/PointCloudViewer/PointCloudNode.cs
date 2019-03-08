using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;
using System.IO;

abstract class PointCloudNode : MonoBehaviour
{
    public enum PCNodeState
    {
        VISIBLE,
        INVISIBLE
    };

    protected BoundingSphere boundingSphere;
    public Bounds boundsInModelSpace { get; private set; }
    protected PointCloudNode[] children = null;

    private PCNodeState _state = PCNodeState.INVISIBLE;
    public PCNodeState State
    {
        get { return _state; }
        set
        {
            if (_state != value)
            {
                _state = value;
                OnStateChanged();
            }
        }
    }

    public abstract void ComputeNodeState(ref List<PointCloudLeafNode.NodeAndDistance> visibleLeafNodesAndDistances, Vector3 camPosition, float zFar);
    public abstract void OnStateChanged();
    public abstract bool Initialize(JSONNode node, DirectoryInfo directory, IPointCloudManager manager);

    public float EstimatedDistance(Vector3 position)
    {
        return boundsInModelSpace.MinDistance(position);
    }

    public abstract void GetClosestPointOnRay(Ray ray,
                                     Vector2 screenPos,
                                     ref float maxDist,
                                     ref Vector3 closestHit,
                                     ref Color colorClosestHit,
                                     float sqrMaxScreenDistance);

    //--------------------------------

    #region JSON Initialization

    public void InitializeFromJSON(JSONNode node)
    {
        Vector3 min = JSONNode2Vector3(node["min"]);
        Vector3 max = JSONNode2Vector3(node["max"]);
        Vector3 center = (min + max) / 2.0f;
        Vector3 size = max - min;
        boundingSphere = new BoundingSphere(center, size.magnitude);

        boundsInModelSpace = new Bounds(center, size);
    }

    public static Vector3 JSONNode2Vector3(JSONNode node)
    {
        return new Vector3(node[0].AsFloat, node[2].AsFloat, node[1].AsFloat);
    }

    public static bool JSONOfLeaf(JSONNode node)
    {
        return node["children"].AsArray.Count == 0 && !node["filename"].Equals("");
    }

    public static PointCloudNode AddNode(JSONNode node, DirectoryInfo directory, GameObject gameObject, IPointCloudManager materialProvider)
    {
        GameObject child = new GameObject("PC Node");
        child.isStatic = true;


        PointCloudNode pcNode = null;
        if (JSONOfLeaf(node))
        {
            pcNode = child.AddComponent<PointCloudLeafNode>();
        }
        else
        {
            pcNode = child.AddComponent<PointCloudParentNode>();
        }

        if (pcNode.Initialize(node, directory, materialProvider))
        {
            child.transform.SetParent(gameObject.transform, false);
            return pcNode;
        }
        else
        {
            Destroy(child);
            return null;
        }

    }
    #endregion
}

class PointCloudParentNode : PointCloudNode
{
    public override bool Initialize(JSONNode node, DirectoryInfo directory, IPointCloudManager manager)
    {
        InitializeFromJSON(node);
        gameObject.name = "PC Parent Node";

        JSONArray childrenJSON = node["children"].AsArray;
        ArrayList childrenList = new ArrayList();
        //Debug.Log("N Children: " + childrenJSON.Count);
        for (int i = 0; i < childrenJSON.Count; i++)
        {
            PointCloudNode pcNode = PointCloudNode.AddNode(childrenJSON[i], directory, gameObject, manager);
            if (pcNode != null)
            {
                childrenList.Add(pcNode);
            }
        }
        children = (PointCloudNode[])childrenList.ToArray(typeof(PointCloudNode));

        return childrenJSON.Count > 0;
    }

    public override void ComputeNodeState(ref List<PointCloudLeafNode.NodeAndDistance> visibleLeafNodesAndDistances, Vector3 camPosition, float zFar)
    {
        float dist = EstimatedDistance(camPosition);
        State = (dist <= zFar) ? PCNodeState.VISIBLE : PCNodeState.INVISIBLE;

        if (State == PCNodeState.VISIBLE)
        {
            foreach (PointCloudNode node in children)
            {
                node.ComputeNodeState(ref visibleLeafNodesAndDistances, camPosition, zFar);
            }
        }
    }

    public override void OnStateChanged()
    {
        switch (State)
        {
            case PCNodeState.INVISIBLE:
                gameObject.SetActive(false);
                foreach (PointCloudNode node in children)
                {
                    node.State = PCNodeState.INVISIBLE;
                }
                break;
            case PCNodeState.VISIBLE:
                gameObject.SetActive(true);
                break;
        }
    }

    public override void GetClosestPointOnRay(Ray ray,
                             Vector2 screenPos,
                             ref float maxDist,
                             ref Vector3 closestHit,
                             ref Color colorClosestHit,
                                      float sqrMaxScreenDistance)
    {
        if (State == PCNodeState.INVISIBLE)
        {
            return;
        }

        Bounds bounds = boundsInModelSpace;
        if (bounds.Contains(ray.origin) || bounds.IntersectRay(ray))
        {
            if (children != null)
            {
                foreach (PointCloudNode node in children)
                {
                    node.GetClosestPointOnRay(ray,
                                              screenPos,
                                              ref maxDist,
                                              ref closestHit,
                                              ref colorClosestHit,
                                              sqrMaxScreenDistance);
                }
            }
        }
    }
}
