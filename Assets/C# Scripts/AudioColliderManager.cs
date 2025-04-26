using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Jobs;


[BurstCompile(DisableSafetyChecks = true)]
public class AudioColliderManager : MonoBehaviour
{
    List<AudioColliderGroup> colliderGroups;

    private NativeArray<ColliderBoxStruct> boxColliders;
    private NativeArray<ColliderSphereStruct> sphereColliders;

    private float3 rayOrigin;
    private NativeArray<float3> rayDirections;

    private NativeList<AudioRayResult> rayResults;

    [SerializeField] int rayCount = 1000;
    [SerializeField] int maxBounces = 3;
    [SerializeField] float maxDistance = 10;



    [BurstCompile(DisableSafetyChecks = true)]
    private void Start()
    {
        InitializeAudioRaytraceSystem();
    }

    [BurstCompile(DisableSafetyChecks = true)]
    public void InitializeAudioRaytraceSystem()
    {
        // Initialize Raycast native arrays
        rayDirections = new NativeArray<float3>(rayCount, Allocator.Persistent);
        rayResults = new NativeList<AudioRayResult>(rayCount * maxBounces, Allocator.Persistent);

        var generateDirectionsJob = new GenerateFibonacciSphereDirectionsJob
        {
            directions = rayDirections
        };

        JobHandle mainJobHandle = generateDirectionsJob.Schedule(rayCount, 64);

        SetupColliderData();

        // Now that we have directions and colliders, schedule the raycasting job
        var audioRayTraceJob = new AudioRayTraceJobParallel
        {
            rayOrigin = rayOrigin,
            rayDirections = rayDirections,

            boxColliders = boxColliders,
            sphereColliders = sphereColliders,

            maxBounces = maxBounces,
            maxDistance = maxDistance,

            results = rayResults.AsParallelWriter()
        };

        mainJobHandle.Complete();
        audioRayTraceJob.Schedule(rayCount, 64, mainJobHandle).Complete();
    }

    [BurstCompile(DisableSafetyChecks = true)]
    private void SetupColliderData()
    {
        //get all collider groups
        colliderGroups = new List<AudioColliderGroup>(FindObjectsOfType<AudioColliderGroup>());

        int boxCount = 0;
        int sphereCount = 0;

        //calculate total amount of colliders for box and spheres
        for (int i = 0; i < colliderGroups.Count; i++)
        {
            boxCount += colliderGroups[i].BoxCount;
            sphereCount += colliderGroups[i].SphereCount;
        }

        //setup native arrays for colliders
        boxColliders = new NativeArray<ColliderBoxStruct>(boxCount, Allocator.Persistent);
        sphereColliders = new NativeArray<ColliderSphereStruct>(sphereCount, Allocator.Persistent);

        int cBoxId = 0;
        int cSphereId = 0;

        //assign colliders to the native arrays
        foreach (var group in colliderGroups)
        {
            group.AddBoxColliders(boxColliders, cBoxId);
            cBoxId += group.BoxCount;

            group.AddSphereColliders(sphereColliders, cSphereId);
            cSphereId += group.SphereCount;
        }
    }


    [BurstCompile(DisableSafetyChecks = true)]
    private void OnDestroy()
    {
        // Dispose of native arrays
        if (rayDirections.IsCreated)
            rayDirections.Dispose();

        if (rayResults.IsCreated)
            rayResults.Dispose();

        if (boxColliders.IsCreated)
            boxColliders.Dispose();

        if (sphereColliders.IsCreated)
            sphereColliders.Dispose();
    }




#if UNITY_EDITOR

    [SerializeField] private bool drawGizmos;

    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false || drawGizmos == false || boxColliders.IsCreated == false || sphereColliders.IsCreated == false) return;

        //green blue-ish color
        Gizmos.color = new Color(1f, 0.75f, 0.25f);

        // Draw all colliders in the group
        foreach (var box in boxColliders)
        {
            Gizmos.DrawWireCube(box.center, box.size * 2);
        }

        foreach (var sphere in sphereColliders)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }

        Gizmos.color = Color.red;
        foreach (var result in rayResults)
        {
            if (result.IsNull) continue;

            Gizmos.DrawWireSphere(result.point, 0.1f);
        }
    }
#endif
}
