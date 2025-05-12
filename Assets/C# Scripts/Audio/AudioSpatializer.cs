using UnityEngine;
using Unity.Mathematics;

[RequireComponent(typeof(AudioSource))]
public class AudioSpatializer : MonoBehaviour
{
    [Header("References")]
    public Transform listenerTransform;
    public Vector3 soundWorldPosition;

    [Header("Panning Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float panStrength = 1.0f;

    [Header("Rear Attenuation")]
    [Range(0f, 1f)]
    [SerializeField] private float rearAttenuationStrength = 0.5f;

    [Header("Elevation Filtering")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float minHighFreqGain = 0.85f;

    [Range(0.5f, 1.5f)]
    [SerializeField] private float maxHighFreqGain = 1.15f;

    [Range(0.5f, 1.5f)]
    [SerializeField] private float overallGain = 1.0f;

    [Header("Filter Strength")]
    [Range(0f, 1f)]
    [SerializeField] private float highFreqDampStrength = 0.3f;

    // Optional live object movement
    [Header("Optional")]
    public Transform soundPosTransform;

    // Cached values for thread-safe audio processing
    private float3 cachedLocalDir;

    // Sampling rate (this would be 44100 or 48000 for most systems)
    private int sampleRate = 44100;

    #region OnEnable/OnDisable

    private void OnEnable()
    {
        UpdateScheduler.Register(OnUpdate);
    }

    private void OnDisable()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }

    #endregion

    private void Start()
    {
        // Fetching the sample rate dynamically from Unity settings
        sampleRate = UnityEngine.AudioSettings.outputSampleRate;
    }

    private void OnUpdate()
    {
        if (soundPosTransform != null)
            soundWorldPosition = soundPosTransform.position;

        if (listenerTransform != null)
        {
            float3 worldDir = soundWorldPosition - listenerTransform.position;
            cachedLocalDir = math.normalize(listenerTransform.InverseTransformDirection(worldDir));
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels != 2 || listenerTransform == null)
            return;

        // Use cached localDir from Update()
        float3 localDir = cachedLocalDir;

        float azimuth = math.degrees(math.atan2(localDir.x, localDir.z)); // -180 to 180
        float elevation = math.degrees(math.asin(math.clamp(localDir.y, -1f, 1f))); // -90 to 90

        // Calculate panning (left and right gain)
        float pan = math.sin(math.radians(azimuth)) * panStrength;
        float leftGain = math.sqrt(0.5f * (1f - pan));
        float rightGain = math.sqrt(0.5f * (1f + pan));

        // Front/Back Attenuation based on azimuth
        float frontFactor = math.max(0f, math.cos(math.radians(azimuth)));
        float rearAtten = math.lerp(1f - rearAttenuationStrength, 1f, frontFactor);

        // Elevation-based high-frequency gain
        float elevationFactor = math.saturate((elevation + 90f) / 180f);
        float highFreqGain = math.lerp(minHighFreqGain, maxHighFreqGain, elevationFactor);

        // Calculate Interaural Time Difference (ITD) based on azimuth
        float earDistanceDifference = math.abs(localDir.x) * 2.0f; // Approximate the ear distance difference from azimuth
        float timeDelay = earDistanceDifference / 343.0f;  // 343 m/s is the speed of sound
        int delaySamples = Mathf.RoundToInt(timeDelay * sampleRate); // Convert to samples

        // Make delay more gradual when azimuth is near -90 or 90
        delaySamples = Mathf.Clamp(delaySamples, 0, Mathf.RoundToInt(sampleRate * 0.005f)); // Max 5ms delay to prevent extreme drops

        // Apply delay for the appropriate ear (left or right)
        for (int i = 0; i < data.Length; i += 2)
        {
            float leftSample = data[i];
            float rightSample = data[i + 1];

            // Simulate delay based on the azimuth (left/right)
            if (azimuth < 0) // Sound from the left
            {
                // Right ear hears the sound later
                if (delaySamples > 0)
                {
                    rightSample = leftSample; // Delay is simulated by taking the left sample for the right ear
                }
            }
            else // Sound from the right
            {
                // Left ear hears the sound later
                if (delaySamples > 0)
                {
                    leftSample = rightSample; // Delay is simulated by taking the right sample for the left ear
                }
            }

            // Apply high-frequency filtering based on elevation
            float processedLeft = (leftSample * (1f - highFreqDampStrength)) + (leftSample * highFreqGain * highFreqDampStrength);
            float processedRight = (rightSample * (1f - highFreqDampStrength)) + (rightSample * highFreqGain * highFreqDampStrength);

            // Apply panning, rear attenuation, and overall gain
            processedLeft *= leftGain * rearAtten * overallGain;
            processedRight *= rightGain * rearAtten * overallGain;

            // Write back to audio data
            data[i] = processedLeft;
            data[i + 1] = processedRight;
        }
    }
}
