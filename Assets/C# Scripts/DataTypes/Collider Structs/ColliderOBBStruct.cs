using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;


[System.Serializable]
[BurstCompile]
public struct ColliderOBBStruct
{
    public float3 center;
    public float3 size;
    public quaternion rotation;
    [Range(0, 1)]
    public float absorption;

    public bool IsNull => size.x == -1;

    public static ColliderOBBStruct Null => new ColliderOBBStruct
    {
        size = new float3(-1, 0, 0)
    };
}