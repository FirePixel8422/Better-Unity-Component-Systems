using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
public struct ProcessAudioDataJob : IJob
{
    [ReadOnly][NoAlias] public NativeArray<AudioRayResult> rayResults;
    [ReadOnly][NoAlias] public NativeArray<int> rayResultCounts;

    [ReadOnly][NoAlias] public NativeArray<float3> returnRayDirections;

    [NoAlias] public NativeArray<float3> targetReturnPositionsTotal;
    [NoAlias] public NativeArray<float3> tempTargetReturnPositions;

    [NoAlias] public NativeArray<int> targetHitCounts;

    [NoAlias] public NativeArray<int> targetReturnCounts;

    [ReadOnly][NoAlias] public NativeArray<int> muffleRayHits;

    [ReadOnly][NoAlias] public int maxRayHits;
    [ReadOnly][NoAlias] public int rayCount;
    [ReadOnly][NoAlias] public float3 rayOriginWorld;

    [ReadOnly][NoAlias] public int totalAudioTargets;

    [ReadOnly][NoAlias] public float3 listenerForwardDir;
    [ReadOnly][NoAlias] public float3 listenerRightDir;

    [WriteOnly][NoAlias] public NativeArray<AudioSettings> audioTargetSettings;



    [BurstCompile]
    public void Execute()
    {
        int totalRayResults = 0;

        for (int i = 0; i < totalAudioTargets; i++)
        {
            targetHitCounts[i] = 0;
            targetReturnPositionsTotal[i] = 0;
            tempTargetReturnPositions[i] = float3.zero;
            targetReturnCounts[i] = 0;
        }

        int resultSetSize;
        AudioRayResult result;
        int lastRayAudioTargetId;

        //collect hit counts, direction sums, and return hrtf_Positions
        for (int i = 0; i < rayCount; i++)
        {
            resultSetSize = rayResultCounts[i];

            //add result count of this raySet to totalRayResults
            totalRayResults += resultSetSize;

            for (int bounceIndex = 0; bounceIndex < resultSetSize; bounceIndex++)
            {
                result = rayResults[i * maxRayHits + bounceIndex];

                //if hitting any target increase hit count for that target id by 1
                if (result.audioTargetId != -1)
                {
                    targetHitCounts[result.audioTargetId] += 1;
                }

                //final bounce of this ray their hit targetId (could be nothing aka -1)
                lastRayAudioTargetId = rayResults[i * maxRayHits + resultSetSize - 1].audioTargetId;

                // Check if this ray got to a audiotarget and if this bounce returned to origin (non-zero return direction)
                if (lastRayAudioTargetId != -1 && math.distance(returnRayDirections[i], float3.zero) != 0)
                {
                    tempTargetReturnPositions[lastRayAudioTargetId] = result.point / (result.fullRayDistance != 0 ? result.fullRayDistance : 1) * 125 / 2;
                    targetReturnCounts[lastRayAudioTargetId] += 1;

                    break;
                }
            }

            //add last ray of every rayset that could retrace to origin
            for (int audioTargetId = 0; audioTargetId < totalAudioTargets; audioTargetId++)
            {
                targetReturnPositionsTotal[audioTargetId] += tempTargetReturnPositions[audioTargetId];

                //reset for next iteration
                tempTargetReturnPositions[audioTargetId] = float3.zero;
            }
        }

        float strength;
        float pan;
        float muffle;

        float hitFraction;
        int maxBatchSize = muffleRayHits.Length / totalAudioTargets;

        //calculate audio strength and panstero based on newly calculated data
        for (int audioTargetId = 0; audioTargetId < totalAudioTargets; audioTargetId++)
        {
            int totalMuffleRayhits = 0;

            //combine all spread muffleRayHitCount values for current audioTarget to 1 int (totalMuffleRayhits)
            for (int i = 0; i < maxBatchSize; i++)
            {
                totalMuffleRayhits += muffleRayHits[totalAudioTargets * i + audioTargetId];
            }
            //set muffleRayHits of current audiotargetId to the totalMuffleRayhits
            muffle = (float)totalMuffleRayhits / (rayCount * maxRayHits);


            //if audiotarget was hit by at least 1 ray
            if (targetHitCounts[audioTargetId] > 0)
            {
                hitFraction = (float)targetHitCounts[audioTargetId] / rayCount;

                strength = math.saturate(hitFraction * 6); // If 16% of rays hit = full volume
            }
            //no rays hit audiotarget > 0 sound
            else
            {
                strength = 0;
            }

            // If we have return hrtf_Positions, use those to compute average direction
            if (targetReturnCounts[audioTargetId] > 0)
            {
                float3 avgPos = targetReturnPositionsTotal[audioTargetId] / targetReturnCounts[audioTargetId];

                // Calculate direction from listener to sound source (target direction)
                float3 targetDir = math.normalize(rayOriginWorld - avgPos); // Direction from listener to sound source

                // Project the target direction onto the horizontal plane (ignore y-axis)
                targetDir.y = 0f;

                // Calculate pan as a value between -1 (left) and 1 (right)
                pan = math.clamp(math.dot(targetDir, listenerRightDir), -1, 1);
            }
            else
            {
                //set value to -2, telling the AudioRatracer to manually calculate the pan with audiotarget - transformPosition
                pan = -2;
            }

            //update the audioTargetSettings for this audiotarget
            audioTargetSettings[audioTargetId] = new AudioSettings(strength, muffle, pan);
        }
    }
}