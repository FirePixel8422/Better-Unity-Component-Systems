using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;


[System.Serializable]
[BurstCompile]
public struct ColliderSphereStruct
{
    public float3 center;
    public float radius;

    [Range(-1, 1)]
    public float absorption;

    [HideInInspector]
    public int audioTargetId;

    public bool IsNull => radius == -1;

    public static ColliderSphereStruct Null => new ColliderSphereStruct
    {
        radius = -1,
    };
}