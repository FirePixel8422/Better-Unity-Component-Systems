using Unity.Burst;
using Unity.Mathematics;



[BurstCompile]
public struct EchoRayResult
{
    public float3 directionToOrigin;
    public float distanceTraveled;
}