using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;


[System.Serializable]
[BurstCompile]
public struct ColliderAABBStruct
{
    public float3 center;
    public float3 size;
    [Range(0, 1)]
    public float absorption;

    public bool IsNull => size.x == -1;

    public static ColliderAABBStruct Null => new ColliderAABBStruct
    {
        size = new float3(-1, 0, 0)
    };
}