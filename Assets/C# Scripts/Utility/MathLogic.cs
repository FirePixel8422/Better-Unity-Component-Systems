using Unity.Burst;
using Unity.Mathematics;



[BurstCompile(DisableSafetyChecks = true)]
public static class MathLogic
{

    [BurstCompile(DisableSafetyChecks = true)]
    public static float MoveTowards(float current, float target, float maxDelta)
    {
        float delta = target - current;
        if (math.abs(delta) <= maxDelta) return target;
        return current + math.sign(delta) * maxDelta;
    }
}