using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    void Update()
    {
        Vector3 pos = gameObject.transform.position;
        Vector3 camPos = Camera.main.transform.position;
        Vector3 lookAt = pos + (pos - camPos);
        transform.LookAt(lookAt, Vector3.up);
        transform.Rotate(270, 0, 0, Space.Self);
        //transform.Rotate(0, 90, 0);
    }
}
