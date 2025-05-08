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

    private float[] overlapLeft;
    private float[] overlapRight;
    private int hrirLength;

    private volatile bool isActive = false;



    private void OnEnable()
    {
        UpdateScheduler.Register(OnUpdate);

        int sampleCount = BinauralAudioManager.hrirDatabase.sampleCount;
        hrirLength = sampleCount;

        overlapLeft = new float[hrirLength];
        overlapRight = new float[hrirLength];

        isActive = true;
    }

    private void OnDisable()
    {
        UpdateScheduler.Unregister(OnUpdate);

        isActive = false;
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
        if (isActive == false || channels != 2)
        {
#if UNITY_EDITOR
            if (channels != 2)
            {
                Debug.LogError("Audio requires exactly 2 channels (stereo).");
            }
#endif  
            return;
        }

        int numFrames = data.Length / channels;
        int sampleCount = BinauralAudioManager.hrirDatabase.sampleCount;

        // Load HRIR data
        float[] leftEarHRIR = new float[sampleCount];
        float[] rightEarHRIR = new float[sampleCount];

        float3 toSource = math.normalize(transformPos - listenerPos);
        float azimuth = math.degrees(math.atan2(math.dot(toSource, listenerRight), math.dot(toSource, listenerForward)));
        float elevation = math.degrees(math.asin(math.dot(toSource, listenerUp)));

        BinauralAudioManager.GetHRIRDataForDirection(azimuth, elevation, leftEarHRIR, rightEarHRIR);

        // Prepare input and output buffers
        float[] inputLeft = new float[numFrames];
        float[] inputRight = new float[numFrames];

        for (int i = 0; i < numFrames; i++)
        {
            inputLeft[i] = data[i * 2];
            inputRight[i] = data[i * 2 + 1];
        }

        float[] processedLeft = new float[numFrames];
        float[] processedRight = new float[numFrames];

        // Apply convolution with overlap
        BinauralAudioManager.ConvolveWithOverlap(inputLeft, inputRight, leftEarHRIR, rightEarHRIR, overlapLeft, overlapRight, processedLeft, processedRight);


        for (int i = 0; i < numFrames; i++)
        {
            data[i * 2] = processedLeft[i];
            data[i * 2 + 1] = processedRight[i];
        }
    }
}
