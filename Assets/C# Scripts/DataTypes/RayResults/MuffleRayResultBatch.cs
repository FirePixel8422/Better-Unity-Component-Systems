using Unity.Mathematics;



public struct MuffleRayResultBatch
{
    private float totalDistance;
    private int hitCount;
    private int totalRayCount;

    public static MuffleRayResultBatch Default()
    {
        return new MuffleRayResultBatch
        {
            totalDistance = 0f,
            hitCount = 0,
            totalRayCount = 0
        };
    }

    public void AddEntry(bool hit, float totalDistanceForRay)
    {
        if (hit)
        {
            totalDistance += totalDistanceForRay;
            hitCount += 1;
        }
        totalRayCount += 1;
    }


    /// <summary>
    /// Get MuffleStrength based on the rayDistances and hit percentage.
    /// </summary>
    public readonly float GetMuffleStrength(float fullClarityDist, float fullClarityHitPercentage)
    {
        if (totalRayCount == 0 || hitCount == 0)
        {
            return 1f;
        }

        float hitPercentage = (float)hitCount / totalRayCount;
        float hitFactor = hitPercentage / fullClarityHitPercentage;

        float avgHitDistance = totalDistance / hitCount;
        float distanceFactor = fullClarityDist / avgHitDistance;

        // multiply hitFactor (0-1 hit percentage) by distanceFactor (0-1 percentage of fullClarityDist) to get muffle strength
        float clarity = math.clamp(hitFactor * distanceFactor, 0f, 1f);

        return 1f - clarity;
    }
}
