using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;


[BurstCompile]
public class AudioColliderGroup : MonoBehaviour
{
    [Header("Box Colliders WITHOUT rotation: fast > 7/10")]
    [SerializeField] private List<ColliderAABBStruct> axisAlignedBoxes = new List<ColliderAABBStruct>();
    [Header("Box Colliders with rotation: \nfast, but a little slower than an 'axisAlignedBox' > 6/10")]
    [SerializeField] private List<ColliderOBBStruct> orientedBoxes = new List<ColliderOBBStruct>();
    [Header("Sphere Collider: very fast > 10/10")]
    [SerializeField] private List<ColliderSphereStruct> spheres = new List<ColliderSphereStruct>();

    public int AABBCount => axisAlignedBoxes.Count;
    public int OBBCount => orientedBoxes.Count;
    public int SphereCount => spheres.Count;



    [BurstCompile]
    /// <summary>
    /// Add all colliders of this AudioGroup to the native arrays of the custom physics engine.
    /// </summary>
    public void AddColliders(
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

            _spheres[spheresStartIndex + i] = sphere;
        }
    }

    

    [BurstCompile]
    private void OnValidate()
    {
        for (int i = 0; i < AABBCount; i++)
        {
            ColliderAABBStruct box = axisAlignedBoxes[i];

            //return if box Collider has setup values.
            if (math.distance(box.center, float3.zero) != 0 || math.distance(box.size, float3.zero) != 0)
            {
                continue;
            }

            //give box collider default values if it is just created
            box.size = new float3(0.5f, 0.5f, 0.5f);

            //save copy back to list
            axisAlignedBoxes[i] = box;
        }

        for (int i = 0; i < OBBCount; i++)
        {
            ColliderOBBStruct box = orientedBoxes[i];

            box.rotation.value.w = 1;

            //return if box Collider has setup values.
            if (math.distance(box.center, float3.zero) != 0 || math.distance(box.size, float3.zero) != 0)
            {
                //save copy back to list
                orientedBoxes[i] = box;

                continue;
            }

            //give box collider default values if it is just created
            box.size = new float3(0.5f, 0.5f, 0.5f);

            //save copy back to list
            orientedBoxes[i] = box;
        }

        for (int i = 0; i < SphereCount; i++)
        {
            ColliderSphereStruct sphere = spheres[i];

            //return if sphere Collider has setup values.
            if (math.distance(sphere.center, float3.zero) != 0 || sphere.radius != 0)
            {
                continue;
            }

            //give sphere collider default values if it is just created
            sphere.radius = 0.5f;

            //save copy back to list
            spheres[i] = sphere;
        }
    }


    [BurstCompile]
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        float3 pos = transform.position;
        Quaternion rot;

        //green blue-ish color
        Gizmos.color = new Color(0, 1f, 0.25f);

        // Draw all colliders in the group
        foreach (var box in axisAlignedBoxes)
        {
            Gizmos.DrawWireCube(pos + box.center, box.size * 2 * transform.localScale);
        }
        foreach (var box in orientedBoxes)
        {
            rot = transform.rotation;
            if (box.rotation != Quaternion.identity)
            {
                rot = transform.rotation * box.rotation;
            }
            Gizmos.DrawWireMesh(GlobalMeshes.cube, pos + box.center, rot, box.size * 2 * transform.localScale);
        }
        foreach (var sphere in spheres)
        {
            Gizmos.DrawWireSphere(pos + sphere.center, sphere.radius * math.max(transform.localScale.x, math.max(transform.localScale.y, transform.localScale.z)));
        }
    }
}
