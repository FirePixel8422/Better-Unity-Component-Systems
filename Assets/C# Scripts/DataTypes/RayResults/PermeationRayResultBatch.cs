using Unity.Mathematics;



public struct PermeationRayResultBatch
{
    private float accumulatedStrength;

    /// <summary>
    /// Resets the batch to its initial state (0 count, float3.zero for point)
    /// </summary>
    public void Reset()
    {
        accumulatedStrength = 0;
    }

    /// <summary>
    /// Adds a ray’s remaining strength to the accumulatedStrength.
    /// </summary>
    public void AddEntry(float strengthLeft)
    {
        accumulatedStrength += strengthLeft;
    }

    /// <summary>
    /// Get MuffleStrengthReduction based on accumulatedStrength and fullReductionStrength and get audioTargetPosEffectiveness.
    /// </summary>
    public void GetPermeationData(float fullReductionStrength, float fullReductionPercent, float3 audioTargetPosRelativeToPlayer, out float muffleReduction, out float3 audioTargetPosEffectiveness)
    {
        muffleReduction = math.clamp(accumulatedStrength / fullReductionStrength, 0, fullReductionPercent);

        audioTargetPosEffectiveness = audioTargetPosRelativeToPlayer * muffleReduction;
    }
}