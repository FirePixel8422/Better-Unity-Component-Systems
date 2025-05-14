using Unity.Mathematics;



public struct DirectionRayResultBatch
{
    private float3 weightedPoint;

    private float totalWeight;


    /// <summary>
    /// Resets the batch to its initial state (0 count, float3.zero for point)
    /// </summary>
    public void Reset()
    {
        weightedPoint = float3.zero;
        totalWeight = 0;
    }

    /// <summary>
    /// Adds a hit point with weighting based on 1 / totalDistance — the closer the ray, the more it contributes to the final averaged point.
    /// </summary>
    public void AddEntry(float3 rayHitPosRelativeToPlayer, float totalDistance)
    {
        weightedPoint += rayHitPosRelativeToPlayer / totalDistance;
        totalWeight += 1 / totalDistance;
    }


    /// <returns>The Averaged point</returns>
    public float3 GetAvgResult()
    {
        if (totalWeight == 0)
        {
            return float3.zero;
        }

        return weightedPoint / totalWeight;
    }
}