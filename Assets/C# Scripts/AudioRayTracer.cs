using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Jobs;


[BurstCompile]
public class AudioRayTracer : MonoBehaviour
{
    [SerializeField] private float3 rayOrigin;

    [Range(1, 10000)]
    [SerializeField] int rayCount = 1000;

    [Range(0, 25)]
    [SerializeField] int maxBounces = 3;

    [Range(0, 1000)]
    [SerializeField] float maxRayDist = 10;


    private List<AudioColliderGroup> colliderGroups;

    private List<AudioTargetRT> audioTargets;
    private NativeArray<float3> audioTargetPositions;

    private NativeArray<ColliderAABBStruct> AABBColliders;
    private int AABBCount;

    private NativeArray<ColliderOBBStruct> OBBColliders;
    private int OBBCount;

    private NativeArray<ColliderSphereStruct> sphereColliders;
    private int sphereCount;

    private NativeArray<float3> rayDirections;

    private NativeArray<AudioRayResult> rayResults;
    private NativeArray<int> rayResultCounts;

    private NativeArray<float3> returnRayDirections;

    private NativeArray<int> muffleRayHits;



    [BurstCompile]
    private void Start()
    {
        InitializeAudioRaytraceSystem();

#if UNITY_EDITOR
        sw = new System.Diagnostics.Stopwatch();
#endif

        UpdateScheduler.Register(OnUpdate);
    }


    #region Setup Raytrace System and data Methods

    [BurstCompile]
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

        rayResults = new NativeArray<AudioRayResult>(maxRayResultsArrayLength, Allocator.Persistent);
        returnRayDirections = new NativeArray<float3>(maxRayResultsArrayLength, Allocator.Persistent);
        rayResultCounts = new NativeArray<int>(rayCount, Allocator.Persistent);

        SetupColliderData();
        SetupAudioTargetData();

        mainJobHandle.Complete();
    }

    [BurstCompile]
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

    [BurstCompile]
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

        targetHitCounts = new NativeArray<int>(audioTargetCount, Allocator.Persistent);
        targetReturnPositionsTotal = new NativeArray<float3>(audioTargetCount, Allocator.Persistent);
        tempTargetReturnPositions = new NativeArray<float3>(audioTargetCount, Allocator.Persistent);
        targetReturnCounts = new NativeArray<int>(audioTargetCount, Allocator.Persistent);

        audioTargetSettings = new NativeArray<AudioSettings>(audioTargetCount, Allocator.Persistent);

        muffleRayHits = new NativeArray<int>(audioTargetCount * maxBatchCount, Allocator.Persistent);
    }

    #endregion




    [Header("WARNING: If false will block the main thread every frame until all rays are calculated")]
    [SerializeField] private bool waitForJobCompletion = true;

    [SerializeField] private int batchSize = 4048;
    [SerializeField] private int maxBatchCount = 3;

    private AudioRayTracerJobParallelBatched audioRayTraceJob;
    private ProcessAudioDataJob calculateAudioTargetDataJob;
    private JobHandle mainJobHandle;

    private NativeArray<int> targetHitCounts;
    private NativeArray<float3> targetReturnPositionsTotal;
    private NativeArray<float3> tempTargetReturnPositions;
    private NativeArray<int> targetReturnCounts;

    private NativeArray<AudioSettings> audioTargetSettings;

    [BurstCompile]
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
            //recreate rayResults and returnRayDirections arrays with new size because maxBounces or rayCount changed
            rayResults = new NativeArray<AudioRayResult>(rayCount * (maxBounces + 1), Allocator.Persistent);
            returnRayDirections = new NativeArray<float3>(rayCount * (maxBounces + 1), Allocator.Persistent);

            if (rayDirections.Length != rayCount)
            {
                //reculcate ray directions and resize rayResultCounts if rayCount changed
                rayDirections = new NativeArray<float3>(rayCount, Allocator.Persistent);
                rayResultCounts = new NativeArray<int>(rayCount, Allocator.Persistent);

                var generateDirectionsJob = new FibonacciDirectionsJobParallel
                {
                    directions = rayDirections
                };

                generateDirectionsJob.Schedule(rayCount, 64).Complete();

                Debug.LogWarning("You changed the rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated rayDirections array with new capacity.");
            }

            Debug.LogWarning("You changed the max bounces/rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated rayResults array with new capacity.");
        }

        if (rayResults.IsCreated && (drawRayHitsGizmos || drawRayTrailsGizmos))
        {
            DEBUG_rayResults = rayResults.ToArray();
            DEBUG_rayResultCounts = rayResultCounts.ToArray();

            DEBUG_returnRayDirections = returnRayDirections.ToArray();
            DEBUG_muffleRayHits = muffleRayHits.ToArray();
        }
#endif

        //trigger an update for all audio targets with ray traced data
        UpdateAudioTargets();

        #region Raycasting Job ParallelBatched

        //create raytrace job and fire it
        audioRayTraceJob = new AudioRayTracerJobParallelBatched
        {
            rayOrigin = (float3)transform.position + rayOrigin,
            rayDirections = rayDirections,

            AABBColliders = AABBColliders,
            OBBColliders = OBBColliders,
            sphereColliders = sphereColliders,

            audioTargetPositions = audioTargetPositions,

            maxRayHits = maxBounces + 1,
            maxRayDist = maxRayDist,
            totalAudioTargets = audioTargets.Count,

            results = rayResults,
            resultCounts = rayResultCounts,

            returnRayDirections = returnRayDirections,
            
            muffleRayHits = muffleRayHits,
        };

        // Calculate how many batches this job would run normally
        int totalBatches = (int)math.ceil((float)rayCount / batchSize);

        // Clamp to the maxBatchCount to prevent overflow
        totalBatches = math.min(totalBatches, maxBatchCount);

        mainJobHandle = audioRayTraceJob.Schedule(rayCount, batchSize * totalBatches);

        #endregion


        #region Calculate Audio Target Data Job

        float3 listenerForward = math.normalize(transform.forward); // The direction the listener is facing (usually the player's forward direction)
        float3 listenerRight = math.normalize(transform.right); // Right direction (for stereo pan)

        calculateAudioTargetDataJob = new ProcessAudioDataJob
        {
            rayResults = rayResults,
            rayResultCounts = rayResultCounts,

            returnRayDirections = returnRayDirections,

            targetReturnPositionsTotal = targetReturnPositionsTotal,
            tempTargetReturnPositions = tempTargetReturnPositions,

            targetHitCounts = targetHitCounts,
            targetReturnCounts = targetReturnCounts,

            maxRayHits = maxBounces + 1,
            rayCount = rayCount,
            rayOriginWorld = (float3)transform.position + rayOrigin,

            totalAudioTargets = audioTargets.Count,

            listenerForwardDir = listenerForward,
            listenerRightDir = listenerRight,

            audioTargetSettings = audioTargetSettings,
            muffleRayHits = muffleRayHits,
        };

        //start job and give mainJobHandle dependency, so it only start after the raytrace job is done.
        //update mainJobHandle to include this new job for its completion signal
        mainJobHandle = JobHandle.CombineDependencies(mainJobHandle, calculateAudioTargetDataJob.Schedule(mainJobHandle));

        #endregion
    }


    [BurstCompile]
    private void UpdateAudioTargets()
    {
        int totalAudioTargets = audioTargets.Count;

        //update audio targets
        for (int audioTargetId = 0; audioTargetId < totalAudioTargets; audioTargetId++)
        {
            AudioSettings settings = audioTargetSettings[audioTargetId];

            if (settings.panStereo == -2)
            {
                // Calculate direction from listener to sound source (target direction)
                float3 targetDir = math.normalize(audioTargets[audioTargetId].transform.position - transform.position - (Vector3)rayOrigin); // Direction from listener to sound source

                // Project the target direction onto the horizontal plane (ignore y-axis)
                targetDir.y = 0f;

                // Calculate pan as a value between -1 (left) and 1 (right)
                settings.panStereo = math.clamp(math.dot(targetDir, transform.right), -1, 1);
            }

            audioTargets[audioTargetId].UpdateAudioSource(settings);
        }
    }




    [BurstCompile]
    private void OnDestroy()
    {
        // Force complete all jobs
        mainJobHandle.Complete();

        // Ray arrays
        DisposeArray(ref rayDirections);
        DisposeArray(ref rayResults);
        DisposeArray(ref rayResultCounts);
        DisposeArray(ref muffleRayHits);

        // Collider arrays
        DisposeArray(ref AABBColliders);
        DisposeArray(ref OBBColliders);
        DisposeArray(ref sphereColliders);

        // Audio arrays
        DisposeArray(ref targetHitCounts);
        DisposeArray(ref targetReturnPositionsTotal);
        DisposeArray(ref tempTargetReturnPositions);
        DisposeArray(ref targetReturnCounts);
        DisposeArray(ref audioTargetPositions);
        DisposeArray(ref audioTargetSettings);

        // Unregister update scheduler
        UpdateScheduler.Unregister(OnUpdate);
    }

    // Helper function to safely dispose arrays
    private void DisposeArray<T>(ref NativeArray<T> array) where T : struct
    {
        if (array.IsCreated)
            array.Dispose();
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


    private AudioRayResult[] DEBUG_rayResults;
    private int[] DEBUG_rayResultCounts;

    private float3[] DEBUG_returnRayDirections;

    [SerializeField] private int[] DEBUG_muffleRayHits;

    [SerializeField] private uint[] hashes; 

    [SerializeField] private float ms;
    private System.Diagnostics.Stopwatch sw;


    [BurstCompile]
    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false) return;

        float3 rayOrigin = (float3)transform.position + this.rayOrigin;


        if (DEBUG_rayResults != null && DEBUG_rayResults.Length != 0 && (drawRayHitsGizmos || drawRayTrailsGizmos || drawReturnRayDirectionGizmos || drawReturnRaysAvgDirectionGizmos))
        {
            AudioRayResult prevResult = AudioRayResult.Null;

            int maxRayHits = DEBUG_rayResults.Length / DEBUG_rayResultCounts.Length;

            int setResultAmountsCount = DEBUG_rayResultCounts.Length;

            int cSetResultCount;

            float3 returningRayDir;
            float3 lastReturningRayOrigin;

            float3 lastReturningRayOriginTotal = float3.zero;
            int lastReturningRayOriginsCount = 0;

            if (setResultAmountsCount * maxRayHits > 5000)
            {
                Debug.LogWarning("Max Gizmos Reached (5k) please turn of gizmos to not fry CPU");

                setResultAmountsCount = 5000 / maxRayHits;
            }

            for (int i = 0; i < setResultAmountsCount; i++)
            {
                cSetResultCount = DEBUG_rayResultCounts[i];
                prevResult.point = rayOrigin;


                //ray hit markers and trails
                for (int i2 = 0; i2 < cSetResultCount; i2++)
                {
                    AudioRayResult result = DEBUG_rayResults[i * maxRayHits + i2];

                    Gizmos.color = rayHitColor;

                    if (drawRayHitsGizmos)
                    {
                        Gizmos.DrawWireCube(result.point, Vector3.one * 0.1f);
                    }

                    Gizmos.color = rayTrailColor;

                    if (drawRayTrailsGizmos)
                    {
                        Gizmos.DrawLine(prevResult.point, result.point);
                        prevResult = result;
                    }
                }


                lastReturningRayOrigin = float3.zero;

                //return to origin rays of each ray and avg direction of all rays last visible player ray
                if (drawReturnRayDirectionGizmos || drawReturnRaysAvgDirectionGizmos)
                {
                    Gizmos.color = rayReturnDirectionColor;

                    //get all ray origins that returned to the original origin and save last ray that did this
                    for (int i2 = 0; i2 < maxRayHits; i2++)
                    {
                        returningRayDir = DEBUG_returnRayDirections[i * maxRayHits + i2];

                        if (cSetResultCount != 0 && DEBUG_rayResults[i * maxRayHits + cSetResultCount - 1].audioTargetId == 0 && math.distance(returningRayDir, float3.zero) != 0)
                        {
                            lastReturningRayOrigin = DEBUG_rayResults[i * maxRayHits + i2].point;

                            if (drawReturnRayDirectionGizmos)
                            {
                                Gizmos.color = rayReturnDirectionColor;
                                Gizmos.DrawLine(rayOrigin, lastReturningRayOrigin);
                            }
                        }
                    }

                    //draw last ray that returned to origin and add its origin to lastReturningRayOriginTotal
                    if (math.distance(lastReturningRayOrigin, float3.zero) != 0)
                    {
                        lastReturningRayOriginTotal += lastReturningRayOrigin;
                        lastReturningRayOriginsCount++;

                        if (drawReturnRayLastDirectionGizmos)
                        {
                            Gizmos.color = rayReturnLastDirectionColor;
                            Gizmos.DrawLine(rayOrigin, lastReturningRayOrigin);
                        }
                    }
                }
            }

            //avg direction of all rays based on lastReturningRayOriginTotal divided by amount of ray origins added here (equal to amount of last rays that returned to origin)
            if (drawReturnRaysAvgDirectionGizmos)
            {
                Gizmos.color = rayReturnAvgDirectionColor;
                Gizmos.DrawLine(rayOrigin, rayOrigin + math.normalize(lastReturningRayOriginTotal / lastReturningRayOriginsCount - rayOrigin) * 2);
            }
        }

        //origin cube
        Gizmos.color = originColor;
        Gizmos.DrawWireSphere(rayOrigin, 0.025f);
        Gizmos.DrawWireSphere(rayOrigin, 0.05f);

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
