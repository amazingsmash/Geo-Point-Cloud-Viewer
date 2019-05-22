using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GISCameraController : MonoBehaviour
{
    public GameObject earth;
    FlyCam flyCamController;
    public PointCloud pointCloud;
    private Camera mainCamera;
    // Start is called before the first frame update
    void Start()
    {
        flyCamController = GetComponent<FlyCam>();
        mainCamera = GetComponent<Camera>();

        pointCloud.transform.up = pointCloud.transform.position - earth.gameObject.transform.position;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        double earthRadius = earth.transform.localScale.x / 2;
        Vector3 camPos = gameObject.transform.position;
        Vector3 earthPos = earth.gameObject.transform.position;
        double distanceToEarthCenter = (camPos - earthPos).magnitude;
        double camHeight = distanceToEarthCenter - earthRadius;


        double distanceToPointCloud = (camPos - pointCloud.transform.position).magnitude;
        flyCamController.shiftAdd = (float)(camHeight) / 50 + flyCamController.speed;
        flyCamController.maxShift = flyCamController.shiftAdd;

        //pointCloud.enabled = camHeight < 10000;

        //mainCamera.nearClipPlane = (float)(camHeight * 0.0001);
        mainCamera.nearClipPlane = 0.03f;

        //Distance to horizon (sphere)
        float distanceToHorizon = Mathf.Sqrt((float)(distanceToEarthCenter * distanceToEarthCenter - earthRadius * earthRadius));
        mainCamera.farClipPlane = distanceToHorizon;

        Debug.Log("Camera Height: " + camHeight);
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.B))
        {
            gameObject.transform.position = pointCloud.gameObject.transform.position;
        }
    }
}
