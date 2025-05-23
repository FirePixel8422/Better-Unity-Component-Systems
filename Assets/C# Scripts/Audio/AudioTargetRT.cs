using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


[RequireComponent(typeof(AudioSource), typeof(AudioSpatializer), typeof(AudioReverbFilter))]
public class AudioTargetRT : AudioColliderGroup
{
    public int id;

    private AudioSource source;
    private AudioSpatializer spatializer;
    private AudioReverbFilter reverb;



    private void Start()
    {
        source = GetComponent<AudioSource>();
        spatializer = GetComponent<AudioSpatializer>();
        reverb = GetComponent<AudioReverbFilter>();

        spatializer.muffleStrength = 0f;
    }


    #region Get Colliders Override Method

    [BurstCompile]
    /// <summary>
    /// Add all colliders of this AudioGroup to the native arrays of the custom physics engine.
    /// override: also set the audioTargetId of all colliders to the id of this script
    /// </summary>
    public override void GetColliders(
        NativeArray<ColliderAABBStruct> _AABBs, int AABBsStartIndex,
        NativeArray<ColliderOBBStruct> _OBBs, int OBBsStartIndex,
        NativeArray<ColliderSphereStruct> _spheres, int spheresStartIndex)
    {
        int colliderCount = AABBCount;

        //add all boxes to the native array
        for (int i = 0; i < colliderCount; i++)
        {
            ColliderAABBStruct box = axisAlignedBoxes[i];

            //account for transform position and set groupId
            box.center += (float3)transform.position;
            box.size *= transform.localScale;

            box.audioTargetId = id;

            _AABBs[AABBsStartIndex + i] = box;
        }

        colliderCount = OBBCount;

        //add all boxes to the native array
        for (int i = 0; i < colliderCount; i++)
        {
            ColliderOBBStruct box = orientedBoxes[i];

            //account for transform position and set groupId
            box.center += (float3)transform.position;
            box.rotation = transform.rotation;
            box.size *= transform.localScale;

            box.audioTargetId = id;

            _OBBs[OBBsStartIndex + i] = box;
        }

        colliderCount = SphereCount;

        //add all spheres to the native array
        for (int i = 0; i < colliderCount; i++)
        {
            ColliderSphereStruct sphere = spheres[i];

            //account for transform position and set groupId
            sphere.center += (float3)transform.position;
            sphere.radius *= math.max(transform.localScale.x, math.max(transform.localScale.y, transform.localScale.z));

            sphere.audioTargetId = id;

            _spheres[spheresStartIndex + i] = sphere;
        }
    }

    #endregion


    /// <summary>
    /// Update AudioTarget at realtime based on the AudioRaytracer's data
    /// </summary>
    public void UpdateAudioSource(AudioTargetData newSettings)
    {
        //1 = 100% muffled audio
        spatializer.muffleStrength = newSettings.muffle;
    }
}
