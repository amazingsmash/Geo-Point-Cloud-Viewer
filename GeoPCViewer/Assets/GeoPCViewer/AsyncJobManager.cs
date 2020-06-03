using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public abstract class Job
{
    public bool IsDone { get; protected set; } = false;

    public abstract void Execute();
}


public class AsyncJobManager
{
    private System.Threading.Thread thread = null;
    private readonly SortedList jobs = new SortedList();
    private readonly object mutex = new object();
    private bool running = false;

    Job PopTopPriorityJob()
    {
        lock (mutex)
        {
            if (jobs.Count > 0)
            {
                Job job = (Job)jobs.GetByIndex(0);
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

