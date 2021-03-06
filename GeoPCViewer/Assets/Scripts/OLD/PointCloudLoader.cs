﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using System.IO;
//http://www.kamend.com/2014/05/rendering-a-point-cloud-inside-unity/

//public class PointCloudLoader : MonoBehaviour, IPointCloudManager
//{

//    public float hdMaxDistance = 5000.0f;
//    public Material hdMaterial = null;
//    public Material ldMaterial = null;

//    Material IPointCloudManager.getMaterialForDistance(float distance)
//    {
//        return (distance < hdMaxDistance) ? hdMaterial : ldMaterial;
//    }

//    Material IPointCloudManager.getMaterialForBoundingBox(BoxCollider box){
//        Vector3 camPos = Camera.main.transform.position;
//        if (box.bounds.Contains(camPos))
//        {
//            return hdMaterial;
//        }
//        else
//        {
//            Vector3 p = box.ClosestPoint(camPos);
//            float sqrDist = (p - camPos).sqrMagnitude;
//            return (sqrDist < (hdMaxDistance * hdMaxDistance)) ? hdMaterial : ldMaterial;
//        }
//    }

//    DirectoryInfo getModelDirectory()
//    {
//        //return new DirectoryInfo("/Users/josemiguelsn/Desktop/repos/LASViewer/Models/LAS MODEL MINI");
//#if UNITY_EDITOR
//        string path = EditorUtility.OpenFolderPanel("Select Model Folder", "", "");
//        if (path.Length > 0)
//        {
//            return new DirectoryInfo(path);
//        }
//        return null;
//#else
//        return new DirectoryInfo("../Models/7");
//#endif
//    }

//    // Use this for initialization
//    void Start()
//    {
//        //DirectoryInfo dir = new DirectoryInfo(getAssetsPath() + "/" + LASByteFolderName);
//        DirectoryInfo dir = getModelDirectory();

//        if (dir != null){
//            FileInfo[] info = dir.GetFiles("*.bytes");
//            foreach (FileInfo f in info)
//            {

//                GameObject child = new GameObject("PointCloud");
//                child.AddComponent<PointCloudPart>();
//                child.GetComponent<PointCloudPart>().initWithFilePath(f.FullName, this);
//                child.transform.SetParent(this.transform, false);
//            }
//        }
//    }

//    void Update()
//    {
//        selectPoint();
//    }


//    void selectPoint()
//    {
//        if (Input.GetMouseButtonDown(0))
//        {
//            Debug.Log("Finding selected point.");

//            Vector3 mPos = Input.mousePosition;
//            MeshFilter[] mf = GetComponentsInChildren<MeshFilter>();
//            float maxDist = 10000000.0f;

//            Vector3 closestHit = Vector3.negativeInfinity;

//            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//            foreach (Transform child in transform)
//            {
//                child.GetComponent<PointCloudPart>().getClosestPointOnRay(ray, mPos, ref maxDist, ref closestHit);
//            }

//            if (!closestHit.Equals(Vector3.negativeInfinity))
//            {
//                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//                sphere.transform.position = closestHit;
//                sphere.transform.localScale = new Vector3(4.0f, 4.0f, 4.0f);
//            }

//        }
//    }

//    static Dictionary<float, Color> classColor = null;
//    Color IPointCloudManager.getColorForClass(float classification)
//    {
//        if (classColor == null)
//        {
//            classColor = new Dictionary<float, Color>();
//            classColor[16] = Color.blue;
//            classColor[19] = Color.blue;
//            classColor[17] = Color.red;
//            classColor[20] = Color.green;
//            classColor[31] = new Color(244.0f / 255.0f, 191.0f / 255.0f, 66.0f / 255.0f);
//            classColor[29] = Color.black;
//            classColor[30] = new Color(244.0f / 255.0f, 65.0f / 255.0f, 244.0f / 255.0f);
//        }

//        return (classColor.ContainsKey(classification)) ? classColor[classification] : Color.gray;
//    }


//}