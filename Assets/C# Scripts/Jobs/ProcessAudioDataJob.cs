using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
public struct ProcessAudioDataJob : IJob
{             
    [ReadOnly, NoAlias] public int TotalAudioTargets;
    [ReadOnly, NoAlias] public NativeArray<float3> AudioTargetPositions;
             
    [ReadOnly, NoAlias] public NativeArray<ushort> MuffleRayHits;
    [ReadOnly, NoAlias] public float MuffleEffectiveness;

    [ReadOnly, NoAlias] public NativeArray<float> PermeationPowerRemains;
    [ReadOnly, NoAlias] public float PermeationStrengthPerRay;
    [ReadOnly, NoAlias] public float PermeationEffectiveness;

    [ReadOnly, NoAlias] public NativeArray<half> EchoRayDistances;
    [ReadOnly, NoAlias] public float MaxReverbDistance;
             
    [ReadOnly, NoAlias] public int MaxHitsPerRay;
    [ReadOnly, NoAlias] public int RayCount;
    [ReadOnly, NoAlias] public float3 RayOrigin;

    // Return array with audio settings per audio target
    [WriteOnly, NoAlias] public NativeArray<AudioTargetRTSettings> AudioTargetSettings;


    [BurstCompile]
    public void Execute()
    {
        int maxBatchSize = MuffleRayHits.Length / TotalAudioTargets;
        int maxRayHits = MaxHitsPerRay * RayCount;

        // Calculate avarage echo distance and total echo ray return hits
        float reverbTotal = 0;
        float echoRayReturnedHits = 0;
        for (int i = 0; i < maxRayHits; i++)
        {
            if (EchoRayDistances[i] == 0)
            {
                echoRayReturnedHits += 1;
                continue;
            }
            reverbTotal += EchoRayDistances[i];
        }
        float avgReverbDist = reverbTotal / maxRayHits;
        float reverbStrength = avgReverbDist / MaxReverbDistance;
        float reverbVolume = echoRayReturnedHits / maxRayHits;


        // Calculate audio strength and panstero based on newly calculated data
        for (int audioTargetId = 0; audioTargetId < TotalAudioTargets; audioTargetId++)
        {
            int totalMuffleRayhits = 0;
            float totalPermeationPower = 0;

            // Combine all spread muffleRayHitCount values for current audioTarget to 1 int (totalMuffleRayhits)
            for (int i = 0; i < maxBatchSize; i++)
            {
                totalMuffleRayhits += MuffleRayHits[TotalAudioTargets * i + audioTargetId];
                totalPermeationPower += PermeationPowerRemains[TotalAudioTargets * i + audioTargetId];
            }

            // Set muffleRayHits of current audiotargetId to the totalMuffleRayhits
            float muffle = 1 - (float)totalMuffleRayhits / (RayCount * MaxHitsPerRay) * MuffleEffectiveness;
            float permeation = (float)totalPermeationPower / RayCount / PermeationStrengthPerRay * PermeationEffectiveness;

            muffle = math.saturate(muffle - permeation);

            // Write new settings back to array
            AudioTargetSettings[audioTargetId] = new AudioTargetRTSettings(muffle, reverbStrength, reverbVolume, AudioTargetPositions[audioTargetId]);
        }
    }
}