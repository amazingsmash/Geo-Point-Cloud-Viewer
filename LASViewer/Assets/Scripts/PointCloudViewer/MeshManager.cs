using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MeshManager
{
    private ObjectPool<Mesh> meshPool = null;
    private ObjectPool<MeshLoaderJob> jobPool = null;
    private readonly AsyncJobThread thread = new AsyncJobThread();
    private Dictionary<string, MeshLoaderJob> jobs = new Dictionary<string, MeshLoaderJob>();

    public int NPendingMeshes
    {
        get
        {
            return thread.NPendingJobs;
        }
    }

    public int NAvailableMeshes
    {
        get
        {
            return meshPool.Size;
        }
    }

    public MeshManager(int nMeshes, int nJobs)
    {
        meshPool = new ObjectPool<Mesh>(nMeshes);
        jobPool = new ObjectPool<MeshLoaderJob>(nJobs);
    }

    public Mesh CreateMesh(FileInfo fileInfo, IPointCloudManager manager, float priority)
    {
        if (!jobs.ContainsKey(fileInfo.FullName))
        {
            MeshLoaderJob job = jobPool.GetInstance();
            if (job != null)
            {
                jobs[fileInfo.FullName] = job;
                job.AsyncMeshLoading(fileInfo, manager, thread, priority);
            }
        }
        else
        {
            MeshLoaderJob job = jobs[fileInfo.FullName];
            if (job.IsDone)
            {
                Mesh mesh = meshPool.GetInstance();
                if (mesh != null)
                {
                    //Debug.Log("Remaining Meshes: " + meshPool.remaining);
                    job.LoadMeshData(mesh);
                    if (mesh != null)
                    {
                        jobs.Remove(fileInfo.FullName);
                        jobPool.ReleaseInstance(job);
                    }
                    return mesh;
                }
            }
            else
            {
                //Changing priority if not finished
                job.priority = priority;
            }
        }
        return null;
    }

    public void ReleaseMesh(Mesh mesh)
    {
        mesh.Clear();
        meshPool.ReleaseInstance(mesh);
    }

    public void StopLoaderThread()
    {
        thread.Stop();
    }
}

public class AsyncJobThread
{
    public abstract class Job
    {
        public float priority = 0;
        public bool IsDone { get; protected set; }

        public void Run(AsyncJobThread thread, float priority)
        {
            this.priority = priority;
            IsDone = false;
            thread.RunJob(this);
        }

        public abstract void Execute();
    }

    private System.Threading.Thread thread = null;
    private readonly ArrayList jobs = new ArrayList();
    private bool running = false;
    public int NPendingJobs { get { return jobs.Count; } }

    public void RunJob(Job job)
    {
        if (!running)
        {
            thread = new System.Threading.Thread(Run);
            running = true;
            thread.Start();
        }

        lock (jobs.SyncRoot)
        {
            jobs.Add(job);
        }
    }

    public void Run()
    {
        while (running)
        {
            Job job = ExtractJob();
            if (job != null)
            {
                job.Execute();
            }
        }
    }

    public void Stop()
    {
        running = false;
    }

    MeshLoaderJob ExtractJob()
    {
        lock (jobs.SyncRoot)
        {
            if (jobs.Count > 0)
            {
                MeshLoaderJob job = (MeshLoaderJob)jobs[0];
                foreach (MeshLoaderJob j in jobs)
                {
                    if (j.priority > job.priority)
                    {
                        job = j;
                    }
                }

                jobs.Remove(job);
                return job;
            }
            return null;
        }
    }
}

public class MeshLoaderJob: AsyncJobThread.Job
{
    private Vector3[] points = null;
    private int[] indices = null;
    private Color[] colors = null;

    private FileInfo fileInfo;
    private IPointCloudManager manager;

    public void AsyncMeshLoading(FileInfo fileInfo, IPointCloudManager manager, AsyncJobThread thread, float priority)
    {
        this.fileInfo = fileInfo;
        this.manager = manager;
        Run(thread, priority);
    }

    public override void Execute()
    {
        byte[] buffer = File.ReadAllBytes(fileInfo.FullName);
        Matrix2D m = Matrix2D.readFromBytes(buffer);
        CreateMeshFromLASMatrix(m.values);
    }

    public Mesh LoadMeshData(Mesh pointCloud)
    {
        if (pointCloud == null)
        {
            Debug.Log("Mesh Pool Empty");
            return null;
        }
        Debug.Assert(points.Length == indices.Length && points.Length == colors.Length,
                     "Arrays of different length at creating mesh.");

        pointCloud.Clear();
        pointCloud.vertices = points;
        pointCloud.colors = colors;
        pointCloud.SetIndices(indices, MeshTopology.Points, 0);

        //Debug.Log("Loaded Point Cloud Mesh with " + points.Length + " points.");

        ReleaseData();

        return pointCloud;
    }

    private void ReleaseData()
    {
        points = null;
        indices = null;
        colors = null;
    }

    private void CreateMeshFromLASMatrix(float[,] matrix)
    {
        int nPoints = matrix.GetLength(0);
        points = new Vector3[nPoints];
        indices = new int[nPoints];
        colors = new Color[nPoints];

        ArrayList cs = new ArrayList();

        for (int i = 0; i < nPoints; i++)
        {
            points[i] = new Vector3(matrix[i, 0], matrix[i, 2], matrix[i, 1]); //XZY
            indices[i] = i;
            int classification = (int)matrix[i, 3];
            colors[i] = manager.GetColorForClass(classification);
        }

        IsDone = true;
    }
}
