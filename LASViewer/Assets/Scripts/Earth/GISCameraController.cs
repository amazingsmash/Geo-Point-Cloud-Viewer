using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GISCameraController : MonoBehaviour
{
    public GameObject earth;
    FlyCam flyCamController;
    public PointCloud pointCloud;
    // Start is called before the first frame update
    void Start()
    {
        flyCamController = GetComponent<FlyCam>();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        double earthRadius = earth.transform.localScale.x / 2;
        Vector3 camPos = gameObject.transform.position;
        Vector3 earthPos = earth.gameObject.transform.position;
        double camHeight = (camPos - earthPos).magnitude - earthRadius;

        flyCamController.shiftAdd = (float)(camHeight) / 50;
        flyCamController.maxShift = flyCamController.shiftAdd;

        //pointCloud.enabled = camHeight < 10000;

    }
}
