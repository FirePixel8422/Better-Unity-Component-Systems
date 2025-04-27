using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct AudioRayTraceJobParallel : IJobParallelFor
{
    [ReadOnly][NoAlias] public float3 rayOrigin;
    [ReadOnly][NoAlias] public NativeArray<float3> rayDirections;

    [ReadOnly][NoAlias] public NativeArray<ColliderAABBStruct> AABBColliders;
    [ReadOnly][NoAlias] public NativeArray<ColliderOBBStruct> OBBColliders;
    [ReadOnly][NoAlias] public NativeArray<ColliderSphereStruct> sphereColliders;

    [ReadOnly][NoAlias] public float maxRayDist;
    [ReadOnly][NoAlias] public int maxBounces;

    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeArray<AudioRayResult> results;

    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeArray<int> resultCounts;


    [BurstCompile]
    public void Execute(int rayIndex)
    {
        float3 cRayDir = rayDirections[rayIndex];
        
        float closestDist = float.MaxValue;
        AudioRayResult rayResult = AudioRayResult.Null;

        ColliderType hitColliderType = ColliderType.None;

        ColliderAABBStruct hitAABB = ColliderAABBStruct.Null;
        ColliderOBBStruct hitOBB = ColliderOBBStruct.Null;
        ColliderSphereStruct hitSphere = ColliderSphereStruct.Null;

        //create and reuse local variables for in the loop
        ColliderAABBStruct tempAABB;
        ColliderOBBStruct tempOBB;
        ColliderSphereStruct tempSphere;
        float dist;
        float soundAbsorption;

        //save local copy of rayOrigin
        float3 cRayOrigin = rayOrigin;

        int bounceCount = 0;
        float totalDist = 0;

        //loop of ray has bounces and life left
        while (bounceCount < maxBounces && totalDist < maxRayDist)
        {
            closestDist = float.MaxValue;

            //box intersection (AABB)
            for (int i = 0; i < AABBColliders.Length; i++)
            {
                tempAABB = AABBColliders[i];
                if (RayIntersectsAABB(cRayOrigin, cRayDir, tempAABB.center, tempAABB.size, out dist))
                {
                    //if this is the closest hit so far
                    if (dist < closestDist)
                    {
                        hitColliderType = ColliderType.AABB;
                        hitAABB = tempAABB;
                        closestDist = dist;

                        rayResult.distance = dist;
                        rayResult.point = cRayOrigin + cRayDir * dist;
                    }
                }
            }
            //rotated box intersection (OBB)
            for (int i = 0; i < OBBColliders.Length; i++)
            {
                tempOBB = OBBColliders[i];
                if (RayIntersectsOBB(cRayOrigin, cRayDir, tempOBB.center, tempOBB.size, tempOBB.rotation, out dist))
                {
                    //if this is the closest hit so far
                    if (dist < closestDist)
                    {
                        hitColliderType = ColliderType.OBB;
                        hitOBB = tempOBB;
                        closestDist = dist;

                        rayResult.distance = dist;
                        rayResult.point = cRayOrigin + cRayDir * dist;
                    }
                }
            }
            // Sphere intersection
            for (int i = 0; i < sphereColliders.Length; i++)
            {
                tempSphere = sphereColliders[i];
                if (RayIntersectsSphere(cRayOrigin, cRayDir, tempSphere.center, tempSphere.radius, out dist))
                {
                    //if this is the closest hit so far
                    if (dist < closestDist)
                    {
                        hitColliderType = ColliderType.Sphere;
                        hitSphere = tempSphere;
                        closestDist = dist;

                        rayResult.distance = dist;
                        rayResult.point = cRayOrigin + cRayDir * dist;
                    }
                }
            }


            //if hit is detected, update ray's position, direction, and total distance traveled
            if (hitColliderType != ColliderType.None)
            {
                //update next ray direction and origin (bouncing it of the hit normal), also get soundAbsorption stat from hit wall
                (cRayOrigin, cRayDir, soundAbsorption) = ReflectRay(hitColliderType, rayResult.point, cRayDir, hitAABB, hitOBB, hitSphere);

                //update ray distance traveled and add sound absorption
                totalDist += closestDist + maxRayDist * soundAbsorption;

                //add hit result to list
                results[rayIndex * maxBounces + bounceCount] = rayResult;

                bounceCount += 1;
            }
            else
            {
                resultCounts[rayIndex] = bounceCount;

                break; // No more hits, break out of the loop
            }
        }

        resultCounts[rayIndex] = bounceCount;
    }

    [BurstCompile]
    private bool RayIntersectsAABB(float3 rayOrigin, float3 rayDir, float3 center, float3 halfExtents, out float distance)
    {
        float3 min = center - halfExtents;
        float3 max = center + halfExtents;

        float3 invDir = 1.0f / rayDir;

        float3 t0 = (min - rayOrigin) * invDir;
        float3 t1 = (max - rayOrigin) * invDir;

        float3 tmin = math.min(t0, t1);
        float3 tmax = math.max(t0, t1);

        float tNear = math.max(math.max(tmin.x, tmin.y), tmin.z);
        float tFar = math.min(math.min(tmax.x, tmax.y), tmax.z);

        if (tNear > tFar || tFar < 0)
        {
            distance = 0;
            return false;
        }

        distance = tNear > 0 ? tNear : tFar;
        return true;
    }


    [BurstCompile]
    private bool RayIntersectsOBB(float3 rayOrigin, float3 rayDir, float3 center, float3 halfExtents, quaternion rotation, out float distance)
    {
        // Transform ray into OBB local space
        float3 localOrigin = math.mul(math.inverse(rotation), rayOrigin - center);
        float3 localDir = math.mul(math.inverse(rotation), rayDir);

        // Use your existing AABB intersection function on the local ray
        return RayIntersectsAABB(localOrigin, localDir, float3.zero, halfExtents, out distance);
    }


    [BurstCompile]
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


    [BurstCompile]
    private (float3 rayOrigin, float3 rayDir, float lostmaxRayDist) ReflectRay(ColliderType hitColliderType, float3 hitWorldPoint, float3 cRayDir, ColliderAABBStruct hitAABB, ColliderOBBStruct hitOBB, ColliderSphereStruct hitSphere)
    {
        float3 normal = float3.zero;
        float lostmaxRayDist = 0;

        switch (hitColliderType)
        {
            case ColliderType.AABB:

                float3 localPoint = hitWorldPoint - hitAABB.center;
                float3 absPoint = math.abs(localPoint);
                float3 halfExtents = hitAABB.size;

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

                lostmaxRayDist = hitAABB.absorption;

                break;

            case ColliderType.OBB:

                float3 localHit = math.mul(math.inverse(hitOBB.rotation), hitWorldPoint - hitOBB.center);
                float3 localHalfExtents = hitOBB.size;

                float3 absPointOBB = math.abs(localHit);
                float3 deltaToFaceOBB = localHalfExtents - absPointOBB;

                float3 localNormal = float3.zero;

                if (deltaToFaceOBB.x < deltaToFaceOBB.y && deltaToFaceOBB.x < deltaToFaceOBB.z)
                {
                    localNormal.x = math.sign(localHit.x);
                }
                else if (deltaToFaceOBB.y < deltaToFaceOBB.x && deltaToFaceOBB.y < deltaToFaceOBB.z)
                {
                    localNormal.y = math.sign(localHit.y);
                }
                else
                {
                    localNormal.z = math.sign(localHit.z);
                }

                normal = math.mul(hitOBB.rotation, localNormal);

                lostmaxRayDist = hitOBB.absorption;

                break;

            case ColliderType.Sphere:

                normal = math.normalize(hitWorldPoint - hitSphere.center);

                lostmaxRayDist = hitSphere.absorption;

                break;

            default:

                break;
        }

        //update next ray direction (bouncing it of the hit wall)
        float3 newRayDir = math.reflect(cRayDir, normal);

        //update rays new origin (hit point)
        float3 newRayOrigin = hitWorldPoint + newRayDir * 0.0001f;

        return (newRayOrigin, newRayDir, lostmaxRayDist);
    }
}
