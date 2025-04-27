using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
public struct GenerateFibonacciSphereDirectionsJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeArray<float3> directions;


    [BurstCompile]
    public void Execute(int i)
    {
        int count = directions.Length;
        float phi = math.PI * (3f - math.sqrt(5f)); // golden angle in radians
        float y = 1f - (i / (float)(count - 1)) * 2f;
        float radius = math.sqrt(1f - y * y);
        float theta = phi * i;

        float x = math.cos(theta) * radius;
        float z = math.sin(theta) * radius;

        directions[i] = math.normalize(new float3(x, y, z));
        //directions[i] = new float3(1, 0, 0);
    }
}