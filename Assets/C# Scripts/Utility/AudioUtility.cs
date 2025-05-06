using Unity.Collections;
using Unity.Mathematics;


public static class AudioUtility
{
    public const float SpeedOfSound = 343f;
    public const float EarSpacing = 343f;

    public const int DelayBufferSize = 128;

    private const float MinCutoff = 3000f;
    private const float MaxCutoff = 18000f;

    private const float ElevationMultiplier = 0.3f;


    private const float HalfPI = math.PI * 0.5f;
    private const float DoublePI = math.PI * 2f;


    public static void ProcessBinauralAudio(
        float[] data, int channels,
        ref float prevLeft, ref float prevRight,
        NativeArray<float> leftDelayBuffer, NativeArray<float> rightDelayBuffer, ref int leftDelayIndex, ref int rightDelayIndex,
        float3 soundDirection, float3 listenerForward, float3 listenerRight, float3 listenerUp, float sampleRate)
    {
        if (channels != 2) return;

        float3 dir = math.lengthsq(soundDirection) > 0.0001f ? math.normalize(soundDirection) : listenerForward;

        // Azimuth in listener's horizontal plane
        float3 projectedDir = dir - math.dot(dir, listenerUp) * listenerUp;
        projectedDir = math.normalize(projectedDir);

        float azimuth = math.atan2(math.dot(listenerRight, projectedDir), math.dot(listenerForward, projectedDir));

        // Simple L/R panning gain
        float gainLeft = 0.5f * (1.0f + math.cos(azimuth));
        float gainRight = 0.5f * (1.0f - math.cos(azimuth));

        // ITD: Interaural Time Difference
        float itdSeconds = (EarSpacing * math.dot(listenerRight, dir)) / SpeedOfSound;
        int sampleDelay = (int)math.round(itdSeconds * sampleRate);
        sampleDelay = math.clamp(sampleDelay, -DelayBufferSize, DelayBufferSize);

        // Process mono samples into stereo
        for (int i = 0; i < data.Length; i += 2)
        {
            float sample = data[i]; // mono source

            int leftIndex = (leftDelayIndex + sampleDelay + DelayBufferSize) % DelayBufferSize;
            int rightIndex = (rightDelayIndex - sampleDelay + DelayBufferSize) % DelayBufferSize;

            leftDelayBuffer[leftIndex] = sample * gainLeft;
            rightDelayBuffer[rightIndex] = sample * gainRight;

            data[i] = leftDelayBuffer[leftDelayIndex];
            data[i + 1] = rightDelayBuffer[rightDelayIndex];

            leftDelayIndex = (leftDelayIndex + 1) % DelayBufferSize;
            rightDelayIndex = (rightDelayIndex + 1) % DelayBufferSize;
        }
    }
}