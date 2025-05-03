using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
public struct FibonacciDirectionsJobParallel : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeArray<float3> directions;


    [BurstCompile]
    public void Execute(int i)
    {
        //if (i != 0) return;
        //
        //directions[0] = new float3(0.2f, 0, 1);
        //directions[1] = new float3(0.25f, 0, 1);
        //directions[2] = new float3(0.3f, 0, 1);
        //
        //return;

        int count = directions.Length;
        float phi = math.PI * (3f - math.sqrt(5f)); // golden angle in radians
        float y = 1f - (i / (float)(count - 1)) * 2f;
        float radius = math.sqrt(1f - y * y);
        float theta = phi * i;

        float x = math.cos(theta) * radius;
        float z = math.sin(theta) * radius;

        directions[i] = math.normalize(new float3(x, y, z));
    }
}