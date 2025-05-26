using Unity.Mathematics;



public struct DirectionRayResult
{
    public float3 WeightedPoint { get; private set; }
    public int HitAudioTargetId { get; private set; }


    public static DirectionRayResult Default()
    {
        return new DirectionRayResult
        {
            WeightedPoint = float3.zero,
            HitAudioTargetId = -1
        };
    }

    /// <summary>
    /// Set hit point with weighting based on 1 / totalDistance — the closer the ray, the more it contributes to the final averaged point after aditional processing.
    /// </summary>
    public DirectionRayResult(float3 rayHitPosRelativeToPlayer, float totalDistance, int hitAudioTargetId)
    {
        WeightedPoint = rayHitPosRelativeToPlayer / totalDistance;
        HitAudioTargetId = hitAudioTargetId;
    }
}