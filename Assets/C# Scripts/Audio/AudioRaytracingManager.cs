using Fire_Pixel.Utility;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;


public class AudioRaytracingManager : MonoBehaviour
{
    private static AudioRaytracingManager Instance;
    

    [Header("Run Raytracing async on background threads in parallel")]
    [Tooltip("WARNING: If false will block the main thread until finished")]
    [SerializeField] private bool computeAsync = true;

    [Tooltip("Max threads to use for raytrace jobs")]
    [SerializeField] private int maxThreadCount = 3;

    public static bool ComputeAsync => Instance.computeAsync;
    public static int ToUseThreadCount => math.min(Instance.maxThreadCount, JobsUtility.JobWorkerCount);

    [SerializeField] private AudioTargetManager audioTargetManager;
    public static AudioTargetManager AudioTargetManager => Instance.audioTargetManager;

    [SerializeField] private AudioColliderManager colliderManager;
    public static AudioColliderManager ColliderManager => Instance.colliderManager;




    private void Awake()
    {
        Instance = this;
        colliderManager.Init();
        audioTargetManager.Init();
    }
    private void OnDestroy()
    {
        CallbackScheduler.RegisterCallback(CallbackType.LateApplicationQuit, () =>
        {
            colliderManager.Dispose();
            audioTargetManager.Dispose();
        });
    }

    private void OnValidate()
    {
        Instance = this;
    }
    private void OnDrawGizmosSelected()
    {
        colliderManager.DrawGizmos();
    }
}
