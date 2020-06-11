using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class FlatTile : MonoBehaviour
{
    public GeoPCViewer viewer;
    public Vector3d cellExtentMin;
    public Vector3d cellExtentMax;
    public string url;

    // Start is called before the first frame update
    IEnumerator Start()
    {
        Vector3 center = (Vector3)(((cellExtentMin + cellExtentMax) / 2) - viewer.XYZOffset);
        center.y = (float)viewer.Model.pcBounds.Min.y; //On floor
        transform.position = center;
        Vector3d extent = cellExtentMax - cellExtentMin;
        transform.localScale = (Vector3)extent;

        Debug.Log(url);

        //Fetching Texture
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
        }
        else
        {
            Texture mapTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            GetComponent<MeshRenderer>().material.mainTexture = mapTexture;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
