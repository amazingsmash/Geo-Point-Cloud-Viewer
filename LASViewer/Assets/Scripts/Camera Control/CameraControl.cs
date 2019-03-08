using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    Camera cam = null;
    FlyCam flyCam;

    // Start is called before the first frame update
    void Start()
    {
        cam = gameObject.GetComponent<Camera>();
        flyCam = gameObject.GetComponent<FlyCam>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.X))
        {
            flyCam.enabled = !flyCam.enabled;

            Cursor.lockState = flyCam.enabled ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }

    public void setCameraOrtho(bool isOrtho)
    {
        if (cam != null)
        {
            cam.orthographic = isOrtho;
            cam.orthographicSize = 250;

            if (isOrtho)
            {
                cam.transform.rotation.SetLookRotation(new Vector3(1, 0, 0));
            }
        }

    }

    private void OnDrawGizmos()
    {
        if (cam != null)
        {
            Gizmos.color = Color.cyan;
            float pointPhysicalSize = 0.1f; //Round point size
            float distanceThreshold = Camera.main.GetDistanceForLenghtToScreenSize(pointPhysicalSize, 1);
            Gizmos.DrawWireSphere(cam.transform.position, distanceThreshold);
        }
    }
}
