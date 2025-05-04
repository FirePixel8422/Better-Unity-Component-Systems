using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioBinauralizer : MonoBehaviour
{
    public Transform listenerTransform;  // Listener's transform (e.g., player's head)
    public Vector3 worldDirection;      // Direction vector from listener to sound source

    private float sampleRate;
    private float delayLeftSamples;
    private float delayRightSamples;
    private float ildLeft;
    private float ildRight;

    private float[] leftDelayBuffer;
    private float[] rightDelayBuffer;
    private int delayBufferSize;

    private void Start()
    {
        sampleRate = UnityEngine.AudioSettings.outputSampleRate;

        // Allocate delay buffers for left and right channels (1 second max delay)
        delayBufferSize = Mathf.CeilToInt(sampleRate);  // Maximum delay size of 1 second
        leftDelayBuffer = new float[delayBufferSize];
        rightDelayBuffer = new float[delayBufferSize];

        UpdateScheduler.Register(OnUpdate);
    }

    private void OnUpdate()
    {
        // Calculate the direction to the listener in local space
        Vector3 localDir = listenerTransform.InverseTransformDirection(worldDirection.normalized);

        // Horizontal angle (azimuth)
        float horizontalAngle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;

        // Vertical angle (elevation)
        float verticalAngle = Mathf.Asin(localDir.y) * Mathf.Rad2Deg;  // Y = sin(?) -> ? = arcsin(Y)

        // Compute ITD based on horizontal angle (left-right)
        float maxITD = 0.0007f;  // Max ITD in seconds (adjust as needed)
        float itd = Mathf.Sin(horizontalAngle * Mathf.Deg2Rad) * maxITD;

        // Compute delay for left and right channels based on horizontal angle
        delayLeftSamples = Mathf.Max(0, -itd * sampleRate);  // Left ear gets negative ITD
        delayRightSamples = Mathf.Max(0, itd * sampleRate);   // Right ear gets positive ITD

        // Adjust ILD based on both horizontal and vertical angles
        ildLeft = Mathf.Clamp01(1.0f - 0.5f * (Mathf.Sin(horizontalAngle * Mathf.Deg2Rad) + 1));
        ildRight = Mathf.Clamp01(1.0f - 0.5f * (-Mathf.Sin(horizontalAngle * Mathf.Deg2Rad) + 1));

        // Adjust ILD based on vertical angle (Above/Below effect)
        // Sounds above or below the listener will be quieter
        float maxVerticalEffect = 0.5f;  // How much vertical angle affects ILD (adjust as needed)
        float verticalFactor = Mathf.Abs(verticalAngle) / 90f;  // Normalize vertical angle to [0, 1]

        // Apply more attenuation for sounds above or below
        ildLeft *= Mathf.Lerp(1.0f, 1.0f - maxVerticalEffect, verticalFactor);
        ildRight *= Mathf.Lerp(1.0f, 1.0f - maxVerticalEffect, verticalFactor);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels != 2)
            return;

        // Loop through the audio samples and apply delay and ILD
        for (int i = 0; i < data.Length; i += 2)
        {
            float left = data[i];
            float right = data[i + 1];

            // Apply delay to left and right channels based on ITD
            int leftIndex = Mathf.FloorToInt(i / 2f - delayLeftSamples);
            int rightIndex = Mathf.FloorToInt(i / 2f - delayRightSamples);

            // Make sure indices are within bounds
            leftIndex = Mathf.Clamp(leftIndex, 0, delayBufferSize - 1);
            rightIndex = Mathf.Clamp(rightIndex, 0, delayBufferSize - 1);

            // Store the new samples in the delay buffer
            leftDelayBuffer[leftIndex] = left;
            rightDelayBuffer[rightIndex] = right;

            // Read the delayed samples from the buffer
            float delayedLeft = leftDelayBuffer[leftIndex];
            float delayedRight = rightDelayBuffer[rightIndex];

            // Apply ILD (Interaural Level Difference)
            data[i] = delayedLeft * ildLeft;
            data[i + 1] = delayedRight * ildRight;
        }
    }

    private void OnDestroy()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }
}
