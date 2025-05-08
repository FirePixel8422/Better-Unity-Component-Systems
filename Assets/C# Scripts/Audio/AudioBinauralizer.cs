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

    #region Event register and unregister with OnEnable and OnDisable

    private void OnEnable()
    {
        UpdateScheduler.Register(OnUpdate);
    }

    private void OnDisable()
    {
        UpdateScheduler.Unregister(OnUpdate);
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

        // Correct projection: remove vertical component relative to listener's up
        float3 toSourceFlat = toSource - math.dot(toSource, listenerUp) * listenerUp;
        toSourceFlat = math.normalize(toSourceFlat);

        // Azimuth in horizontal plane
        float azimuth = math.degrees(math.atan2(math.dot(toSourceFlat, listenerRight), math.dot(toSourceFlat, listenerForward)));

        // Elevation: angle above/below horizontal plane
        float elevation = math.degrees(math.asin(math.dot(toSource, listenerUp)));



        BinauralAudioManager.GetHRIRDataForDirection(azimuth, elevation, BinauralAudioManager.hrirDatabase, ref leftEarHRIR, ref rightEarHRIR);

        var processedLeft = new NativeArray<float>(numFrames, Allocator.Temp);
        var processedRight = new NativeArray<float>(numFrames, Allocator.Temp);

        // Convolution with overlap-add
        for (int i = 0; i < numFrames; i++)
        {
            float leftSample = 0f;
            float rightSample = 0f;

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

        // Write back to audio buffer
        for (int i = 0; i < numFrames; i++)
        {
            data[i * 2] = processedLeft[i];
            data[i * 2 + 1] = processedRight[i];

            data[i * 2] = math.clamp(processedLeft[i], -1f, 1f);
            data[i * 2 + 1] = math.clamp(processedRight[i], -1f, 1f);
        }

        leftEarHRIR.Dispose();
        rightEarHRIR.Dispose();
        processedLeft.Dispose();
        processedRight.Dispose();
    }
}
