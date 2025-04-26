using Unity.Burst;
using Unity.Mathematics;



[System.Serializable]
[BurstCompile(DisableSafetyChecks = true)]
public struct AudioRayResult
{
    public float distance;
    public float3 point;
    public float absorption;

    public bool IsNull => distance == -1;

    public static AudioRayResult Null => new AudioRayResult
    {
        distance = -1,
        point = float3.zero,
        absorption = 0f
    };
}