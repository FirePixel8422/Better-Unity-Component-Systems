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

        NativeArray<float> leftEarHRIR = new NativeArray<float>(BinauralAudioManager.hrirDatabase.sampleCount, Allocator.Temp);
        NativeArray<float> rightEarHRIR = new NativeArray<float>(BinauralAudioManager.hrirDatabase.sampleCount, Allocator.Temp);


        float3 toSource = transformPos- listenerPos;
        float distance = math.length(toSource);

        float2 direction = float2.zero;

        if (distance > 0.001f)
        {
            toSource = math.normalize(toSource);

            // Azimuth: angle around Y axis relative to listener forward
            float azimuth = math.degrees(math.atan2(math.dot(toSource, listenerRight), math.dot(toSource, listenerForward)));

            // Elevation: angle above/below horizontal plane relative to listener
            float elevation = math.degrees(math.asin(math.dot(toSource, listenerUp)));

            direction = new float2(azimuth, elevation);

            Debug.Log($"Azimuth: {azimuth}, Elevation: {elevation}");
        }

        BinauralAudioManager.GetHRIRDataForDirection(direction, BinauralAudioManager.hrirDatabase, ref leftEarHRIR, ref rightEarHRIR);

        int sampleCount = leftEarHRIR.Length;

        NativeArray<float> processedLeft = new NativeArray<float>(numFrames, Allocator.Temp);
        NativeArray<float> processedRight = new NativeArray<float>(numFrames, Allocator.Temp);

        // Convolution per frame (not per data.Length)
        for (int i = 0; i < numFrames; i++)
        {
            processedLeft[i] = 0f;
            processedRight[i] = 0f;

            for (int j = 0; j < sampleCount; j++)
            {
                int sampleIndex = i - j;
                if (sampleIndex >= 0)
                {
                    processedLeft[i] += data[sampleIndex * 2] * leftEarHRIR[j];
                    processedRight[i] += data[sampleIndex * 2 + 1] * rightEarHRIR[j];
                }
            }
        }

        // Write processed data back
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





    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawRay(transform.position, soundDirection);
    //    Gizmos.color = Color.green;
    //    Gizmos.DrawRay(transform.position, listenerForward);
    //    Gizmos.color = Color.blue;
    //    Gizmos.DrawRay(transform.position, listenerRight);
    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawRay(transform.position, listenerUp);
    //}
}