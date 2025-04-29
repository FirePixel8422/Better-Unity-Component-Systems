using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Jobs;


[BurstCompile]
public class AudioRayTracer : MonoBehaviour
{
    private List<AudioColliderGroup> colliderGroups;
    private List<AudioTargetRT> audioTargets;

    private NativeArray<ColliderAABBStruct> AABBColliders;
    private int AABBCount;
    private NativeArray<int> AABBGroupIdToColliderId;
    private NativeArray<int> AABBColliderIdToGroupId;

    private NativeArray<ColliderOBBStruct> OBBColliders;
    private int OBBCount;

    private NativeArray<ColliderSphereStruct> sphereColliders;
    private int sphereCount;

    [SerializeField] private float3 rayOrigin;

    private NativeArray<float3> rayDirections;

    private NativeArray<AudioRayResult> rayResults;
    private NativeArray<int> rayResultCounts;

    private NativeArray<float3> returnRayDirections;


    [Range(1, 10000)]
    [SerializeField] int rayCount = 1000;

    [Range(1, 25)]
    [SerializeField] int maxBounces = 3;

    [Range(1, 100000)]
    [SerializeField] float maxRayDist = 10;



    [BurstCompile]
    private void Start()
    {
        InitializeAudioRaytraceSystem();

#if UNITY_EDITOR
        sw = new System.Diagnostics.Stopwatch();
#endif

        UpdateScheduler.Register(OnUpdate);
    }

    [BurstCompile]
    private void InitializeAudioRaytraceSystem()
    {
        // Initialize Raycast native arrays
        rayDirections = new NativeArray<float3>(rayCount, Allocator.Persistent);


        #region Fibonacci Sphere Method

        var generateDirectionsJob = new FibonacciDirectionsJobParallel
        {
            directions = rayDirections
        };

        JobHandle mainJobHandle = generateDirectionsJob.Schedule(rayCount, 64);

        #endregion


        //do as much tasks here to give the job some time to complete before forcing it to complete.
        rayResults = new NativeArray<AudioRayResult>(rayCount * (maxBounces + 1), Allocator.Persistent);
        returnRayDirections = new NativeArray<float3>(rayCount * (maxBounces + 1), Allocator.Persistent);
        rayResultCounts = new NativeArray<int>(rayCount, Allocator.Persistent);

        SetupColliderData();

        mainJobHandle.Complete();
    }

    [BurstCompile]
    private void SetupColliderData()
    {
        //get all collider groups
        colliderGroups = new List<AudioColliderGroup>(FindObjectsOfType<AudioColliderGroup>());
        audioTargets = new List<AudioTargetRT>(FindObjectsOfType<AudioTargetRT>());

        for (int i = 0; i < audioTargets.Count; i++)
        {
            audioTargets[i].id = i;
        }

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



    [Header("WARNING: If false will block the main thread every frame until all rays are calculated")]
    [SerializeField] private bool waitForJobCompletion = true;

    [SerializeField] private int batchSize;

    private AudioRayTraceJobParallel audioRayTraceJob;
    private JobHandle audioRayTraceJobHandle;

    [BurstCompile]
    private void OnUpdate()
    {
        //if waitForJobCompletion is true skip a frame if job is not done yet
        if (waitForJobCompletion && audioRayTraceJobHandle.IsCompleted == false) return;

        audioRayTraceJobHandle.Complete();

#if UNITY_EDITOR

        ms = sw.ElapsedMilliseconds;
        sw.Restart();

        if (AABBColliders.IsCreated && drawColliderGizmos)
        {
            DEBUG_AABBColliders = AABBColliders.ToArray();
            DEBUG_OBBColliders = OBBColliders.ToArray();
            DEBUG_sphereColliders = sphereColliders.ToArray();
        }

        if (rayResults.IsCreated && (drawRayHitsGizmos || drawRayTrailsGizmos))
        {
            DEBUG_rayResults = rayResults.ToArray();
            DEBUG_rayResultCounts = rayResultCounts.ToArray();

            DEBUG_returnRayDirections = returnRayDirections.ToArray();
        }

        //failsafe to prevent crash when updating maxBounces in editor
        if (audioRayTraceJob.rayDirections.Length != 0 && (audioRayTraceJob.maxBounces != maxBounces || rayDirections.Length != rayCount))
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
#endif

        //trigger an update for all audio targets with ray traced data
        UpdateAudioTargets();

        //create raytrace job and fire it
        audioRayTraceJob = new AudioRayTraceJobParallel
        {
            rayOrigin = (float3)transform.position + rayOrigin,
            rayDirections = rayDirections,

            AABBColliders = AABBColliders,
            OBBColliders = OBBColliders,
            sphereColliders = sphereColliders,

            maxBounces = maxBounces,
            maxRayDist = maxRayDist,

            results = rayResults,
            resultCounts = rayResultCounts,

            returnRayDirections = returnRayDirections,
        };

        //force job completion
        audioRayTraceJobHandle = audioRayTraceJob.Schedule(rayCount, batchSize);
    }

    
    [BurstCompile]
    private void UpdateAudioTargets()
    {
        int audioTargetCount = audioTargets.Count;

        int[] targetHitCounts = new int[audioTargetCount];
        int existingHitCount = 0;

        float3[] targetReturnPositionsTotal = new float3[audioTargetCount];
        float3[] tempTargetReturnPositions = new float3[audioTargetCount];
        int[] targetReturnCounts = new int[audioTargetCount];

        int totalResultSets = rayCount;
        float3 rayOriginWorld = (float3)transform.position + rayOrigin;

        // Collect hit counts, direction sums, and return positions
        for (int i = 0; i < totalResultSets; i++)
        {
            int resultSetSize = rayResultCounts[i];

            for (int i2 = 0; i2 < resultSetSize; i2++)
            {
                AudioRayResult result = rayResults[i * maxBounces + i2];

                existingHitCount += 1;

                //if hitting any target increase hit count for that target id by 1
                if (result.audioTargetId != -1)
                {
                    targetHitCounts[result.audioTargetId] += 1;
                }

                //final bounce of this ray their hit targetId (could be nothing aka -1)
                int lastRayAudioTargetId = rayResults[i * maxBounces + resultSetSize - 1].audioTargetId;

                // Check if this ray got to a audiotarget and if this bounce returned to origin (non-zero return direction)
                if (lastRayAudioTargetId != -1 && math.distance(returnRayDirections[i], float3.zero) != 0)
                {
                    tempTargetReturnPositions[lastRayAudioTargetId] = result.point;
                    targetReturnCounts[lastRayAudioTargetId]++;
                }
            }

            //add last
            for (int audioTargetId = 0; audioTargetId < audioTargetCount; audioTargetId++)
            {
                targetReturnPositionsTotal[audioTargetId] += tempTargetReturnPositions[audioTargetId];

                // Reset for next iteration
                tempTargetReturnPositions[audioTargetId] = float3.zero;
            }
        }

        for (int i = 0; i < audioTargetCount; i++)
        {
            float strength = 0f;
            float pan = 0f;

            if (targetHitCounts[i] > 0)
            {
                float hitFraction = (float)targetHitCounts[i] / (existingHitCount / maxBounces);

                strength = math.saturate(hitFraction / 0.16f); // If 16% of rays hit = full volume
            }

            // If we have return positions, use those to compute average direction
            if (targetReturnCounts[i] > 0)
            {
                float3 avgPos = targetReturnPositionsTotal[i] / targetReturnCounts[i];

                // Listener's forward and right direction
                float3 listenerForward = math.normalize(transform.forward); // The direction the listener is facing (usually the player's forward direction)
                float3 listenerRight = math.normalize(transform.right); // Right direction (for stereo pan)

                // Calculate direction from listener to sound source (target direction)
                float3 targetDir = math.normalize(avgPos - (float3)transform.position); // Direction from listener to sound source

                // Project the target direction onto the horizontal plane (ignore y-axis)
                targetDir.y = 0f;

                // Calculate pan as a value between -1 (left) and 1 (right)
                pan = math.dot(targetDir, listenerRight);

                // Clamp the pan value between -1 and 1 to avoid extreme values
                pan = math.clamp(pan, -1f, 1f);
            }

            audioTargets[i].UpdateAudioSource(strength, pan);
        }
    }




    [BurstCompile]
    private void OnDestroy()
    {
        audioRayTraceJobHandle.Complete();

        // Dispose of native arrays
        if (rayDirections.IsCreated)
            rayDirections.Dispose();

        if (rayResults.IsCreated)
            rayResults.Dispose();

        if (rayResultCounts.IsCreated)
            rayResultCounts.Dispose();

        if (AABBColliders.IsCreated)
            AABBColliders.Dispose();

        if (OBBColliders.IsCreated)
            OBBColliders.Dispose();

        if (sphereColliders.IsCreated)
            sphereColliders.Dispose();

        UpdateScheduler.Unregister(OnUpdate);
    }




#if UNITY_EDITOR

    [Header("DEBUG")]
    [SerializeField] private bool drawColliderGizmos = true;
    [SerializeField] private bool drawRayHitsGizmos = true;
    [SerializeField] private bool drawRayTrailsGizmos;
    [SerializeField] private bool drawReturnRayDirectionGizmos;
    [SerializeField] private bool drawReturnRaysAvgDirectionGizmos;


    [SerializeField] private Color originColor = Color.green;

    [SerializeField] private Color rayHitColor = Color.cyan;
    [SerializeField] private Color rayTrailColor = new Color(0, 1, 0, 0.15f);

    [SerializeField] private Color rayReturnDirectionColor = new Color(0.5f, 0.25f, 0, 1f);
    [SerializeField] private Color rayReturnLastDirectionColor = new Color(1, 0.5f, 0, 1);
    [SerializeField] private Color rayReturnAvgDirectionColor = new Color(1, 0.5f, 0, 1);

    [SerializeField] private Color colliderColor = new Color(1f, 0.75f, 0.25f);


    private ColliderAABBStruct[] DEBUG_AABBColliders;
    private ColliderOBBStruct[] DEBUG_OBBColliders;
    private ColliderSphereStruct[] DEBUG_sphereColliders;

    private AudioRayResult[] DEBUG_rayResults;
    private int[] DEBUG_rayResultCounts;

    private float3[] DEBUG_returnRayDirections;

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

            int maxBounces = DEBUG_rayResults.Length / DEBUG_rayResultCounts.Length;

            int setResultAmountsCount = DEBUG_rayResultCounts.Length;
            int cSetResultCount;

            float3 returningRayDir;
            float3 lastReturningRayOrigin;

            float3 lastReturningRayOriginTotal = float3.zero;
            int lastReturningRayOriginsCount = 0;

            if (setResultAmountsCount * maxBounces > 5000)
            {
                Debug.LogWarning("Max Gizmos Reached (5k) please turn of gizmos to not fry CPU");
                setResultAmountsCount = 5000 / maxBounces;
            }

            for (int i = 0; i < setResultAmountsCount; i++)
            {
                cSetResultCount = DEBUG_rayResultCounts[i];
                prevResult.point = rayOrigin;

                //ray hit markers and trails
                for (int i2 = 0; i2 < cSetResultCount; i2++)
                {
                    AudioRayResult result = DEBUG_rayResults[i * (maxBounces - 1) + i2];

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
                    for (int i2 = 0; i2 < maxBounces; i2++)
                    {
                        returningRayDir = DEBUG_returnRayDirections[i * maxBounces + i2];

                        if (cSetResultCount != 0 && DEBUG_rayResults[i * maxBounces + cSetResultCount - 1].audioTargetId == 0 && math.distance(returningRayDir, float3.zero) != 0)
                        {
                            lastReturningRayOrigin = DEBUG_rayResults[i * maxBounces + i2].point;
                        }
                    }

                    //draw last ray that returned to origin and add its origin to lastReturningRayOriginTotal
                    if (math.distance(lastReturningRayOrigin, float3.zero) != 0)
                    {
                        lastReturningRayOriginTotal += lastReturningRayOrigin;
                        lastReturningRayOriginsCount++;

                        if (drawReturnRayDirectionGizmos)
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
        if (drawColliderGizmos && DEBUG_AABBColliders != null && DEBUG_OBBColliders != null && DEBUG_sphereColliders != null)
        {
            foreach (var box in DEBUG_AABBColliders)
            {
                Gizmos.DrawWireCube(box.center, box.size * 2);
            }
            foreach (var box in DEBUG_OBBColliders)
            {
                Gizmos.DrawWireMesh(GlobalMeshes.cube, box.center, box.rotation, box.size * 2);
            }
            foreach (var sphere in DEBUG_sphereColliders)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }
#endif
}
