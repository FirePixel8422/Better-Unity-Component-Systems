using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public class AudioBinauralizer : MonoBehaviour
{
    [SerializeField] private float3 soundDirection;

    [SerializeField] private float sampleRate = 48000f;

    private float prevLeft = 0f;
    private float prevRight = 0f;

    private NativeArray<float> leftDelayBuffer;
    private NativeArray<float> rightDelayBuffer;

    private int leftDelayIndex = 0;
    private int rightDelayIndex = 0;

    [SerializeField] private float3 listenerForward;
    [SerializeField] private float3 listenerRight;
    [SerializeField] private float3 listenerUp;


    [BurstCompile]
    private void Awake()
    {
        leftDelayBuffer = new NativeArray<float>(AudioUtility.DelayBufferSize, Allocator.Persistent);
        rightDelayBuffer = new NativeArray<float>(AudioUtility.DelayBufferSize, Allocator.Persistent);
    }


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
        listenerForward = Camera.main.transform.forward;
        listenerRight = Camera.main.transform.right;
        listenerUp = Camera.main.transform.up;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        AudioUtility.ProcessBinauralAudio(
            data, channels,
            ref prevLeft, ref prevRight,
            leftDelayBuffer, rightDelayBuffer, ref leftDelayIndex, ref rightDelayIndex,
            soundDirection, listenerForward, listenerRight, listenerUp, sampleRate
            );
    }

    private void OnDestroy()
    {
        leftDelayBuffer.DisposeIfCreated();
        rightDelayBuffer.DisposeIfCreated();
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, soundDirection);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, listenerForward);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, listenerRight);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, listenerUp);
    }
}
