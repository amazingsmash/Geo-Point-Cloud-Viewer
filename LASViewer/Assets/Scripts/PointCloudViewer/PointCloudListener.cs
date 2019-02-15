using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointCloudListener : IPointCloudListener
{
    public void onPointSelected(Vector3 point)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = point;
        sphere.transform.localScale = new Vector3(4.0f, 4.0f, 4.0f);
    }
}
