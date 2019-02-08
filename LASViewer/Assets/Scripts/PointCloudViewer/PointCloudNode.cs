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

    //protected Bounds bounds;
    protected BoundingSphere boundingSphere;

    static public int nVisibleNodes = 0;
    static public int nInvisibleNodes = 0;

    protected PointCloudNode[] children = null;

    private PCNodeState _state = PCNodeState.INVISIBLE;
    public PCNodeState State
    {
        get { return _state; }
        set
        {
            _state = value;
            if (value == PCNodeState.VISIBLE)
            {
                nVisibleNodes++;
            }
            else
            {
                nInvisibleNodes++;
            }
        }
    }


    public abstract Bounds GetBoundsInWorldSpace();

    public abstract void GetClosestPointOnRay(Ray ray,
                                     Vector2 screenPos,
                                     ref float maxDist,
                                     ref Vector3 closestHit,
                                              float sqrMaxScreenDistance);

    public bool closerThan(Vector3 position, float minDistance)
    {
        BoundingSphere boundingSphereWorldSpace = boundingSphere.Transform(gameObject.transform);
        float distSphere = boundingSphereWorldSpace.DistanceTo(position);
        if (distSphere <= minDistance) return true;
        return false;
    }

    public void testRenderState(PCNodeState parentState,
                                Vector3 camPosWorldSpace,
                                float sqrVisibleDistance)
    {
        if (parentState == PCNodeState.INVISIBLE)
        {
            State = PCNodeState.INVISIBLE;
        }
        else
        {
            float maxDist = Mathf.Sqrt(sqrVisibleDistance);
            bool close = closerThan(camPosWorldSpace, maxDist);
            State = close ? PCNodeState.VISIBLE : PCNodeState.INVISIBLE;
        }

        if (children != null)
        {
            foreach (PointCloudNode node in children)
            {
                node.testRenderState(State, camPosWorldSpace, sqrVisibleDistance);
            }
        }
    }

    public void checkVisibility(PCNodeState parentState,
                        Vector3 cameraInObjSpacePosition,
                        float sqrVisibleDistance)
    {
        if (parentState == PCNodeState.INVISIBLE)
        {
            State = PCNodeState.INVISIBLE;
        }
        else
        {
            float maxDist = Mathf.Sqrt(sqrVisibleDistance);
            bool close = closerThan(cameraInObjSpacePosition, maxDist);
            State = close ? PCNodeState.VISIBLE : PCNodeState.INVISIBLE;
        }

        foreach (PointCloudNode node in children)
        {
            node.testRenderState(State, cameraInObjSpacePosition, sqrVisibleDistance);
        }
    }

    //--------------------------------

    public void InitializeFromJSON(JSONNode node)
    {
        Vector3 min = JSONNode2Vector3(node["min"]);
        Vector3 max = JSONNode2Vector3(node["max"]);
        Vector3 center = (min + max) / 2.0f;
        Vector3 size = max - min;
        boundingSphere = new BoundingSphere(center, size.magnitude);
    }

    public static Vector3 JSONNode2Vector3(JSONNode node)
    {
        return new Vector3(node[0].AsFloat, node[1].AsFloat, node[2].AsFloat);
    }

    public static bool isLeaf(JSONNode node)
    {
        return node["children"].AsArray.Count == 0 && !node["filename"].Equals("");
    }

    public static PointCloudNode addNode(JSONNode node, DirectoryInfo directory, GameObject gameObject, IPointCloudManager materialProvider)
    {
        GameObject child = new GameObject("PC Node");
        child.transform.SetParent(gameObject.transform, false);

        if (isLeaf(node))
        {
            PointCloudLeafNode leaf = child.AddComponent<PointCloudLeafNode>();
            leaf.init(node, directory, materialProvider);
            return leaf;
        }
        else
        {
            PointCloudParentNode parent = child.AddComponent<PointCloudParentNode>();
            parent.Initialize(node, directory, materialProvider);
            return parent;
        }
    }
}



class PointCloudParentNode : PointCloudNode
{

    public override Bounds GetBoundsInWorldSpace()
    {
        Bounds b = new Bounds();
        foreach (PointCloudNode node in children)
        {
            if (node.State == PCNodeState.VISIBLE)
            {
                Bounds cb = node.GetBoundsInWorldSpace();
                b.Encapsulate(cb);
            }
        }
        return b;
    }

    public void Initialize(JSONNode node, DirectoryInfo directory, IPointCloudManager materialProvider)
    {
        InitializeFromJSON(node);

        JSONArray childrenJSON = node["children"].AsArray;
        children = new PointCloudNode[childrenJSON.Count];
        if (children != null)
        {
            for (int i = 0; i < childrenJSON.Count; i++)
            {
                children[i] = PointCloudNode.addNode(childrenJSON[i], directory, gameObject, materialProvider);
            }
        }
    }

    public override void GetClosestPointOnRay(Ray ray,
                             Vector2 screenPos,
                             ref float maxDist,
                             ref Vector3 closestHit,
                                      float sqrMaxScreenDistance)
    {

        Bounds bounds = GetBoundsInWorldSpace();
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
