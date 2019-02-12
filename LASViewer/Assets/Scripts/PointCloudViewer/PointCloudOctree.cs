using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SimpleJSON;
using System.IO;
using UnityEditor;

public partial class PointCloudOctree : MonoBehaviour, IPointCloudManager
{
    DirectoryInfo directory = null;
    float secondsSinceLastVisibilityCheck = 0;
    PointCloudNode[] topNodes = null;

    DirectoryInfo getModelDirectory()
    {
        return new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/LAS MODEL");
#if UNITY_EDITOR
        string path = EditorUtility.OpenFolderPanel("Select Model Folder", "", "");
        if (path.Length > 0)
        {
            return new DirectoryInfo(path);
        }
        return null;
#else
        return new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/LAS MODEL");
#endif
    }

    // Use this for initialization
    void Start()
    {
        DirectoryInfo dir = getModelDirectory();
        init(dir);
    }

    public void init(DirectoryInfo directory)
    {
        if (!transform.lossyScale.NearlyEquals(Vector3.one))
        {
            Debug.LogError("PointCloud must be not to scale.");
            return;
        }


        this.directory = directory;
        FileInfo index = directory.GetFiles("voxelIndex.json")[0];
        StreamReader reader = new StreamReader(index.FullName);
        string s = reader.ReadToEnd();
        JSONNode json = JSON.Parse(s);

        if (json.IsArray)
        {
            topNodes = new PointCloudNode[json.AsArray.Count];
            for (int i = 0; i < json.AsArray.Count; i++)
            {
                topNodes[i] = PointCloudNode.addNode(json.AsArray[i], this.directory, gameObject, this);
            }
        }
        else
        {
            topNodes = new PointCloudNode[1];
            topNodes[0] = PointCloudNode.addNode(json, this.directory, gameObject, this);
        }

        System.GC.Collect(); //Garbage Collection
    }


    void Update()
    {
        CheckNodeRenderState();
        selectPoint();
    }

    private void CheckNodeRenderState()
    {
        secondsSinceLastVisibilityCheck += Time.deltaTime;
        if (secondsSinceLastVisibilityCheck > 0.25f)
        {
            float sqrVisibleDistance = (float)(Camera.main.farClipPlane * 1.2);
            sqrVisibleDistance = sqrVisibleDistance * sqrVisibleDistance;

            Vector3 camPos = Camera.main.transform.position;
            foreach(PointCloudNode node in topNodes){
                node.testRenderState(PointCloudNode.PCNodeState.VISIBLE,
                                       camPos,
                                       sqrVisibleDistance);
            }
            //Vector3 cameraInObjSpacePosition = gameObject.transform.TransformPoint(camPos);
            //for (int i = 0; i < transform.childCount; i++)
            //{
            //    GameObject child = transform.GetChild(i).gameObject;
            //    PointCloudNode node = child.GetComponent<PointCloudNode>();
            //    if (node != null)
            //    {
            //        node.testRenderState(PointCloudNode.PCNodeRenderState.VISIBLE, 
            //                             cameraInObjSpacePosition, 
            //                             sqrVisibleDistance);
            //    }
            //}

            //Debug.Log("N Vis-Inv Nodes" + PointCloudNode.nVisibleNodes + " " + PointCloudNode.nInvisibleNodes);
            PointCloudNode.nVisibleNodes = 0;
            PointCloudNode.nInvisibleNodes = 0;

            secondsSinceLastVisibilityCheck = 0.0f;
        }
    }


    void selectPoint()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Finding selected point.");

            float maxScreenDistance = 20.0f;

            Vector3 mousePosition = Input.mousePosition;
            MeshFilter[] mf = GetComponentsInChildren<MeshFilter>();
            float maxDist = 10000000.0f;

            Vector3 closestHit = Vector3.negativeInfinity;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                PointCloudNode node = child.GetComponent<PointCloudNode>();
                if (node != null)
                {
                    node.GetClosestPointOnRay(ray,
                                              mousePosition,
                                              ref maxDist, 
                                              ref closestHit, 
                                              maxScreenDistance * maxScreenDistance);
                }
            }

            if (!closestHit.Equals(Vector3.negativeInfinity))
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = closestHit;
                sphere.transform.localScale = new Vector3(4.0f, 4.0f, 4.0f);
            }

        }
    }
}

//IPointCloudManager
public partial class PointCloudOctree : MonoBehaviour, IPointCloudManager
{
    public float hdMaxDistance = 5000.0f;
    public Material hdMaterial = null;
    public Material ldMaterial = null;

    Material IPointCloudManager.getMaterialForDistance(float distance)
    {
        return (distance < hdMaxDistance) ? hdMaterial : ldMaterial;
    }

    Material IPointCloudManager.getMaterialForBoundingBox(Bounds box)
    {
        Vector3 camPos = Camera.main.transform.position;
        if (box.Contains(camPos))
        {
            return hdMaterial;
        }
        else
        {
            Vector3 p = box.ClosestPoint(camPos);
            float sqrDist = (p - camPos).sqrMagnitude;
            return (sqrDist < (hdMaxDistance * hdMaxDistance)) ? hdMaterial : ldMaterial;
        }
    }

    static Dictionary<float, Color> classColor = null;
    Color IPointCloudManager.getColorForClass(float classification)
    {
        if (classColor == null)
        {
            classColor = new Dictionary<float, Color>();
            classColor[16] = Color.blue;
            classColor[19] = Color.blue;
            classColor[17] = Color.red;
            classColor[20] = Color.green;
            classColor[31] = new Color(244.0f / 255.0f, 191.0f / 255.0f, 66.0f / 255.0f);
            classColor[29] = Color.black;
            classColor[30] = new Color(244.0f / 255.0f, 65.0f / 255.0f, 244.0f / 255.0f);
        }

        return (classColor.ContainsKey(classification)) ? classColor[classification] : Color.gray;
    }
}