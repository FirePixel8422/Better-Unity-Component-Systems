using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(DisableSafetyChecks = true)]
public struct AudioRayTraceJobParallel : IJobParallelFor
{
    [ReadOnly][NoAlias] public float3 rayOrigin;
    [ReadOnly][NoAlias] public NativeArray<float3> rayDirections;

    [ReadOnly][NoAlias] public NativeArray<ColliderBoxStruct> boxColliders;
    [ReadOnly][NoAlias] public NativeArray<ColliderSphereStruct> sphereColliders;

    [WriteOnly][NoAlias] public NativeArray<AudioRayResult> results;


    [BurstCompile(DisableSafetyChecks = true)]
    public void Execute(int index)
    {
        float3 rayDir = rayDirections[index];

        float closestDist = float.MaxValue;

        AudioRayResult rayResult = AudioRayResult.Null;

        // Cache collider data
        ColliderBoxStruct box;
        ColliderSphereStruct sphere;
        float dist;

        // Box intersection (AABB)
        for (int i = 0; i < boxColliders.Length; i++)
        {
            box = boxColliders[i];
            if (RayIntersectsAABB(rayOrigin, rayDir, box.center, box.size, out dist))
            {
                if (dist < closestDist)
                {
                    closestDist = dist;
                    rayResult.distance = dist;
                    rayResult.point = rayOrigin + rayDir * dist;
                }
            }
        }

        // Sphere intersection
        for (int i = 0; i < sphereColliders.Length; i++)
        {
            sphere = sphereColliders[i];
            if (RayIntersectsSphere(rayOrigin, rayDir, sphere.center, sphere.radius, out dist))
            {
                if (dist < closestDist)
                {
                    closestDist = dist;
                    rayResult.distance = dist;
                    rayResult.point = rayOrigin + rayDir * dist;
                }
            }
        }

        results[index] = rayResult;
    }

    [BurstCompile(DisableSafetyChecks = true)]
    private bool RayIntersectsAABB(float3 rayOrigin, float3 rayDir, float3 center, float3 halfExtents, out float distance)
    {
        float3 min = center - halfExtents;
        float3 max = center + halfExtents;

        // Calculate intersection with AABB
        float tmin = (min.x - rayOrigin.x) / rayDir.x;
        float tmax = (max.x - rayOrigin.x) / rayDir.x;
        if (tmin > tmax) { float tmp = tmin; tmin = tmax; tmax = tmp; }

        float tymin = (min.y - rayOrigin.y) / rayDir.y;
        float tymax = (max.y - rayOrigin.y) / rayDir.y;
        if (tymin > tymax) { float tmp = tymin; tymin = tymax; tymax = tmp; }

        if ((tmin > tymax) || (tymin > tmax)) { distance = 0; return false; }
        if (tymin > tmin) tmin = tymin;
        if (tymax < tmax) tmax = tymax;

        float tzmin = (min.z - rayOrigin.z) / rayDir.z;
        float tzmax = (max.z - rayOrigin.z) / rayDir.z;
        if (tzmin > tzmax) { float tmp = tzmin; tzmin = tzmax; tzmax = tmp; }

        if ((tmin > tzmax) || (tzmin > tmax)) { distance = 0; return false; }
        if (tzmin > tmin) tmin = tzmin;
        if (tzmax < tmax) tmax = tzmax;

        distance = tmin > 0 ? tmin : tmax;
        return distance >= 0;
    }

    [BurstCompile(DisableSafetyChecks = true)]
    private bool RayIntersectsSphere(float3 rayOrigin, float3 rayDir, float3 center, float radius, out float distance)
    {
        float3 oc = rayOrigin - center;
        float a = math.dot(rayDir, rayDir);
        float b = 2.0f * math.dot(oc, rayDir);
        float c = math.dot(oc, oc) - radius * radius;
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            distance = 0;
            return false;
        }

        float sqrtDiscriminant = math.sqrt(discriminant);
        float t0 = (-b - sqrtDiscriminant) / (2.0f * a);
        float t1 = (-b + sqrtDiscriminant) / (2.0f * a);

        // Select the nearest valid intersection
        if (t0 >= 0)
        {
            distance = t0;
            return true;
        }
        else if (t1 >= 0)
        {
            distance = t1;
            return true;
        }

        distance = 0;
        return false;
    }
}