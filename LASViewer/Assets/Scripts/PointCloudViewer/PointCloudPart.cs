using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;
using System.IO;

public interface IPointCloudManager{
    Material getMaterialForDistance(float distance);
    Material getMaterialForBoundingBox(BoxCollider box);
    Color getColorForClass(float classification);
}

public static class BoxColliderExtension
{
    public static float sqrDistance(this BoxCollider box, Vector3 position)
    {
        Vector3 p = box.ClosestPoint(position);
        return (position - p).sqrMagnitude;
    }

    //public static float distanceToMainCamera(this BoxCollider box)
    //{
    //    Vector3 c = Camera.main.transform.position;
    //    Vector3 p = box.ClosestPoint(Camera.main.transform.position);
    //    return (c - p).magnitude;
    //}
}

class PointCloudOctreeLeafNode : PointCloudOctreeNode
{
    FileInfo fileInfo = null;
    IPointCloudManager manager = null;
    Renderer meshRenderer = null;
    MeshFilter meshFilter = null;

    float sqrVisibleDistance;
    bool isMeshInitialized = false;

    public void init(JSONNode node, DirectoryInfo directory, IPointCloudManager manager)
    {
        gameObject.name = "PointCloudOctreeLeafNode";
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = null;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        this.manager = manager;
        string filename = node["filename"];
        fileInfo = directory.GetFiles(filename)[0];
        Debug.Assert(fileInfo != null, "File not found:" + node["filename"]);
        initBoxCollider(node);

        //initMesh();
        sqrVisibleDistance = (float)(Camera.main.farClipPlane * 1.2);
        sqrVisibleDistance = sqrVisibleDistance * sqrVisibleDistance;
    }


    public void initMesh(){
        byte[] buffer = File.ReadAllBytes(fileInfo.FullName);
        Matrix2D m = Matrix2D.readFromBytes(buffer);
        meshFilter.mesh = createMeshFromLASMatrix(m.values);
        isMeshInitialized = false;
    }

    // Update is called once per frame
    void Update()
    {

        //Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        Vector3 camPos = Camera.main.transform.position;
        camPos = gameObject.transform.TransformPoint(camPos);

        float sqrDist = box.sqrDistance(camPos);
        if (sqrDist < sqrVisibleDistance)
        {
            if (!isMeshInitialized)
            {
                initMesh(); 
            }

            meshRenderer.material = manager.getMaterialForBoundingBox(box);
        }

    }

    //-------------

    Mesh createMeshFromLASMatrix(float[,] matrix)
    {
        int nPoints = matrix.GetLength(0);
        Mesh pointCloud = new Mesh();
        Vector3[] points = new Vector3[nPoints];
        int[] indices = new int[nPoints];
        Color[] colors = new Color[nPoints];

        for (int i = 0; i < nPoints; i++)
        {
            points[i] = new Vector3(matrix[i, 0], matrix[i, 1], matrix[i, 2]);
            indices[i] = i;
            float classification = matrix[i, 3];
            colors[i] = manager.getColorForClass(classification);
        }

        pointCloud.vertices = points;
        pointCloud.colors = colors;
        pointCloud.SetIndices(indices, MeshTopology.Points, 0);

        Debug.Log("Loaded Point Cloud Mesh with " + nPoints + " points.");

        return pointCloud;
    }

    public void getClosestPointOnRay(Ray ray, Vector2 screenPos, ref float maxDist, ref Vector3 closestHit)
    {
        RaycastHit hit;

        if (box.bounds.Contains(ray.origin) || box.Raycast(ray, out hit, maxDist))
        {
            Mesh mesh = meshFilter.mesh;
            print("Scanning Point Cloud with " + mesh.vertices.Length + " vertices.");
            foreach (Vector3 p in mesh.vertices)
            {
                Vector3 pWorld = transform.TransformPoint(p);
                Vector3 v = Camera.main.WorldToScreenPoint(pWorld);
                float distancePointToCamera = Mathf.Abs(v.z);
                if (distancePointToCamera < maxDist)
                {
                    float sqrDistance = (new Vector2(v.x, v.y) - screenPos).sqrMagnitude;
                    if (sqrDistance < 25.0f)
                    {
                        closestHit = pWorld;
                        maxDist = distancePointToCamera;
                    }
                }
            }
        }
    }
}

public class PointCloudPart : MonoBehaviour {

    Mesh mesh = null;
    string filePath = null;
    BoxCollider coll = null;
    MeshRenderer meshRenderer = null;
    IPointCloudManager matProvider = null;

    public void initWithFilePath(string filePath, IPointCloudManager matProvider){
        this.filePath = filePath;
        this.matProvider = matProvider;
        byte[] buffer = File.ReadAllBytes(filePath);
        Matrix2D m = Matrix2D.readFromBytes(buffer);
        mesh = createMeshFromLASMatrix(m.values);
    }

    // Use this for initialization
    void Start () {
        gameObject.name = (new FileInfo(filePath)).Name;
        gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        meshRenderer.material = matProvider.getMaterialForDistance(1000000.0f);

        gameObject.AddComponent<BoxCollider>();
        Bounds b = gameObject.GetComponent<MeshFilter>().mesh.bounds;
        coll = gameObject.GetComponent<BoxCollider>();
        coll.center = b.center;
        coll.size = b.size;
    }
	
	// Update is called once per frame
	void Update () {
        //Vector3 camPos = Camera.main.gameObject.transform.position;
        //Vector3 boxCenter = gameObject.transform.TransformPoint(coll.center);
        //float distanceToCam = (camPos - boxCenter).magnitude;
        //meshRenderer.material = matProvider.getMaterialForDistance(distanceToCam);

        meshRenderer.material = matProvider.getMaterialForBoundingBox(coll);
    }

    //-------------

    static Dictionary<float, Color> classColor = null;
    Color getColorForClass(float classification)
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

    Mesh createMeshFromLASMatrix(float[,] matrix)
    {
        int nPoints = matrix.GetLength(0);
        Mesh pointCloud = new Mesh();
        Vector3[] points = new Vector3[nPoints];
        int[] indices = new int[nPoints];
        Color[] colors = new Color[nPoints];

        for (int i = 0; i < nPoints; i++)
        {
            points[i] = new Vector3(matrix[i, 0], matrix[i, 1], matrix[i, 2]);
            indices[i] = i;
            float classification = matrix[i, 3];
            colors[i] = getColorForClass(classification);
        }

        pointCloud.vertices = points;
        pointCloud.colors = colors;
        pointCloud.SetIndices(indices, MeshTopology.Points, 0);

        Debug.Log("Generated Point Cloud Mesh with " + nPoints + " points.");

        return pointCloud;
    }

    public void getClosestPointOnRay(Ray ray, Vector2 screenPos, ref float maxDist, ref Vector3 closestHit)
    {
        RaycastHit hit;
        if (coll.bounds.Contains(ray.origin) || coll.Raycast(ray, out hit, maxDist))
        {
            print("Scanning Point Cloud with " + mesh.vertices.Length + " vertices.");
            foreach (Vector3 p in mesh.vertices)
            {
                Vector3 pWorld = transform.TransformPoint(p);
                Vector3 v = Camera.main.WorldToScreenPoint(pWorld);
                float distancePointToCamera = Mathf.Abs(v.z);
                if (distancePointToCamera < maxDist)
                {
                    float sqrDistance = (new Vector2(v.x, v.y) - screenPos).sqrMagnitude;
                    if (sqrDistance < 25.0f)
                    {
                        closestHit = pWorld;
                        maxDist = distancePointToCamera;
                    }
                }
            }
        }
    }
}
