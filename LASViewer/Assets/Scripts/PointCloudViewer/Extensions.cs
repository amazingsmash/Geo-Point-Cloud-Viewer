using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BoxColliderExtension
{
    public static float sqrDistance(this BoxCollider box, Vector3 position)
    {
        Vector3 p = box.ClosestPoint(position);
        return (position - p).sqrMagnitude;
    }

    public static float distance(this BoxCollider box, Vector3 position)
    {
        Vector3 p = box.ClosestPoint(position);
        return Vector3.Distance(position, p);
    }
}

public static class BoundsExtension
{
    public static float sqrDistance(this Bounds box, Vector3 position)
    {
        Vector3 p = box.ClosestPoint(position);
        if (p == position)
        {
            return 0.0f; //Inside
        }
        return (position - p).sqrMagnitude;
    }

    public static float distance(this Bounds box, Vector3 position)
    {
        Vector3 p = box.ClosestPoint(position);
        return Vector3.Distance(position, p);
    }
}

public static class Vector3Extensions{
    public static float LargestDimension(this Vector3 v){
        return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
    }

    private static bool NearlyEqual(float a, float b)
    {
        float diff = Mathf.Abs(a - b);
        return (diff < 0.000001);//float.Epsilon);
    }

    public static bool NearlyEquals(this Vector3 v, Vector3 v2){
        if (!NearlyEqual(v.x, v2.x)) return false;
        if (!NearlyEqual(v.y, v2.y)) return false;
        if (!NearlyEqual(v.z, v2.z)) return false;
        return true;
    }
}

public static class BoundingSphereExtensions
{
    public static BoundingSphere Transform(this BoundingSphere sphere, Transform transform)
    {
        Vector3 center = transform.TransformPoint(sphere.position);
        float radius = transform.localScale.LargestDimension() * sphere.radius;
        return new BoundingSphere(center, radius);
    }

    public static float DistanceTo(this BoundingSphere sphere, Vector3 position)
    {
        return ((sphere.position - position).magnitude) - sphere.radius;
    }
}

public static class LoDGroupExtensions
{
    public static int ActiveLoD(this LODGroup g)
    {
        LOD[] lods = g.GetLODs();
        for (int i = 0; i < lods.Length; i++)
        {
            if (lods[i].renderers[0].isVisible)
            {
                return i;
            }
        }
        return -1;
    }
}


