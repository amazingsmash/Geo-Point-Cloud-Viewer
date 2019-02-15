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
    protected Bounds boundsInModelSpace;
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

    public float EstimatedDistance(Vector3 position)
    {

        Bounds bounds = boundsInModelSpace;
        if (bounds.Contains(position))
        {
            return 0.0f;
        }
        Vector3 p = bounds.ClosestPoint(position);
        return Vector3.Distance(p, position);
    }

    public abstract void GetClosestPointOnRay(Ray ray,
                                     Vector2 screenPos,
                                     ref float maxDist,
                                     ref Vector3 closestHit,
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
        child.transform.SetParent(gameObject.transform, false);

        if (JSONOfLeaf(node))
        {
            PointCloudLeafNode leaf = child.AddComponent<PointCloudLeafNode>();
            leaf.Initialize(node, directory, materialProvider);
            return leaf;
        }
        else
        {
            PointCloudParentNode parent = child.AddComponent<PointCloudParentNode>();
            parent.Initialize(node, directory, materialProvider);
            return parent;
        }
    }
    #endregion
}

class PointCloudParentNode : PointCloudNode
{
    public void Initialize(JSONNode node, DirectoryInfo directory, IPointCloudManager materialProvider)
    {
        InitializeFromJSON(node);

        JSONArray childrenJSON = node["children"].AsArray;
        children = new PointCloudNode[childrenJSON.Count];
        if (children != null)
        {
            for (int i = 0; i < childrenJSON.Count; i++)
            {
                children[i] = PointCloudNode.AddNode(childrenJSON[i], directory, gameObject, materialProvider);
            }
        }
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
                foreach (PointCloudNode node in children)
                {
                    node.State = PCNodeState.INVISIBLE;
                }
                break;
            case PCNodeState.VISIBLE:
                break;
        }
    }

    public override void GetClosestPointOnRay(Ray ray,
                             Vector2 screenPos,
                             ref float maxDist,
                             ref Vector3 closestHit,
                                      float sqrMaxScreenDistance)
    {
        if (State == PCNodeState.INVISIBLE)
        {
            return;
        }


        Bounds bounds = boundsInModelSpace;  //GetBoundsInWorldSpace();
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
                                              sqrMaxScreenDistance);
                }
            }
        }
    }
}
