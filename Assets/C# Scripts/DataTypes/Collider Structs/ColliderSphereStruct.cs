using Unity.Mathematics;
using Unity.Burst;


[System.Serializable]
[BurstCompile(DisableSafetyChecks = true)]
public struct ColliderSphereStruct
{
    public float3 center;
    public float radius;
    public float absorption;

    public bool IsNull => radius == -1;

    public static ColliderSphereStruct Null => new ColliderSphereStruct
    {
        radius = -1,
    };
}