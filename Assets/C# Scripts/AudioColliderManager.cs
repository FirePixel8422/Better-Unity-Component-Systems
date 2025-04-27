using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Jobs;


[BurstCompile]
public class AudioColliderManager : MonoBehaviour
{
    private List<AudioColliderGroup> colliderGroups;

    private NativeArray<ColliderAABBStruct> AABBColliders;
    private NativeArray<ColliderOBBStruct> OBBColliders;
    private NativeArray<ColliderSphereStruct> sphereColliders;

    [SerializeField] private float3 rayOrigin;

    private NativeArray<float3> rayDirections;

    private NativeArray<AudioRayResult> rayResults;
    private NativeArray<int> rayResultCounts;


    [Range(1, 100000)]
    [SerializeField] int rayCount = 1000;

    [Range(1, 100)]
    [SerializeField] int maxBounces = 3;

    [Range(0, 100)]
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

        var generateDirectionsJob = new GenerateFibonacciSphereDirectionsJob
        {
            directions = rayDirections
        };

        JobHandle mainJobHandle = generateDirectionsJob.Schedule(rayCount, 64);

        //do as much tasks here to give the job some time to complete before forcing it to complete.
        rayResults = new NativeArray<AudioRayResult>(rayCount * maxBounces, Allocator.Persistent);
        rayResultCounts = new NativeArray<int>(rayCount, Allocator.Persistent);

        SetupColliderData();

        mainJobHandle.Complete();
    }

    [BurstCompile]
    private void SetupColliderData()
    {
        //get all collider groups
        colliderGroups = new List<AudioColliderGroup>(FindObjectsOfType<AudioColliderGroup>());

        int AABBCount = 0;
        int OBBCount = 0;
        int sphereCount = 0;

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
            group.AddColliders(AABBColliders, cAABBId, OBBColliders, cOBBId, sphereColliders, cSphereId);

            cAABBId += group.AABBCount;
            cOBBId += group.OBBCount;
            cSphereId += group.SphereCount;
        }
    }



    [Header("WARNING: Setting this to false will block the main thread every frame until all rays are calculated")]
    [SerializeField] private bool waitForJobCompletion = true;

    [SerializeField] int batchSize;

    private AudioRayTraceJobParallel audioRayTraceJob;
    private JobHandle audioRayTraceJobHandle;

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
        }

        //failsafe to prevent crash when updating maxBounces in editor
        if (audioRayTraceJob.rayDirections.Length != 0 && (audioRayTraceJob.maxBounces != maxBounces || rayDirections.Length != rayCount))
        {
            //recreate rayResults array with new size because maxBounces changed
            rayResults = new NativeArray<AudioRayResult>(rayCount * maxBounces, Allocator.Persistent);

            if (rayDirections.Length != rayCount)
            {
                //reculcate ray directions and resize rayResultCounts if rayCount changed
                rayDirections = new NativeArray<float3>(rayCount, Allocator.Persistent);
                rayResultCounts = new NativeArray<int>(rayCount, Allocator.Persistent);

                var generateDirectionsJob = new GenerateFibonacciSphereDirectionsJob
                {
                    directions = rayDirections
                };

                generateDirectionsJob.Schedule(rayCount, 64).Complete();

                Debug.LogWarning("You changed the rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated rayDirections array with new capacity.");
            }

            Debug.LogWarning("You changed the max bounces/rayCount in the inspector. This will cause a crash in Builds, failsafe triggered: Recreated rayResults array with new capacity.");
        }
#endif

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
        };

        //force job completion
        audioRayTraceJobHandle = audioRayTraceJob.Schedule(rayCount, batchSize);
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

    private ColliderAABBStruct[] DEBUG_AABBColliders;
    private ColliderOBBStruct[] DEBUG_OBBColliders;
    private ColliderSphereStruct[] DEBUG_sphereColliders;

    private AudioRayResult[] DEBUG_rayResults;
    private int[] DEBUG_rayResultCounts;

    [SerializeField] private float ms;
    private System.Diagnostics.Stopwatch sw;


    [BurstCompile]
    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false) return;

        if (DEBUG_rayResults != null && DEBUG_rayResults.Length != 0 && (drawRayHitsGizmos || drawRayTrailsGizmos))
        {
            AudioRayResult prevResult = AudioRayResult.Null;

            int maxBounces = DEBUG_rayResults.Length / DEBUG_rayResultCounts.Length;

            int setResultAmountsCount = DEBUG_rayResultCounts.Length;
            int cSetResultCount;

            if(setResultAmountsCount * maxBounces > 5000)
            {
                Debug.LogWarning("Max Gizmos Reached (5k) please turn of gizmos to not fry CPU");
                setResultAmountsCount = 5000 / maxBounces;
            }

            for (int i = 0; i < setResultAmountsCount; i++)
            {
                cSetResultCount = DEBUG_rayResultCounts[i];
                prevResult.point = (float3)transform.position + rayOrigin;

                for (int i2 = 0; i2 < cSetResultCount; i2++)
                {
                    AudioRayResult result = DEBUG_rayResults[i * maxBounces + i2];

                    Gizmos.color = Color.cyan;

                    if (drawRayHitsGizmos)
                    {
                        Gizmos.DrawWireCube(result.point, Vector3.one * 0.1f);
                    }

                    Gizmos.color = new Color(0, 1, 0, 0.15f);

                    if (drawRayTrailsGizmos)
                    {
                        Gizmos.DrawLine(prevResult.point, result.point);
                        prevResult = result;
                    }
                }
            }
            //origin cube
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere((float3)transform.position + rayOrigin, 0.025f);
            Gizmos.DrawWireSphere((float3)transform.position + rayOrigin, 0.05f);
        }

        //green blue-ish color
        Gizmos.color = new Color(1f, 0.75f, 0.25f);

        // Draw all colliders in the collider arrays
        if (drawColliderGizmos && DEBUG_AABBColliders != null && DEBUG_OBBColliders != null && DEBUG_sphereColliders != null)
        {
            foreach (var box in DEBUG_AABBColliders)
            {
                Gizmos.DrawWireCube(box.center, box.size * 2 * transform.localScale);
            }
            foreach (var box in DEBUG_OBBColliders)
            {
                Gizmos.DrawWireMesh(GlobalMeshes.cube, box.center, box.rotation, box.size * 2 * transform.localScale);
            }
            foreach (var sphere in DEBUG_sphereColliders)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }
#endif
}
