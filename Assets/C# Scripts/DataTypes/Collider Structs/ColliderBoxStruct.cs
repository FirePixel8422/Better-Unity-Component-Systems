using Unity.Mathematics;
using Unity.Burst;


[System.Serializable]
[BurstCompile(DisableSafetyChecks = true)]
public struct ColliderBoxStruct
{
    public float3 center;
    public float3 size;
    public float absorption;

    public bool IsNull => size.x == -1;

    public static ColliderBoxStruct Null => new ColliderBoxStruct
    {
        size = new float3(-1, 0, 0)
    };
}