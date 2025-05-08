using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public class AudioBinauralizer : MonoBehaviour
{
    [SerializeField] private float2 soundDirection;

    [SerializeField] private float3 transformPos;
    [SerializeField] private float3 listenerPos;
    [SerializeField] private float3 listenerForward;
    [SerializeField] private float3 listenerRight;
    [SerializeField] private float3 listenerUp;

    // Overlap-add buffers for edges
    private NativeArray<float> overlapLeft;
    private NativeArray<float> overlapRight;
    private int overlapCount;

    #region Event register and unregister with OnEnable and OnDisable

    private void OnEnable()
    {
        UpdateScheduler.Register(OnUpdate);
        // Initialize overlap buffers with max possible size (sampleCount)
        int sampleCount = BinauralAudioManager.hrirDatabase.sampleCount;
        overlapLeft = new NativeArray<float>(sampleCount, Allocator.Persistent);
        overlapRight = new NativeArray<float>(sampleCount, Allocator.Persistent);
        overlapCount = 0;
    }
    private void OnDisable()
    {
        UpdateScheduler.Unregister(OnUpdate);
        overlapLeft.DisposeIfCreated();
        overlapRight.DisposeIfCreated();
    }

    #endregion

    private void OnUpdate()
    {
        transformPos = transform.position;

        listenerPos = Camera.main.transform.position;
        listenerForward = Camera.main.transform.forward;
        listenerRight = Camera.main.transform.right;
        listenerUp = Camera.main.transform.up;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels != 2)
        {
            Debug.LogError("Audio requires exactly 2 channels (stereo).");
            return;
        }

        int numFrames = data.Length / channels;
        int sampleCount = BinauralAudioManager.hrirDatabase.sampleCount;

        var leftEarHRIR = new NativeArray<float>(sampleCount, Allocator.Temp);
        var rightEarHRIR = new NativeArray<float>(sampleCount, Allocator.Temp);

        float3 toSource = transformPos - listenerPos;
        toSource = math.normalize(toSource);

        // Azimuth and elevation
        float azimuth = math.degrees(math.atan2(math.dot(toSource, listenerRight), math.dot(toSource, listenerForward)));
        float elevation = math.degrees(math.asin(math.dot(toSource, listenerUp)));

        BinauralAudioManager.GetHRIRDataForDirection(azimuth, elevation, BinauralAudioManager.hrirDatabase, ref leftEarHRIR, ref rightEarHRIR);

        var processedLeft = new NativeArray<float>(numFrames, Allocator.Temp);
        var processedRight = new NativeArray<float>(numFrames, Allocator.Temp);

        // Convolution with overlap-add
        for (int i = 0; i < numFrames; i++)
        {
            float leftSample = 0f;
            float rightSample = 0f;
            // add overlap from previous block
            if (i < overlapCount)
            {
                leftSample += overlapLeft[i];
                rightSample += overlapRight[i];
            }
            // convolution
            for (int j = 0; j < sampleCount; j++)
            {
                int frameIndex = i - j;
                if (frameIndex >= 0)
                {
                    leftSample += data[frameIndex * 2] * leftEarHRIR[j];
                    rightSample += data[frameIndex * 2 + 1] * rightEarHRIR[j];
                }
            }
            processedLeft[i] = leftSample;
            processedRight[i] = rightSample;
        }

        // prepare new overlap
        overlapCount = math.min(sampleCount, numFrames);
        for (int i = 0; i < overlapCount; i++)
        {
            overlapLeft[i] = processedLeft[numFrames - overlapCount + i];
            overlapRight[i] = processedRight[numFrames - overlapCount + i];
        }

        // write back
        for (int i = 0; i < numFrames; i++)
        {
            data[i * 2] = processedLeft[i];
            data[i * 2 + 1] = processedRight[i];
        }

        leftEarHRIR.Dispose();
        rightEarHRIR.Dispose();
        processedLeft.Dispose();
        processedRight.Dispose();
    }
}
