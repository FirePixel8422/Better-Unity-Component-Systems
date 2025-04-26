using Unity.Mathematics;
using Unity.Burst;


[System.Serializable]
[BurstCompile(DisableSafetyChecks = true)]
public struct ColliderBoxStruct
{
    public float3 center;
    public float3 size;

    public float absorption;
}