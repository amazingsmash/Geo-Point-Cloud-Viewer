using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointHighlighter : MonoBehaviour
{
    public GeoPCViewer viewer;
    public GameObject pointer;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition = Input.mousePosition;
            bool hit = viewer.SelectPoint(mousePosition, 10.0f, out Vector3 p, out float pointClass);
            if (hit)
            {
                var go = Instantiate(pointer);
                go.transform.position = p;
            }
        }
    }
}
