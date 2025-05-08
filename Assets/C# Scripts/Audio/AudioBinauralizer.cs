using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public class AudioBinauralizer : MonoBehaviour
{
    [SerializeField] private float stereoSeparation = 1.0f; // 0 = mono, 1 = original, >1 = exaggerated

    [SerializeField] private float3 transformPos;
    [SerializeField] private float3 listenerPos;
    [SerializeField] private float3 listenerForward;
    [SerializeField] private float3 listenerRight;
    [SerializeField] private float3 listenerUp;

    private NativeArray<float> overlapLeft;
    private NativeArray<float> overlapRight;
    private int hrirLength;

    private void OnEnable()
    {
        UpdateScheduler.Register(OnUpdate);
    }

    private void OnDisable()
    {
        UpdateScheduler.Unregister(OnUpdate);

        if (overlapLeft.IsCreated) overlapLeft.Dispose();
        if (overlapRight.IsCreated) overlapRight.Dispose();
    }

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
        hrirLength = sampleCount;

        // Create overlap buffers on first call
        if (!overlapLeft.IsCreated)
        {
            overlapLeft = new NativeArray<float>(hrirLength - 1, Allocator.Persistent);
            overlapRight = new NativeArray<float>(hrirLength - 1, Allocator.Persistent);
        }

        // Load HRIR data
        var leftEarHRIR = new NativeArray<float>(sampleCount, Allocator.Temp);
        var rightEarHRIR = new NativeArray<float>(sampleCount, Allocator.Temp);

        float3 toSource = math.normalize(transformPos - listenerPos);
        float azimuth = math.degrees(math.atan2(math.dot(toSource, listenerRight), math.dot(toSource, listenerForward)));
        float elevation = math.degrees(math.asin(math.dot(toSource, listenerUp)));

        BinauralAudioManager.GetHRIRDataForDirection(azimuth, elevation, BinauralAudioManager.hrirDatabase, ref leftEarHRIR, ref rightEarHRIR);

        // Prepare input and output buffers
        var inputLeft = new NativeArray<float>(numFrames, Allocator.Temp);
        var inputRight = new NativeArray<float>(numFrames, Allocator.Temp);
        for (int i = 0; i < numFrames; i++)
        {
            inputLeft[i] = data[i * 2];
            inputRight[i] = data[i * 2 + 1];
        }

        var processedLeft = new NativeArray<float>(numFrames, Allocator.Temp);
        var processedRight = new NativeArray<float>(numFrames, Allocator.Temp);

        // Apply convolution with overlap
        BinauralAudioManager.ConvolveWithOverlap(inputLeft, leftEarHRIR, ref overlapLeft, ref processedLeft);
        BinauralAudioManager.ConvolveWithOverlap(inputRight, rightEarHRIR, ref overlapRight, ref processedRight);

        // write back
        for (int i = 0; i < numFrames; i++)
        {
            float center = (processedLeft[i] + processedRight[i]) * 0.5f;
            float left = center + (processedLeft[i] - center) * stereoSeparation;
            float right = center + (processedRight[i] - center) * stereoSeparation;

            data[i * 2] = left;
            data[i * 2 + 1] = right;
        }

        // Dispose temp arrays
        leftEarHRIR.Dispose();
        rightEarHRIR.Dispose();
        inputLeft.Dispose();
        inputRight.Dispose();
        processedLeft.Dispose();
        processedRight.Dispose();
    }
}
