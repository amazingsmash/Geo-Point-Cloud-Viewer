using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPointCloudListener
{
    void onPointSelected(Vector3 point, float classCode);
}

public interface IPointCloudManager
{
    Color GetColorForClass(int classification);
    MeshManager GetMeshManager();
    //Material HDMaterial { get; }
    //Material LDMaterial { get; }

    //Material[] GetMaterialsForDistance(float minDistance, float maxDistance);

    void ModifyRendererBasedOnBounds(Bounds bounds, MeshRenderer meshRenderer);

    Material GetNDM();
    Material GetFDM();
    float nearDistanceThreshold();
    float farDistanceThreshold();
}
