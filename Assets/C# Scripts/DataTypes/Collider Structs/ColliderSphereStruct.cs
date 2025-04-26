using Unity.Mathematics;
using Unity.Burst;


[System.Serializable]
[BurstCompile(DisableSafetyChecks = true)]
public struct ColliderSphereStruct
{
    public float3 center;
    public float radius;

    public float absorption;
}