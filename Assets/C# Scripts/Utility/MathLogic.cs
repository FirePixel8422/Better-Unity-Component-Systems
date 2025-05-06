using Unity.Burst;
using Unity.Mathematics;



[BurstCompile(DisableSafetyChecks = true)]
public static class MathLogic
{
    public static float DistanceFrom(this float3 value, float3 toSubtract)
    {
        float3 difference = value - toSubtract;
        return math.abs(difference.x) + math.abs(difference.y) + math.abs(difference.z);
    }

    public static int DistanceFrom(this int3 value, int3 toSubtract)
    {
        int3 difference = value - toSubtract;
        return math.abs(difference.x) + math.abs(difference.y) + math.abs(difference.z);
    }


    [BurstCompile(DisableSafetyChecks = true)]
    public static float MoveTowards(float current, float target, float maxDelta)
    {
        float delta = target - current;
        if (math.abs(delta) <= maxDelta) return target;
        return current + math.sign(delta) * maxDelta;
    }
}