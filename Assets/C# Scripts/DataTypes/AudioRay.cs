using Unity.Burst;
using Unity.Mathematics;



[System.Serializable]
[BurstCompile(DisableSafetyChecks = true)]
public struct AudioRay
{
    public float3 origin;
    public float3 direction;
}