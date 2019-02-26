using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemoteServerPointCloudListener : MonoBehaviour, IPointCloudListener, ITCPEndListener
{
    TCPServer server = null;

    public int port = 3333;

    void Start()
    {
        server = new TCPServer("127.0.0.1", port, this);
        Debug.Log("Server generated.");
    }

    public void OnMessageReceived(string msg)
    {
        Debug.Log(msg);
    }

    public void onPointSelected(Vector3 point, float classCode)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = point;
        float ballSize = 2.0f;
        sphere.transform.localScale = new Vector3(ballSize, ballSize, ballSize);

        SelectPointCmd selectPointCmd = new SelectPointCmd(0, 0, 0);

        server.SendMessage("Point Selected: " + point.ToString() + " of class: " + classCode);
    }

    public void OnStatusChanged(TCPEnd.Status status)
    {

    }

    public void OnStatusMessage(string msg)
    {
        Debug.Log(msg);
    }
}
