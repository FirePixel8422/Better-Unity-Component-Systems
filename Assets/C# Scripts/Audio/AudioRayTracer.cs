using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


public class AudioRayTracer : UpdateMonoBehaviour
{
    [SerializeField] private float3 rayOriginOffset;
    [EditorReadOnly] public float3 RayOrigin => (float3)transform.position + rayOriginOffset;

    [Range(1, 5000)]
    [SerializeField] private int rayCount = 1000;

    [Range(0, 25)]
    [SerializeField] private int maxBounces = 3;
    public int MaxHitsPerRay => (maxBounces + 1);

    [Range(0, 500)]
    [Tooltip("Max distance a ray can travel")]
    [SerializeField] private float maxRayLife = 10;

    [Tooltip("Max distance a muffle hit is registered")]
    [SerializeField] private float maxMuffleHitDistance = 10;

    [Tooltip("Muffle/Permeation Effectiveness Multipliers")]
    [Range(0, 1)]
    [SerializeField] private float muffleEffectiveness = 1;
    [Range(0, 1)]
    [SerializeField] private float mufflePermeationEffectiveness = 0.5f;

    [Tooltip("Start strength per permeation ray")]
    [SerializeField] private float permeationStrengthPerRay = 1;

    [Tooltip("Max distance at which reverb will max out")]
    [SerializeField] private float maxReverbDistance = 20;

#if UNITY_EDITOR
    [EditorReadOnly] public AudioRayDebugger Debugger;

    [SerializeField, EditorReadOnly] private float raytracerMs;
    [SerializeField, EditorReadOnly] private float batchCycleMs;

    private Stopwatch raytracerJobsStopwatch;
    private Stopwatch batchCycleStopwatch;
#endif


    private NativeArray<half3> mainRayDirections;
    private NativeArray<half> echoRayDistances;

#if UNITY_EDITOR
    private NativeArray<AudioRayHitResult> rayHitResults;
    private NativeArray<byte> rayHitResultCounts;
#endif

    private JobHandle mainJobHandle;

    private AudioRaytracerJobBatched audioRayTracerJobBatched;
    private AudioPermeationJobBatched audioPermeationJobBatched;
    private ProcessAudioDataJob processAudioDataJob;


    private void Awake()
    {
        InitializeAudioRaytraceSystem();

#if UNITY_EDITOR
        raytracerJobsStopwatch = new Stopwatch();
        batchCycleStopwatch = new Stopwatch();
#endif
    }


    #region Setup Raytrace System and data Methods

    private void InitializeAudioRaytraceSystem()
    {
        //initialize Raycast native arrays
        mainRayDirections = new NativeArray<half3>(rayCount, Allocator.Persistent);

        //generate sphere directions with fibonacci sphere algorithm
        var generateDirectionsJob = new FibonacciDirectionsJobParallel
        {
            directions = mainRayDirections,
        };

        JobHandle mainJobHandle = generateDirectionsJob.Schedule(rayCount, 512);

        // Do all other tasks here to give the sphere direcion job some time to complete before forcing it to complete.
        int maxRayResultsArrayLength = rayCount * MaxHitsPerRay;

#if UNITY_EDITOR
        rayHitResults = new NativeArray<AudioRayHitResult>(maxRayResultsArrayLength, Allocator.Persistent);
        rayHitResultCounts = new NativeArray<byte>(rayCount, Allocator.Persistent);
#endif
        echoRayDistances = new NativeArray<half>(maxRayResultsArrayLength, Allocator.Persistent);

        mainJobHandle.Complete();
    }

    #endregion


    protected override void OnUpdate()
    {
        // If computeAsync is true skip a frame if job is not done yet
        if ((AudioRaytracingManager.ComputeAsync && mainJobHandle.IsCompleted == false) || AudioTargetManager.AudioTargetCount_NextBatch == 0) return;
        
        mainJobHandle.Complete();

#if UNITY_EDITOR
        batchCycleStopwatch.Restart();

        raytracerMs = raytracerJobsStopwatch.ElapsedMilliseconds;
        raytracerJobsStopwatch.Restart();
#endif

        // Trigger an update for all audio targets with ray traced data after raytrace job has finished
        AudioTargetManager.UpdateAudioTargetSettings();

#if UNITY_EDITOR
        // Failsafe to prevent crash when updating maxBounces in editor
        if (audioRayTracerJobBatched.RayDirections.Length != 0 && (audioRayTracerJobBatched.MaxHitsPerRay != MaxHitsPerRay || mainRayDirections.Length != rayCount))
        {
            // Recreate rayResults and echoRayDirections arrays with new size because maxBounces or rayCount changed
            rayHitResults = new NativeArray<AudioRayHitResult>(rayCount * MaxHitsPerRay, Allocator.Persistent);
            echoRayDistances = new NativeArray<half>(rayCount * MaxHitsPerRay, Allocator.Persistent);

            if (mainRayDirections.Length != rayCount)
            {
                // Reculcate ray directions and resize rayHitResultCounts if rayCount changed
                mainRayDirections = new NativeArray<half3>(rayCount, Allocator.Persistent);
                rayHitResultCounts = new NativeArray<byte>(rayCount, Allocator.Persistent);

                var generateDirectionsJob = new FibonacciDirectionsJobParallel
                {
                    directions = mainRayDirections
                };

                generateDirectionsJob.Schedule(rayCount, 512).Complete();

                DebugLogger.LogWarning("You changed the rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated mainRayDirections array with new capacity.");
            }
            DebugLogger.LogWarning("You changed the max bounces/rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated rayResults array with new capacity.");
        }

        if (Debugger != null && Debugger.EnableDebugging)
        {
            Debugger.RayOrigin = RayOrigin;

            Debugger.DebugData.RayResults = rayHitResults.ToArray();
            Debugger.DebugData.RayResultCounts = rayHitResultCounts.ToArray();

            Debugger.DebugData.EchoRayDistances = echoRayDistances.ToArray();
            Debugger.DebugData.AudioTargetPositions = AudioTargetManager.AudioTargetPositions.JobBatch.ToArray();

            Debugger.DebugData.MaxMuffleHits = rayCount * MaxHitsPerRay;
            Debugger.DebugData.MuffleRayHits = AudioTargetManager.MuffleRayHits.ToArray();

            Debugger.DebugData.MufflePercent01 = new float[AudioTargetManager.AudioTargetCount_JobBatch];
            for (int i = 0; i < AudioTargetManager.AudioTargetCount_JobBatch; i++)
            {
                Debugger.DebugData.MufflePercent01[i] = AudioTargetManager.AudioTargetSettings.JobBatch[i].MuffleStrength;
            }
        }
#endif

        AudioTargetManager.UpdateJobBatch();
        AudioColliderManager.UpdateJobBatch();

#if UNITY_EDITOR
        batchCycleMs = batchCycleStopwatch.ElapsedMilliseconds;
#endif

        int batchSize = (int)math.max(1, math.ceil((float)rayCount / AudioRaytracingManager.ToUseThreadCount));

        audioRayTracerJobBatched = new AudioRaytracerJobBatched
        {
            RayOrigin = RayOrigin,
            RayDirections = mainRayDirections,

            AABBColliders = AudioColliderManager.AABBColliders.JobBatch,
            AABBColliderCount = AudioColliderManager.AABBColliders.JobBatchCount,

            OBBColliders = AudioColliderManager.OBBColliders.JobBatch,
            OBBColliderCount = AudioColliderManager.OBBColliders.JobBatchCount,

            SphereColliders = AudioColliderManager.SphereColliders.JobBatch,
            SphereColliderCount = AudioColliderManager.SphereColliders.JobBatchCount,

            AudioTargetPositions = AudioTargetManager.AudioTargetPositions.JobBatch,
            TotalAudioTargets = AudioTargetManager.AudioTargetCount_JobBatch,

            MaxHitsPerRay = MaxHitsPerRay,
            MaxRayLife = maxRayLife,
            
            RayHitResults = rayHitResults,
            RayHitResultCounts = rayHitResultCounts,

            EchoRayDistances = echoRayDistances,
            
            MuffleRayHits = AudioTargetManager.MuffleRayHits,
            MaxMuffleHitDistance = maxMuffleHitDistance,
        };
        JobHandle handleA = audioRayTracerJobBatched.Schedule(rayCount, batchSize);

        audioPermeationJobBatched = new AudioPermeationJobBatched
        {
            RayOrigin = RayOrigin,
            RayDirections = mainRayDirections,

            AABBColliders = AudioColliderManager.AABBColliders.JobBatch,
            AABBColliderCount = AudioColliderManager.AABBColliders.JobBatchCount,

            OBBColliders = AudioColliderManager.OBBColliders.JobBatch,
            OBBColliderCount = AudioColliderManager.OBBColliders.JobBatchCount,

            SphereColliders = AudioColliderManager.SphereColliders.JobBatch,
            SphereColliderCount = AudioColliderManager.SphereColliders.JobBatchCount,

            AudioTargetPositions = AudioTargetManager.AudioTargetPositions.JobBatch,
            TotalAudioTargets = AudioTargetManager.AudioTargetCount_JobBatch,

            PermeationStrengthPerRay = permeationStrengthPerRay,
            PermeationPowerRemains = AudioTargetManager.PermeationPowerRemains,
        };
        handleA = JobHandle.CombineDependencies(handleA, audioPermeationJobBatched.Schedule(rayCount, batchSize));

        processAudioDataJob = new ProcessAudioDataJob
        {
            EchoRayDistances = echoRayDistances,
            MaxReverbDistance = maxReverbDistance,

            TotalAudioTargets = AudioTargetManager.AudioTargetCount_JobBatch,
            AudioTargetPositions = AudioTargetManager.AudioTargetPositions.JobBatch,
            AudioTargetSettings = AudioTargetManager.AudioTargetSettings.JobBatch,

            MuffleRayHits = AudioTargetManager.MuffleRayHits,
            MuffleEffectiveness = muffleEffectiveness,

            PermeationPowerRemains = AudioTargetManager.PermeationPowerRemains,
            PermeationStrengthPerRay = permeationStrengthPerRay,
            PermeationEffectiveness = mufflePermeationEffectiveness,

            MaxHitsPerRay = MaxHitsPerRay,
            RayCount = rayCount,
            RayOrigin = RayOrigin,
        };
        // Start job and give mainJobHandle dependency, so it only start after the raytrace job is done.
        // Update mainJobHandle to include this new job for its completion signal
        mainJobHandle = JobHandle.CombineDependencies(handleA, processAudioDataJob.Schedule(handleA));
    }


    private void OnDestroy()
    {
        // Force complete all jobs
        mainJobHandle.Complete();

        // Ray arrays
        mainRayDirections.DisposeIfCreated();
        echoRayDistances.DisposeIfCreated();

#if UNITY_EDITOR
        rayHitResults.DisposeIfCreated();
        rayHitResultCounts.DisposeIfCreated();
#endif
    }
}