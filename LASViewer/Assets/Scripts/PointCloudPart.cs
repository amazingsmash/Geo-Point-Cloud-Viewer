using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;

public class PointCloudPart : MonoBehaviour {

    Mesh mesh = null;
    string filePath = null;
    Material material = null;

    public void initWithFilePath(string filePath, Material material){
        this.filePath = filePath;
        this.material = material;
        byte[] buffer = File.ReadAllBytes(filePath);
        Matrix2D m = Matrix2D.readFromBytes(buffer);
        mesh = createMeshFromLASMatrix(m.values);
    }

    // Use this for initialization
    void Start () {
        gameObject.name = (new FileInfo(filePath)).Name;
        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<MeshRenderer>();
        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        gameObject.GetComponent<MeshRenderer>().material = material;

        gameObject.AddComponent<BoxCollider>();
        Bounds b = gameObject.GetComponent<MeshFilter>().mesh.bounds;
        gameObject.GetComponent<BoxCollider>().center = b.center;
        gameObject.GetComponent<BoxCollider>().size = b.size;
    }
	
	// Update is called once per frame
	void Update () {
		
	}
    //-------------

    Dictionary<float, Color> classColor = null;
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

        if (classColor.ContainsKey(classification))
        {
            return classColor[classification];
        }
        else
        {
            return Color.gray;
        }
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
        Collider coll = gameObject.GetComponent<Collider>();
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
