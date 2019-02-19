using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointCloudListener : IPointCloudListener, ITCPListener
{
    readonly TCPServer server;

    public PointCloudListener()
    {
        server = new TCPServer(this);
    }

    public void OnMessageReceived(string msg)
    {
        Debug.Log(msg);
    }

    public void onPointSelected(Vector3 point)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = point;
        float ballSize = 2.0f;
        sphere.transform.localScale = new Vector3(ballSize, ballSize, ballSize);

        server.SendMessage("Point Selected: " + point.ToString());
    }

    public void OnStatusMessage(string msg)
    {
        Debug.Log(msg);
    }
}
