using UnityEngine;

public struct Box
{
    public readonly Vector3d Min, Max;
    public Box(Vector3d min, Vector3d max)
    {
        this.Min = min;
        this.Max = max;
    }

    public Vector3d Center { get => (Max + Min) / 2; }
    public Vector3d Size { get => (Max - Min); }
}
