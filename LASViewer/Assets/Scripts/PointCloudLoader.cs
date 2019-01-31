using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using System.IO;
//http://www.kamend.com/2014/05/rendering-a-point-cloud-inside-unity/

public class PointCloudLoader : MonoBehaviour
{

    //public string LASByteFolderName = "7";

    //public static string getAssetsPath()
    //{
    //    string assetsPath = "";
    //    RuntimePlatform platform = Application.platform;
    //    switch (platform)
    //    {
    //        case RuntimePlatform.Android:
    //            assetsPath = "jar:file://" + Application.dataPath + "!/assets";
    //            break;
    //        case RuntimePlatform.IPhonePlayer:
    //            assetsPath = Application.dataPath + "/Raw";
    //            break;
    //        case RuntimePlatform.OSXEditor:
    //        case RuntimePlatform.WindowsEditor:
    //        case RuntimePlatform.WindowsPlayer:
    //            assetsPath = Application.dataPath + "/StreamingAssets";
    //            break;
    //        case RuntimePlatform.OSXPlayer:
    //            assetsPath = Application.dataPath + "/Resources/Data/StreamingAssets";
    //            break;
    //        default:
    //            Debug.Log("Unrecognized platform.");
    //            break;
    //    }

    //    return assetsPath;
    //}

    //void createChildWithMesh(Mesh mesh){
    //    GameObject child = new GameObject("PointCloud");
    //    child.AddComponent<MeshFilter>();
    //    child.AddComponent<MeshRenderer>();
    //    child.GetComponent<MeshFilter>().mesh = mesh;
    //    child.GetComponent<MeshRenderer>().material = GetComponent<MeshRenderer>().material;
    //    child.transform.SetParent(this.transform, false);

    //    child.AddComponent<BoxCollider>();
    //    Bounds b = child.GetComponent<MeshFilter>().mesh.bounds;
    //    child.GetComponent<BoxCollider>().center = b.center;
    //    child.GetComponent<BoxCollider>().size = b.size;
    //}

    //void readFromCSV(){

    //    DirectoryInfo dir = new DirectoryInfo(getAssetsPath() + "/LAS_CSV_2");
    //    FileInfo[] info = dir.GetFiles("*.*");
    //    foreach (FileInfo f in info)
    //    {
    //        if (f.FullName.EndsWith("txt"))
    //        {
    //            StreamReader reader = new StreamReader(f.FullName);
    //            string csv = reader.ReadToEnd();

    //            float[,] mat = CSVManager.ReadFloatCSVMatrix(csv);
    //            Debug.Log("Read matrix " + mat.GetLength(0) + " x " + mat.GetLength(1));
    //            //mesh = createMeshFromLASMatrix(mat);
    //            Mesh mesh = createMeshFromLASMatrix(mat);
    //            createChildWithMesh(mesh);


    //            //break; //Just 1 cloud
    //        }
    //    }

    //}

    DirectoryInfo getModelDirectory(){
        string path = EditorUtility.OpenFolderPanel("Select Model Folder", "", "");
        if (path.Length > 0){
            return new DirectoryInfo(path);
        }
        return null;
    }

    // Use this for initialization
    void Start()
    {
        //DirectoryInfo dir = new DirectoryInfo(getAssetsPath() + "/" + LASByteFolderName);
        DirectoryInfo dir = getModelDirectory();
        if (dir != null){
            FileInfo[] info = dir.GetFiles("*.bytes");
            foreach (FileInfo f in info)
            {
                //byte[] buffer = File.ReadAllBytes(f.FullName);
                //Matrix2D m = Matrix2D.readFromBytes(buffer);
                //Mesh mesh = createMeshFromLASMatrix(m.values);
                //createChildWithMesh(mesh);


                GameObject child = new GameObject("PointCloud");
                child.AddComponent<PointCloudPart>();
                child.GetComponent<PointCloudPart>().initWithFilePath(f.FullName, GetComponent<MeshRenderer>().material);
                child.transform.SetParent(this.transform, false);
            }
        }
    }

    //Dictionary<float, Color> classColor = null;
    //Color getColorForClass(float classification){

    //    if (classColor == null)
    //    {
    //        classColor = new Dictionary<float, Color>();
    //        classColor[16] = Color.blue;
    //        classColor[19] = Color.blue;
    //        classColor[17] = Color.red;
    //        classColor[20] = Color.green;
    //        classColor[31] = new Color(244.0f / 255.0f, 191.0f / 255.0f, 66.0f / 255.0f);
    //        classColor[29] = Color.black;
    //        classColor[30] = new Color(244.0f / 255.0f, 65.0f / 255.0f, 244.0f / 255.0f);
    //    }

    //    if (classColor.ContainsKey(classification)){
    //        return classColor[classification];
    //    } else{
    //        return Color.gray;
    //    }
    //}

    //Mesh createMeshFromLASMatrix(float[,] matrix)
    //{
    //    int nPoints = matrix.GetLength(0);
    //    Mesh mesh = new Mesh();
    //    Vector3[] points = new Vector3[nPoints];
    //    int[] indices = new int[nPoints];
    //    Color[] colors = new Color[nPoints];

    //    for (int i = 0; i < nPoints; i++)
    //    {
    //        points[i] = new Vector3(matrix[i, 0], matrix[i, 1], matrix[i, 2]);
    //        indices[i] = i;
    //        float classification = matrix[i, 3];
    //        colors[i] = getColorForClass(classification);
    //    }

    //    mesh.vertices = points;
    //    mesh.colors = colors;
    //    mesh.SetIndices(indices, MeshTopology.Points, 0);

    //    Debug.Log("Generated Point Cloud Mesh with " + nPoints + " points.");

    //    return mesh;
    //}

    //Mesh createRandomCubeMesh()
    //{
    //    int numPoints = 60000;

    //    Mesh mesh = new Mesh();
    //    Vector3[] points = new Vector3[numPoints];
    //    int[] indices = new int[numPoints];
    //    Color[] colors = new Color[numPoints];
    //    for (int i = 0; i < points.Length; ++i)
    //    {
    //        points[i] = new Vector3(Random.Range(-10, 10), Random.Range(-10, 10), Random.Range(-10, 10));
    //        indices[i] = i;
    //        colors[i] = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), 1.0f);
    //    }

    //    mesh.vertices = points;
    //    mesh.colors = colors;
    //    mesh.SetIndices(indices, MeshTopology.Points, 0);
    //    return mesh;
    //}

    void Update()
    {
        selectPoint();
        //if (Input.GetMouseButtonDown(0))
        //{
        //    Debug.Log("Finding selected point.");

        //    Vector3 mPos = Input.mousePosition;
        //    MeshFilter[] mf = GetComponentsInChildren<MeshFilter>();
        //    const float maxDist = 10000000.0f;

        //    Vector3 closestHit = Vector3.negativeInfinity;
        //    float minHitDistance = maxDist;

        //    foreach (Transform child in transform){


        //        Collider coll = child.GetComponent<Collider>();
        //        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //        RaycastHit hit;
        //        if (!coll.bounds.Contains(ray.origin) && !coll.Raycast(ray, out hit, maxDist))
        //        {
        //            continue;
        //        }

        //        Mesh mesh = child.GetComponent<MeshFilter>().mesh;


        //        print("Scanning Point Cloud with " + mesh.vertices.Length + " vertices.");
        //        foreach (Vector3 p in mesh.vertices)
        //        {
        //            Vector3 pWorld = transform.TransformPoint(p);
        //            Vector3 v = Camera.main.WorldToScreenPoint(pWorld);
        //            float distancePointToCamera = Mathf.Abs(v.z);
        //            if (distancePointToCamera < minHitDistance)
        //            {
        //                float sqrDistance = (new Vector2(v.x, v.y) - new Vector2(mPos.x, mPos.y)).sqrMagnitude;
        //                if (sqrDistance < 25.0f)
        //                {
        //                    closestHit = pWorld;
        //                    minHitDistance = distancePointToCamera;
        //                }
        //            }
        //        }
        //    }

        //    if (!closestHit.Equals(Vector3.negativeInfinity))
        //    {
        //        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //        sphere.transform.position = closestHit;
        //        sphere.transform.localScale = new Vector3(4.0f, 4.0f, 4.0f);
        //    }

        //}
    }


    void selectPoint()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Finding selected point.");

            Vector3 mPos = Input.mousePosition;
            MeshFilter[] mf = GetComponentsInChildren<MeshFilter>();
            float maxDist = 10000000.0f;

            Vector3 closestHit = Vector3.negativeInfinity;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            foreach (Transform child in transform)
            {
                child.GetComponent<PointCloudPart>().getClosestPointOnRay(ray, mPos, ref maxDist, ref closestHit);
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