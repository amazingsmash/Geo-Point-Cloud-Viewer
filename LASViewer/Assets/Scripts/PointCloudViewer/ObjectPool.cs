using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class ObjectPool<T> where T : class, new()
{
    T[] instances = null;
    public int remaining { get; private set; }

    public ObjectPool(int count)
    {
        instances = new T[count];
        for (int i = 0; i < instances.Length; i++)
        {
            instances[i] = new T();
        }
        remaining = count;
    }

    public T GetInstance()
    {
        for (int i = 0; i < instances.Length; i++)
        {
            if (instances[i] != null)
            {
                T m = instances[i];
                instances[i] = null;
                remaining--;
                return m;
            }
        }
        return null;
    }

    public bool IsEmpty()
    {
        return remaining == 0;
    }

    public void ReleaseInstance(T instance)
    {
        for (int i = 0; i < instances.Length; i++)
        {
            if (instances[i] == null)
            {
                instances[i] = instance;
                remaining++;
                return;
            }
        }
    }
}