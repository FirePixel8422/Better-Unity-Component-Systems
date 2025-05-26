using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Jobs;

public class AudioRayTracer : MonoBehaviour
{
    [SerializeField] private float3 raytracerOrigin;

    [Range(1, 10000)]
    [SerializeField] int rayCount = 1000;

    [Range(0, 25)]
    [SerializeField] int maxBounces = 3;

    [Range(0, 1000)]
    [SerializeField] float maxRayDist = 10;

    [SerializeField] public float fullClarityDist;
    [SerializeField] public float fullClarityHitPercentage;

    [SerializeField] public float fullMuffleReductionStrength;
    [SerializeField] public float muffleReductionPercent;

    [SerializeField] public float distanceFalloffPerMeter;
    [SerializeField] public float permeationFalloffPerMeter;
    


    private List<AudioColliderGroup> colliderGroups;

    private NativeArray<ColliderAABBStruct> AABBColliders;
    private int AABBCount;

    private NativeArray<ColliderOBBStruct> OBBColliders;
    private int OBBCount;

    private NativeArray<ColliderSphereStruct> sphereColliders;
    private int sphereCount;


    private NativeArray<float3> rayDirections;


    private NativeArray<MuffleRayResultBatch> muffleResultBatches;

    private NativeArray<DirectionRayResult> directionResults;

    private NativeArray<float> permeationResultBatches;

    private NativeArray<float> echoRayResults;


    private List<AudioTargetRT> audioTargets;
    private NativeArray<float3> audioTargetPositions;

    private NativeArray<AudioTargetData> audioTargetSettings;



    private void OnEnable() => UpdateScheduler.Register(OnUpdate);
    private void OnDisable() => UpdateScheduler.Unregister(OnUpdate);


    private void Start()
    {
        InitializeAudioRaytraceSystem();

#if UNITY_EDITOR
        sw = new System.Diagnostics.Stopwatch();
#endif
    }


    #region Setup Raytrace System and Data

    private void InitializeAudioRaytraceSystem()
    {
        //initialize Raycast native arrays
        rayDirections = new NativeArray<float3>(rayCount, Allocator.Persistent);

        //generate sphere directions with fibonacci sphere algorithm
        var generateDirectionsJob = new FibonacciDirectionsJobParallel
        {
            directions = rayDirections
        };

        JobHandle mainJobHandle = generateDirectionsJob.Schedule(rayCount, 64);


        //do as much tasks here to give the sphere direcion job some time to complete before forcing it to complete.

        int maxRayResultsArrayLength = rayCount * (maxBounces + 1);

        directionResults = new NativeArray<DirectionRayResult>(maxRayResultsArrayLength, Allocator.Persistent);
        echoRayResults = new NativeArray<float>(maxRayResultsArrayLength, Allocator.Persistent);

        SetupColliderData();
        SetupAudioTargetData();

        //force complete direction generator job
        mainJobHandle.Complete();
    }

    
    private void SetupColliderData()
    {
        //get all collider groups
        colliderGroups = new List<AudioColliderGroup>(FindObjectsOfType<AudioColliderGroup>());

        AABBCount = 0;
        OBBCount = 0;
        sphereCount = 0;

        //calculate total amount of colliders for box and spheres
        for (int i = 0; i < colliderGroups.Count; i++)
        {
            AABBCount += colliderGroups[i].AABBCount;
            OBBCount += colliderGroups[i].OBBCount;
            sphereCount += colliderGroups[i].SphereCount;
        }

        //setup native arrays for colliders
        AABBColliders = new NativeArray<ColliderAABBStruct>(AABBCount, Allocator.Persistent);
        OBBColliders = new NativeArray<ColliderOBBStruct>(OBBCount, Allocator.Persistent);
        sphereColliders = new NativeArray<ColliderSphereStruct>(sphereCount, Allocator.Persistent);

        int cAABBId = 0;
        int cOBBId = 0;
        int cSphereId = 0;

        //assign colliders to the native arrays
        foreach (var group in colliderGroups)
        {
            group.GetColliders(AABBColliders, cAABBId, OBBColliders, cOBBId, sphereColliders, cSphereId);

            cAABBId += group.AABBCount;
            cOBBId += group.OBBCount;
            cSphereId += group.SphereCount;
        }
    }

    
    private void SetupAudioTargetData()
    {
        //get all audio targets
        audioTargets = new List<AudioTargetRT>(FindObjectsOfType<AudioTargetRT>());

        int audioTargetCount = audioTargets.Count;

        audioTargetPositions = new NativeArray<float3>(audioTargetCount, Allocator.Persistent);

        for (int i = 0; i < audioTargetCount; i++)
        {
            audioTargets[i].id = i;
            audioTargetPositions[i] = audioTargets[i].transform.position;
        }

        audioTargetSettings = new NativeArray<AudioTargetData>(audioTargetCount, Allocator.Persistent);

        muffleResultBatches = new NativeArray<MuffleRayResultBatch>(audioTargetCount * maxBatchCount, Allocator.Persistent);
        permeationResultBatches = new NativeArray<float>(audioTargetCount * maxBatchCount, Allocator.Persistent);
    }

    #endregion




    [Header("WARNING: If false will block the main thread every frame until all rays are calculated")]
    [SerializeField] private bool waitForJobCompletion = true;

    [SerializeField] private int batchSize = 4048;
    [SerializeField] private int maxBatchCount = 3;

    private AudioRayTracerJobParallelBatched audioRayTraceJob;
    private ProcessAudioDataJob calculateAudioTargetDataJob;
    private JobHandle mainJobHandle;

    
    private void OnUpdate()
    {
        //if waitForJobCompletion is true skip a frame if job is not done yet
        if (waitForJobCompletion && mainJobHandle.IsCompleted == false) return;

        mainJobHandle.Complete();


#if UNITY_EDITOR

        ms = sw.ElapsedMilliseconds;
        sw.Restart();

        //failsafe to prevent crash when updating maxBounces in editor
        if (audioRayTraceJob.rayDirections.Length != 0 && (audioRayTraceJob.maxRayHits != (maxBounces + 1) || rayDirections.Length != rayCount))
        {
            echoRayResults = new NativeArray<float>(rayCount * (maxBounces + 1), Allocator.Persistent);
            directionResults = new NativeArray<DirectionRayResult>(rayCount * (maxBounces + 1), Allocator.Persistent);

            if (rayDirections.Length != rayCount)
            {
                //reculcate ray directions and resize rayResultCounts if rayCount changed
                rayDirections = new NativeArray<float3>(rayCount, Allocator.Persistent);

                var generateDirectionsJob = new FibonacciDirectionsJobParallel
                {
                    directions = rayDirections
                };

                generateDirectionsJob.Schedule(rayCount, 64).Complete();

                Debug.LogWarning("You changed the rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated rayDirections array with new capacity.");
            }

            Debug.LogWarning("You changed the max bounces/rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated rayResults array with new capacity.");
        }

        //if (rayResults.IsCreated && (drawRayHitsGizmos || drawRayTrailsGizmos))
        //{
        //    DEBUG_rayResults = rayResults.ToArray();
        //    DEBUG_rayResultCounts = rayResultCounts.ToArray();

        //    DEBUG_returnRayDirections = returnRayDirections.ToArray();
        //    DEBUG_muffleRayHits = muffleRayHits.ToArray();
        //}
#endif

        //trigger an update for all audio targets with ray traced data
        UpdateAudioTargets();


        #region Raycasting Job ParallelBatched

        //create raytrace job and fire it
        audioRayTraceJob = new AudioRayTracerJobParallelBatched
        {
            raytracerOrigin = (float3)transform.position + raytracerOrigin,
            rayDirections = rayDirections,

            AABBColliders = AABBColliders,
            OBBColliders = OBBColliders,
            sphereColliders = sphereColliders,

            audioTargetPositions = audioTargetPositions,

            maxRayHits = maxBounces + 1,
            maxRayDist = maxRayDist,
            totalAudioTargets = audioTargets.Count,

            muffleResultBatches = muffleResultBatches,
            directionResults = directionResults,
            
            permeationResultBatches = permeationResultBatches,
            distanceFalloffPerMeter = distanceFalloffPerMeter,
            permeationFalloffPerMeter = permeationFalloffPerMeter,

            echoRayResults = echoRayResults,
        };

        // Calculate how many batches this job would run normally
        int batchCount = (int)math.ceil((float)rayCount / batchSize);

        // Clamp to the maxBatchCount to prevent overflow
        batchCount = math.min(batchCount, maxBatchCount);

        mainJobHandle = audioRayTraceJob.Schedule(rayCount, batchSize * batchCount);

        #endregion


        #region Calculate Audio Target Data Job

        float3 listenerForward = math.normalize(transform.forward); // The direction the listener is facing (usually the player's forward direction)
        float3 listenerRight = math.normalize(transform.right); // Right direction (for stereo pan)

        calculateAudioTargetDataJob = new ProcessAudioDataJob
        {
            muffleResultBatches = muffleResultBatches,
            fullClarityDist = fullClarityDist,
            fullClarityHitPercentage = fullClarityHitPercentage,

            directionResults = directionResults,

            permeationResultBatches = permeationResultBatches,
            fullMuffleReductionStrength = fullMuffleReductionStrength,
            muffleReductionPercent = muffleReductionPercent,

            echoRayResults = echoRayResults,

            batchCount = batchCount,
            rayCount = rayCount,
            raytracerOrigin = (float3)transform.position + raytracerOrigin,

            audioTargetPositions = audioTargetPositions,
            totalAudioTargets = audioTargets.Count,

            audioTargetSettings = audioTargetSettings,
        };

        // Start job and give mainJobHandle dependency, so it only start after the raytrace job is done.
        // Update mainJobHandle to include this new job for its completion signal
        mainJobHandle = JobHandle.CombineDependencies(mainJobHandle, calculateAudioTargetDataJob.Schedule(mainJobHandle));

        #endregion
    }


    
    private void UpdateAudioTargets()
    {
        int totalAudioTargets = audioTargets.Count;

        //update audio targets
        for (int audioTargetId = 0; audioTargetId < totalAudioTargets; audioTargetId++)
        {
            audioTargets[audioTargetId].UpdateAudioSource(audioTargetSettings[audioTargetId]);
        }
    }



    
    private void OnDestroy()
    {
        // Force complete all jobs
        mainJobHandle.Complete();

        // Ray arrays
        rayDirections.DisposeIfCreated();

        // Ray Result arrays
        muffleResultBatches.DisposeIfCreated();
        directionResults.DisposeIfCreated();
        permeationResultBatches.DisposeIfCreated();
        echoRayResults.DisposeIfCreated();

        // Collider arrays
        AABBColliders.DisposeIfCreated();
        OBBColliders.DisposeIfCreated();
        sphereColliders.DisposeIfCreated();

        // Audio Target arrays
        audioTargetPositions.DisposeIfCreated();
        audioTargetSettings.DisposeIfCreated();
    }




#if UNITY_EDITOR

    [Header("DEBUG")]
    [SerializeField] private bool drawColliderGizmos = true;
    [SerializeField] private bool drawRayHitsGizmos = true;
    [SerializeField] private bool drawRayTrailsGizmos;
    [SerializeField] private bool drawReturnRayDirectionGizmos;
    [SerializeField] private bool drawReturnRayLastDirectionGizmos;
    [SerializeField] private bool drawReturnRaysAvgDirectionGizmos;


    [SerializeField] private Color originColor = Color.green;

    [SerializeField] private Color rayHitColor = Color.cyan;
    [SerializeField] private Color rayTrailColor = new Color(0, 1, 0, 0.15f);

    [SerializeField] private Color rayReturnDirectionColor = new Color(0.5f, 0.25f, 0, 1f);
    [SerializeField] private Color rayReturnLastDirectionColor = new Color(1, 0.5f, 0, 1);
    [SerializeField] private Color rayReturnAvgDirectionColor = new Color(1, 0.5f, 0, 1);

    [SerializeField] private Color colliderColor = new Color(1f, 0.75f, 0.25f);

    [SerializeField] private float ms;
    private System.Diagnostics.Stopwatch sw;


    
    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false) return;

        float3 raytracerOrigin = (float3)transform.position + this.raytracerOrigin;


        //origin cube
        Gizmos.color = originColor;
        Gizmos.DrawWireSphere(raytracerOrigin, 0.025f);
        Gizmos.DrawWireSphere(raytracerOrigin, 0.05f);

        //green blue-ish color
        Gizmos.color = colliderColor;

        // Draw all colliders in the collider arrays
        if (drawColliderGizmos && AABBColliders.IsCreated)
        {
            foreach (var box in AABBColliders)
            {
                Gizmos.DrawWireCube(box.center, box.size * 2);
            }
            foreach (var box in OBBColliders)
            {
                Gizmos.DrawWireMesh(GlobalMeshes.cube, box.center, box.rotation, box.size * 2);
            }
            foreach (var sphere in sphereColliders)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }
#endif
}
