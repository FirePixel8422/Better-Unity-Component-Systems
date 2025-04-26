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

    [ReadOnly][NoAlias] public int maxBounces;
    [ReadOnly][NoAlias] public float maxDistance;

    [WriteOnly][NoAlias] public NativeList<AudioRayResult>.ParallelWriter results;


    [BurstCompile(DisableSafetyChecks = true)]
    public void Execute(int index)
    {
        float3 rayDir = rayDirections[index];
        
        float closestDist = float.MaxValue;
        AudioRayResult rayResult = AudioRayResult.Null;

        ColliderType hitColliderType = ColliderType.None;
        ColliderBoxStruct hitColliderBox = ColliderBoxStruct.Null;
        ColliderSphereStruct hitColliderSphere = ColliderSphereStruct.Null;

        //create and reuse local variables for in the loop
        ColliderBoxStruct temp_Box;
        ColliderSphereStruct temp_Sphere;
        float dist;

        //save local copy of rayOrigin
        float3 cRayOrigin = rayOrigin;

        int bounceCount = 0;
        float totalDist = 0;

        float3 normal = float3.zero;

        //loop of ray has bounces and travel distance left
        while (bounceCount < maxBounces && totalDist < maxDistance)
        {
            closestDist = float.MaxValue;

            // Box intersection (AABB)
            for (int i = 0; i < boxColliders.Length; i++)
            {
                temp_Box = boxColliders[i];
                if (RayIntersectsAABB(cRayOrigin, rayDir, temp_Box.center, temp_Box.size, out dist))
                {
                    //if this is the closest hit so far
                    if (dist < closestDist)
                    {
                        hitColliderType = ColliderType.BoxAABB;
                        hitColliderBox = temp_Box;
                        closestDist = dist;

                        rayResult.distance = dist;
                        rayResult.point = cRayOrigin + rayDir * dist;
                    }
                }
            }

            // Sphere intersection
            for (int i = 0; i < sphereColliders.Length; i++)
            {
                temp_Sphere = sphereColliders[i];
                if (RayIntersectsSphere(cRayOrigin, rayDir, temp_Sphere.center, temp_Sphere.radius, out dist))
                {
                    //if this is the closest hit so far
                    if (dist < closestDist)
                    {
                        hitColliderType = ColliderType.Sphere;
                        hitColliderSphere = temp_Sphere;
                        closestDist = dist;

                        rayResult.distance = dist;
                        rayResult.point = cRayOrigin + rayDir * dist;
                    }
                }
            }

            // If a hit is detected, update ray's position, direction, and total distance
            if (hitColliderType != ColliderType.None)
            {
                switch (hitColliderType)
                {
                    case ColliderType.BoxAABB:

                        float3 localPoint = rayResult.point - hitColliderBox.center;
                        float3 absPoint = math.abs(localPoint);
                        float3 halfExtents = hitColliderBox.size;

                        normal = float3.zero;

                        // Determine which face was hit by seeing which axis distance is closest to its respective half-extent
                        float3 deltaToFace = halfExtents - absPoint;

                        if (deltaToFace.x < deltaToFace.y && deltaToFace.x < deltaToFace.z)
                        {
                            normal.x = math.sign(localPoint.x);
                        }
                        else if (deltaToFace.y < deltaToFace.x && deltaToFace.y < deltaToFace.z)
                        {
                            normal.y = math.sign(localPoint.y);
                        }
                        else
                        {
                            normal.z = math.sign(localPoint.z);
                        }


                        break;

                    case ColliderType.Sphere:

                        normal = math.normalize(rayResult.point - hitColliderSphere.center);

                        break;

                    default:

                        break;
                }

                //update ray distance traveled
                totalDist += closestDist;

                //update next ray direction (bouncing it of the hit wall)
                rayDir = math.reflect(rayDir, normal);

                //update rays new origin (hit point)
                cRayOrigin = rayResult.point + rayDir * 0.0001f;

                bounceCount += 1;
            }
            else
            {
                break; // No more hits, break out of the loop
            }

            //add result to list
            results.AddNoResize(rayResult);
        }
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
