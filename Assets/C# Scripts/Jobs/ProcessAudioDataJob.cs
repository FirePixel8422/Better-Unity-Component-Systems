using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
public struct ProcessAudioDataJob : IJob
{
    [NativeDisableParallelForRestriction]
    [ReadOnly][NoAlias] public NativeArray<MuffleRayResultBatch> muffleResultBatches;
    [ReadOnly][NoAlias] public float fullClarityDist;
    [ReadOnly][NoAlias] public float fullClarityHitPercentage;

    [NativeDisableParallelForRestriction]
    [ReadOnly][NoAlias] public NativeArray<DirectionRayResultBatch> directionResultBatches;

    [NativeDisableParallelForRestriction]
    [ReadOnly][NoAlias] public NativeArray<PermeationRayResultBatch> permeationResultBatches;
    [ReadOnly][NoAlias] public float fullMuffleReductionStrength;
    [ReadOnly][NoAlias] public float muffleReductionPercent;

    [NativeDisableParallelForRestriction]
    [ReadOnly][NoAlias] public NativeArray<EchoRayResult> echoRayResults;

    [ReadOnly][NoAlias] public int batchCount;
    [ReadOnly][NoAlias] public float3 raytracerOrigin;

    [ReadOnly][NoAlias] public NativeArray<float3> audioTargetPositions;
    [ReadOnly][NoAlias] public int totalAudioTargets;

    [WriteOnly][NoAlias] public NativeArray<AudioTargetData> audioTargetSettings;



    [BurstCompile]
    public void Execute()
    {
        // Loop over all audio targets
        for (int audioTargetId = 0; audioTargetId < totalAudioTargets; audioTargetId++)
        {
            float muffleStrength = 0f;
            float3 audioPosition = float3.zero;

            float3 audioTargetPosRelativeToPlayer = audioTargetPositions[audioTargetId] - raytracerOrigin;

            // Loop over all batches bound to current audio target
            for (int batchId = 0; batchId < batchCount; batchId++)
            {
                int targetAudioTargetBatchId = audioTargetId * batchCount + batchId;

                // Add strength of current batch to muffleStrength
                muffleStrength += muffleResultBatches[targetAudioTargetBatchId].GetMuffleStrength(fullClarityDist, fullClarityHitPercentage);


                audioPosition += directionResultBatches[targetAudioTargetBatchId].GetAvgAudioPosition();


                // Get permeation rays data
                float muffleReduction = permeationResultBatches[targetAudioTargetBatchId].GetMuffleReduction(fullMuffleReductionStrength, muffleReductionPercent);

                //subtract muffleStrength by permeation muffleReduction
                muffleStrength -= muffleReduction;

                // Add audioTargets positions relative to the player * muffReduction Strength to audioPosition
                audioPosition += audioTargetPosRelativeToPlayer * muffleReduction;
            }

            audioTargetSettings[audioTargetId] = new AudioTargetData(muffleStrength, audioPosition);
        }
    }
}