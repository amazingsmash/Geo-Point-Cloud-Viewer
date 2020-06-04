using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public abstract class Job
{
    public bool IsDone { get; protected set; } = false;
    public int priority = 0;

    public abstract void Execute();
}

public class PriorityComparer : IComparer<int>
{
    public int Compare(int x, int y)
    {
        return x < y ? -1 : 1;
    }
}

public class AsyncJobManager
{
    private System.Threading.Thread thread = null;
    private readonly SortedList<int, Job> jobs = new SortedList<int, Job>(new PriorityComparer());
    private readonly object mutex = new object();
    private bool running = false;

    Job PopTopPriorityJob()
    {
        lock (mutex)
        {
            if (jobs.Count > 0)
            {
                Job job = (Job)jobs.Values[0];
                jobs.RemoveAt(0);
                return job;
            }
            return null;
        }
    }

    public void RunJob(Job job, int priority)
    {
        if (!running)
        {
            thread = new System.Threading.Thread(ThreadMain);
            running = true;
            thread.Start();
        }

        lock (mutex)
        {
            jobs.Add(priority, job);
        }
    }

    private void ThreadMain()
    {
        while (running)
        {
            Job job = PopTopPriorityJob();
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

}

