using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;


[BurstCompile(DisableSafetyChecks = true)]
public class AudioColliderGroup : MonoBehaviour
{
    [SerializeField] private List<ColliderBoxStruct> boxes = new List<ColliderBoxStruct>();
    [SerializeField] private List<ColliderSphereStruct> spheres = new List<ColliderSphereStruct>();

    public int BoxCount => boxes.Count;
    public int SphereCount => spheres.Count;



    [BurstCompile(DisableSafetyChecks = true)]
    /// <summary>
    /// Add all boxColliders of this AudioGroup to the native array, returns added amount.
    /// </summary>
    public void AddBoxColliders(NativeArray<ColliderBoxStruct> _boxes, int startIndex)
    {
        int boxCount = BoxCount;

        //add all boxes to the native array
        for (int i = 0; i < boxCount; i++)
        {
            ColliderBoxStruct box = boxes[i];

            //account for transform position and set groupId
            box.center += (float3)transform.position;
            box.size *= transform.localScale;

            _boxes[startIndex + i] = box;
        }
    }

    [BurstCompile(DisableSafetyChecks = true)]
    /// <summary>
    /// Add all sphereColliders of this AudioGroup to the native array, returns added amount.
    /// </summary>
    public void AddSphereColliders(NativeArray<ColliderSphereStruct> _spheres, int startIndex)
    {
        int sphereCount = SphereCount;

        //add all spheres to the native array
        for (int i = 0; i < sphereCount; i++)
        {
            ColliderSphereStruct sphere = spheres[i];

            //account for transform position and set groupId
            sphere.center += (float3)transform.position;
            sphere.radius *= math.max(transform.localScale.x, math.max(transform.localScale.y, transform.localScale.z));

            _spheres[startIndex + i] = sphere;
        }
    }

    

    [BurstCompile(DisableSafetyChecks = true)]
    private void OnValidate()
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            ColliderBoxStruct box = boxes[i];

            //return if box Collider has setup values.
            if (math.distance(box.center, float3.zero) != 0 || math.distance(box.size, float3.zero) != 0)
            {
                continue;
            }

            //give box collider default values if it is just created
            box.size = new float3(0.5f, 0.5f, 0.5f);
            box.absorption = 1;

            //save copy back to list
            boxes[i] = box;
        }

        for (int i = 0; i < spheres.Count; i++)
        {
            ColliderSphereStruct sphere = spheres[i];

            //return if sphere Collider has setup values.
            if (math.distance(sphere.center, float3.zero) != 0 || sphere.radius != 0)
            {
                continue;
            }

            //give sphere collider default values if it is just created
            sphere.radius = 0.5f;
            sphere.absorption = 1;

            //save copy back to list
            spheres[i] = sphere;
        }
    }


    [BurstCompile(DisableSafetyChecks = true)]
    private void OnDrawGizmos()
    {
        float3 pos = transform.position;

        //green blue-ish color
        Gizmos.color = new Color(0, 1f, 0.25f);

        // Draw all colliders in the group
        foreach (var box in boxes)
        {
            Gizmos.DrawWireMesh(GlobalMeshes.cube, pos + box.center, transform.rotation, box.size * 2 * transform.localScale);
        }
        foreach (var sphere in spheres)
        {
            Gizmos.DrawWireSphere(pos + sphere.center, sphere.radius * math.max(transform.localScale.x, math.max(transform.localScale.y, transform.localScale.z)));
        }
    }
}
