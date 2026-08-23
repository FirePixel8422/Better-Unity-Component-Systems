using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


public class AudioRayTracer : UpdateMonoBehaviour
{
    [SerializeField] private float3 rayOrigin;

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
        raytracerJobsStopwatch = new System.Diagnostics.Stopwatch();
        batchCycleStopwatch = new System.Diagnostics.Stopwatch();
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

        rayHitResults = new NativeArray<AudioRayHitResult>(maxRayResultsArrayLength, Allocator.Persistent);
        rayHitResultCounts = new NativeArray<byte>(rayCount, Allocator.Persistent);
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

                Debug.LogWarning("You changed the rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated mainRayDirections array with new capacity.");
            }
            Debug.LogWarning("You changed the max bounces/rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated rayResults array with new capacity.");
        }

        if (drawDebugArrays)
        {
            DEBUG_RayResults = rayHitResults.ToArray();
            DEBUG_RayResultCounts = rayHitResultCounts.ToArray();

            DEBUG_EchoRayDistances = echoRayDistances.ToArray();
            DEBUG_AudioTargetPositions = AudioTargetManager.AudioTargetPositions.JobBatchAsArray().ToArray();

            DEBUG_MaxMuffleHits = rayCount * MaxHitsPerRay;
            DEBUG_MuffleRayHits = AudioTargetManager.MuffleRayHits.ToArray();

            DEBUG_MufflePercent01 = new float[AudioTargetManager.AudioTargetCount_JobBatch];
            for (int i = 0; i < AudioTargetManager.AudioTargetCount_JobBatch; i++)
            {
                DEBUG_MufflePercent01[i] = AudioTargetManager.AudioTargetSettings.JobBatch[i].MuffleStrength;
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
            RayOrigin = (float3)transform.position + rayOrigin,
            RayDirections = mainRayDirections,

            AABBColliders = AudioColliderManager.AABBColliders.JobBatchAsArray(),
            AABBColliderCount = AudioColliderManager.AABBColliders.JobBatch.Length,

            OBBColliders = AudioColliderManager.OBBColliders.JobBatchAsArray(),
            OBBColliderCount = AudioColliderManager.OBBColliders.JobBatch.Length,

            SphereColliders = AudioColliderManager.SphereColliders.JobBatchAsArray(),
            SphereColliderCount = AudioColliderManager.SphereColliders.JobBatch.Length,

            AudioTargetPositions = AudioTargetManager.AudioTargetPositions.JobBatchAsArray(),
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
            RayOrigin = (float3)transform.position + rayOrigin,
            RayDirections = mainRayDirections,

            AABBColliders = AudioColliderManager.AABBColliders.JobBatchAsArray(),
            AABBColliderCount = AudioColliderManager.AABBColliders.JobBatch.Length,

            OBBColliders = AudioColliderManager.OBBColliders.JobBatchAsArray(),
            OBBColliderCount = AudioColliderManager.OBBColliders.JobBatch.Length,

            SphereColliders = AudioColliderManager.SphereColliders.JobBatchAsArray(),
            SphereColliderCount = AudioColliderManager.SphereColliders.JobBatch.Length,

            AudioTargetPositions = AudioTargetManager.AudioTargetPositions.JobBatchAsArray(),
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
            AudioTargetPositions = AudioTargetManager.AudioTargetPositions.JobBatchAsArray(),
            AudioTargetSettings = AudioTargetManager.AudioTargetSettings.JobBatchAsArray(),

            MuffleRayHits = AudioTargetManager.MuffleRayHits,
            MuffleEffectiveness = muffleEffectiveness,

            PermeationPowerRemains = AudioTargetManager.PermeationPowerRemains,
            PermeationStrengthPerRay = permeationStrengthPerRay,
            PermeationEffectiveness = mufflePermeationEffectiveness,

            MaxHitsPerRay = MaxHitsPerRay,
            RayCount = rayCount,
            RayOriginWorld = (float3)transform.position + rayOrigin,
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


#if UNITY_EDITOR

    [Header("DEBUG Gizmos")]
    [SerializeField] private Color originColor = Color.green;
    [Space(10)]

    [Header("DEBUG Data Arrays")]
    [SerializeField] private bool drawDebugArrays = true;

    [SerializeField] private bool drawRayHitsGizmos = true;
    [SerializeField] private Color rayHitColor = Color.cyan;

    [SerializeField] private bool drawEchoRayGizmos = true;
    [SerializeField] private Color echoRayColor = Color.cyan;

    [SerializeField] private bool drawRayTrailsGizmos;
    [SerializeField] private Color rayTrailColor = new Color(0, 1, 0, 0.15f);

    [SerializeField] private float3[] DEBUG_AudioTargetPositions;
    [SerializeField] private int DEBUG_MaxMuffleHits;
    [SerializeField] private ushort[] DEBUG_MuffleRayHits;
    [SerializeField] private float[] DEBUG_MufflePercent01;

    private AudioRayHitResult[] DEBUG_RayResults;
    private byte[] DEBUG_RayResultCounts;
    [SerializeField] private half[] DEBUG_EchoRayDistances;


    [SerializeField] private float raytracerMs;
    [SerializeField] private float batchCycleMs;
    private System.Diagnostics.Stopwatch raytracerJobsStopwatch;
    private System.Diagnostics.Stopwatch batchCycleStopwatch;


    private void OnDrawGizmos()
    {
        float3 rayOrigin = (float3)transform.position + this.rayOrigin;

        Gizmos.color = originColor;
        Gizmos.DrawWireCube(rayOrigin, Vector3.one * 0.25f);
        Gizmos.DrawWireCube(rayOrigin, Vector3.one * 0.2f);

        if (Application.isPlaying == false) return;

        if (DEBUG_RayResults.HasData() && drawDebugArrays)
        {
            float3 prevRayHitPoint = float3.zero;

            int maxRayHits = DEBUG_RayResults.Length / DEBUG_RayResultCounts.Length;
            int setResultAmountsCount = DEBUG_RayResultCounts.Length;
            int cSetResultCount;

            const int MAX_GIZMOS = 5000;

            if (setResultAmountsCount * maxRayHits > MAX_GIZMOS)
            {
                Debug.LogWarning($"Max Gizmos Reached: '{MAX_GIZMOS}', please turn of gizmos to not fry CPU");

                setResultAmountsCount = MAX_GIZMOS / maxRayHits;
            }

            for (int i = 0; i < setResultAmountsCount; i++)
            {
                cSetResultCount = DEBUG_RayResultCounts[i];
                prevRayHitPoint = (half3)rayOrigin;

                // Ray hit markers and trails
                for (int i2 = 0; i2 < cSetResultCount; i2++)
                {
                    int cRayHitId = i * maxRayHits + i2;
                    float3 cRayHitPoint = DEBUG_RayResults[cRayHitId].HitPoint;

                    if (drawRayHitsGizmos)
                    {
                        Gizmos.color = rayHitColor;
                        Gizmos.DrawWireCube(cRayHitPoint, Vector3.one * 0.1f);
                    }
                    if (drawRayTrailsGizmos)
                    {
                        Gizmos.color = rayTrailColor;
                        Gizmos.DrawLine(prevRayHitPoint, cRayHitPoint);
                        prevRayHitPoint = cRayHitPoint;
                    }
                }
            }

            for (int i = 0; i < DEBUG_RayResults.Length; i++)
            {
                if (drawEchoRayGizmos)
                {
                    if (DEBUG_EchoRayDistances[i] != 0)
                    {
                        Gizmos.color = echoRayColor;
                        Gizmos.DrawLine(rayOrigin, (float3)DEBUG_RayResults[i].HitPoint);
                    }
                }
            }
        }
    }
#endif
}