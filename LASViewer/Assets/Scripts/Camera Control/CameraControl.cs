using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraControl : MonoBehaviour
{
    Camera cam = null;
    FlyCam flyCam;
    public Text label = null;

    private string flyMsg = "Press AWSD keys to Fly, Hold Shift for Speed Up. Press X to show the cursor.";
    private string selectMsg = "Select points with the cursor. Press X to Fly.";

    private void Awake()
    {
        Application.targetFrameRate = 60;
    }

    // Start is called before the first frame update
    void Start()
    {
        cam = gameObject.GetComponent<Camera>();
        flyCam = gameObject.GetComponent<FlyCam>();
        Cursor.visible = false;
        ShowText(flyMsg);
    }

    void ShowText(string msg)
    {
        if (label)
        {
            label.text = msg;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.X))
        {
            flyCam.enabled = !flyCam.enabled;
            Cursor.lockState = flyCam.enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !flyCam.enabled;
            ShowText(flyCam.enabled ? flyMsg : selectMsg);
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

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(cam.transform.position, cam.farClipPlane);
        }
    }
}
