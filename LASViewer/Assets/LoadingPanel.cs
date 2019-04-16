using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadingPanel : MonoBehaviour
{
    public PointCloudViewer pcv;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (pcv.NLoadingNodes == 0 && Time.time > 2)
        {
            Hide();
        }
    }

    void Hide()
    {
        GetComponent<CanvasGroup>().alpha = 0f; //this makes everything transparent
        GetComponent<CanvasGroup>().blocksRaycasts = false; //this prevents the UI element to receive input events
    }
}
