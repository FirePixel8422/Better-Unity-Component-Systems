using UnityEngine;
using Unity.Mathematics;
using System;
using System.Runtime.CompilerServices;

[RequireComponent(typeof(AudioSource))]
public class AudioSpatializer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform listenerTransform;

    [Header("Panning Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float panStrength = 1.0f;

    [Header("Rear Attenuation")]
    [Range(0f, 1f)]
    [SerializeField] private float rearAttenuationStrength = 0.5f;

    [Range(0.25f, 10f)]
    [SerializeField] private float overallGain = 1.0f;

    [Header("Distance Based Panning")]
    [SerializeField] private bool distanceBasedPanning = false;
    [SerializeField] private float maxPanDistance = 5f;

    [Header("Rear Attenuation Distance")]
    [SerializeField] private bool distanceBasedRearAttenuation = false;
    [SerializeField] private float maxRearAttenuationDistance = 10f;

    [Header("Elevation Influence Falloff And Freq Effect")]
    [Range(1f, 50f)]
    [SerializeField] private float maxElevationEffectDistance = 15f;

    [Space(5)]

    [SerializeField] private float maxLowPassCutoff = 22000f;
    [SerializeField] private float minLowPassCutoff = 5000f;
    [SerializeField] private float minHighPassCutoff = 20f;
    [SerializeField] private float maxHighPassCutoff = 500f;

    [Space(5)]

    [Range(0f, 2f)]
    [SerializeField] private float lowPassVolume = 0.85f; // Volume reduction when applying lowpass (below horizon)
    [Range(0f, 2f)]
    [SerializeField] private float highPassVolume = 0.85f; // Volume reduction when applying highpass (above horizon)

    [Header("Muffle Effect")]
    [Range(0f, 1f)]
    [SerializeField] private float muffleStrength = 0f;
    [SerializeField] private float maxMuffleCutoff = 22000f;
    [SerializeField] private float minMuffleCutoff = 1000f;

    [Header("Reverb Effect")]
    [SerializeField] private float reverbTime = 0.3f;   // seconds
    [Range(0, 1)]
    [SerializeField] private float reverbDecay = 0.5f; // 0 = no reverb, 1 = infinite sustain

    private const int ReverbTapCount = 4;
    private float[][] reverbBufferLeft;
    private float[][] reverbBufferRight;
    private int[] reverbBufferIndices;
    private int[] reverbBufferSizes;

    private float3 cachedLocalDir;
    private float3 listenerPosition;

    private float3 soundOffsetToPlayer;

    // Filter state
    private float previousLeftLP;
    private float previousRightLP;
    private float previousLeftHP;
    private float previousRightHP;
    private float previousLeftInput;
    private float previousRightInput;

    // New muffle pass filter state
    private float previousLeftMuffle;
    private float previousRightMuffle;

    private int sampleRate;



    private void OnEnable() => UpdateScheduler.Register(OnUpdate);
    private void OnDisable() => UpdateScheduler.Unregister(OnUpdate);


    private void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;

        // Initialize reverb buffers
        reverbBufferLeft = new float[ReverbTapCount][];
        reverbBufferRight = new float[ReverbTapCount][];
        reverbBufferIndices = new int[ReverbTapCount];
        reverbBufferSizes = new int[ReverbTapCount];

        Unity.Mathematics.Random rand = new Unity.Mathematics.Random(1234);

        for (int i = 0; i < ReverbTapCount; i++)
        {
            float tapDelaySec = reverbTime * (0.3f + 0.7f * rand.NextFloat());
            int bufferSize = Mathf.CeilToInt(sampleRate * tapDelaySec);

            reverbBufferSizes[i] = bufferSize;
            reverbBufferLeft[i] = new float[bufferSize];
            reverbBufferRight[i] = new float[bufferSize];
            reverbBufferIndices[i] = 0;
        }
    }

    private void OnUpdate()
    {
        if (listenerTransform != null)
        {
            listenerPosition = listenerTransform.position;

            float3 worldDir = soundOffsetToPlayer + listenerPosition;
            cachedLocalDir = math.normalize(listenerTransform.InverseTransformDirection(worldDir));
        }
    }

    public void UpdateSettings(AudioTargetData settings)
    {
        muffleStrength = settings.muffle;
        soundOffsetToPlayer = settings.position;
    }


    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels != 2)
            return;

        float3 localDir = cachedLocalDir;
        float distanceToListener = math.length(soundOffsetToPlayer);
        float azimuth = math.degrees(math.atan2(localDir.x, localDir.z));

        float effectivePanStrength = panStrength;
        if (distanceBasedPanning)
        {
            float distanceFactor = math.saturate(distanceToListener / maxPanDistance);
            effectivePanStrength *= distanceFactor;
        }

        float pan = math.sin(math.radians(azimuth)) * effectivePanStrength;
        float leftGain = math.sqrt(0.5f * (1f - pan));
        float rightGain = math.sqrt(0.5f * (1f + pan));

        float frontFactor = math.max(0f, math.cos(math.radians(azimuth)));
        float rearAtten = math.lerp(1f - rearAttenuationStrength, 1f, frontFactor);

        if (distanceBasedRearAttenuation)
        {
            rearAtten = math.clamp(rearAtten * math.saturate(1f - (distanceToListener / maxRearAttenuationDistance)), 1f - rearAttenuationStrength, 1f);
        }

        float volumeFalloff;
        if (localDir.y <= 0f)
        {
            volumeFalloff = math.lerp(1f, lowPassVolume, math.saturate(-localDir.y));
        }
        else
        {
            volumeFalloff = math.lerp(1f, highPassVolume, math.saturate(localDir.y));
        }

        for (int i = 0; i < data.Length; i += 2)
        {
            float leftSample = data[i];
            float rightSample = data[i + 1];

            float processedLeft = leftSample * leftGain * rearAtten * overallGain * volumeFalloff;
            float processedRight = rightSample * rightGain * rearAtten * overallGain * volumeFalloff;

            // Apply elevation effects
            if (localDir.y <= 0f)
            {
                float lowPassCutoff = math.lerp(maxLowPassCutoff, minLowPassCutoff, math.saturate(-localDir.y)) * (1f - 0.5f * math.saturate(distanceToListener / maxElevationEffectDistance));

                processedLeft = LowPass(processedLeft, ref previousLeftLP, lowPassCutoff, sampleRate);
                processedRight = LowPass(processedRight, ref previousRightLP, lowPassCutoff, sampleRate);
            }
            else
            {
                float highPassCutoff = math.lerp(minHighPassCutoff, maxHighPassCutoff, math.saturate(localDir.y)) * (1f + 0.5f * math.saturate(distanceToListener / maxElevationEffectDistance));

                processedLeft = HighPass(processedLeft, ref previousLeftInput, ref previousLeftHP, highPassCutoff, sampleRate);
                processedRight = HighPass(processedRight, ref previousRightInput, ref previousRightHP, highPassCutoff, sampleRate);
            }

            // Apply muffle effect
            if (muffleStrength > 0f)
            {
                float muffleCutoff = math.lerp(maxMuffleCutoff, minMuffleCutoff, muffleStrength);
                processedLeft = LowPass(processedLeft, ref previousLeftMuffle, muffleCutoff, sampleRate);
                processedRight = LowPass(processedRight, ref previousRightMuffle, muffleCutoff, sampleRate);
            }

            // Apply reverb effect
            for (int tap = 0; tap < ReverbTapCount; tap++)
            {
                int idx = reverbBufferIndices[tap];

                float delayedLeft = reverbBufferLeft[tap][idx];
                float delayedRight = reverbBufferRight[tap][idx];

                processedLeft += delayedLeft * reverbDecay;
                processedRight += delayedRight * reverbDecay;

                reverbBufferLeft[tap][idx] = processedLeft;
                reverbBufferRight[tap][idx] = processedRight;

                reverbBufferIndices[tap] = (idx + 1) % reverbBufferSizes[tap];
            }

            data[i] = processedLeft;
            data[i + 1] = processedRight;
        }
    }


    private const float DoublePI = 2f * math.PI;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float LowPass(float input, ref float previousOutput, float cutoff, float sampleRate)
    {
        float RC = 1.0f / (cutoff * DoublePI);
        float dt = 1.0f / sampleRate;
        float alpha = dt / (RC + dt);
        previousOutput += alpha * (input - previousOutput);
        return previousOutput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float HighPass(float input, ref float previousInput, ref float previousOutput, float cutoff, float sampleRate)
    {
        float RC = 1.0f / (cutoff * DoublePI);
        float dt = 1.0f / sampleRate;
        float alpha = RC / (RC + dt);
        float output = alpha * (previousOutput + input - previousInput);
        previousInput = input;
        previousOutput = output;
        return output;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(listenerPosition + soundOffsetToPlayer, 0.5f);
    }
}
