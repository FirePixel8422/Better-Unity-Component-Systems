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

    [ReadOnly][NoAlias] public NativeArray<float3> audioTargetPositions;

    [ReadOnly][NoAlias] public float maxRayDist;
    [ReadOnly][NoAlias] public int maxBounces;

    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeArray<AudioRayResult> results;

    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeArray<int> resultCounts;

    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeArray<float3> returnRayDirections;



    [BurstCompile]
    public void Execute(int rayIndex)
    {
        float3 cRayDir = rayDirections[rayIndex];

        float closestDist = float.MaxValue;
        AudioRayResult rayResult = AudioRayResult.Null;
        float3 rayResultHitWorldPoint = float3.zero;

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

        //reset return ray directions array completely before starting
        for (int i = 0; i < maxBounces + 1; i++)
        {
            returnRayDirections[rayIndex * maxBounces + i] = float3.zero;
            results[rayIndex * maxBounces + i] = AudioRayResult.Null;
        }
        resultCounts[rayIndex] = 0;


        //loop of ray has bounces and life left
        while (bounceCount <= maxBounces && totalDist < maxRayDist)
        {
            closestDist = float.MaxValue;
            hitColliderType = ColliderType.None;


            #region Interection Tests: AABB, OBB, Sphere

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
                        rayResult.audioTargetId = tempAABB.audioTargetId;
                        //rayResult.point = cRayOrigin + cRayDir * dist;
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
                        rayResult.audioTargetId = tempOBB.audioTargetId;
                        //rayResult.point = cRayOrigin + cRayDir * dist;
                    }
                }
            }
            //sphere intersection
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
                        rayResult.audioTargetId = tempSphere.audioTargetId;
                        //rayResult.point = cRayOrigin + cRayDir * dist;
                    }
                }
            }

            #endregion


            //if hit is detected, update ray's position, direction, and total distance traveled
            if (hitColliderType != ColliderType.None)
            {
                rayResultHitWorldPoint = cRayOrigin + cRayDir * closestDist;

                //update next ray direction and origin (bouncing it of the hit normal), also get soundAbsorption stat from hit wall
                (cRayOrigin, cRayDir, soundAbsorption) = ReflectRay(hitColliderType, rayResultHitWorldPoint, cRayDir, hitAABB, hitOBB, hitSphere);


                #region Check if hit ray point can return to original origin point

                // Only shoot a return ray if it's not the first bounce
                if (bounceCount > 0)
                {
                    // Shoot a return ray to the original origin
                    float3 returnRayDir = math.normalize(rayOrigin - cRayOrigin); // Direction back to origin

                    // Calculate the distance to the original origin
                    float distToOriginalOrigin = math.distance(rayOrigin, cRayOrigin); // Get the distance to the origin

                    // if nothing was hit, store the return ray direction
                    if (CanRaySeePoint(cRayOrigin, returnRayDir, distToOriginalOrigin))
                    {
                        returnRayDirections[rayIndex * maxBounces + bounceCount] = returnRayDir;
                    }
                }
                #endregion


                //update ray distance traveled and add sound absorption
                totalDist += closestDist;

                if (soundAbsorption != 0)
                {
                    totalDist += maxRayDist * soundAbsorption;
                    bounceCount += (int)(maxBounces * soundAbsorption);
                }

#if UNITY_EDITOR
                //for debugging and drawing gizmos
                rayResult.point = rayResultHitWorldPoint;
#endif

                //add hit result to return data array in the assigned index for this ray
                results[rayIndex * maxBounces + bounceCount] = rayResult;

                bounceCount += 1;
            }
            else
            {
                resultCounts[rayIndex] = bounceCount;

                break; //ray went ou of bounds, break out of the loop
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
    private bool CanRaySeePoint(float3 rayOrigin, float3 rayDir, float distToOriginalOrigin)
    {
        //check against AABBs
        foreach (var collider in AABBColliders)
        {
            if (RayIntersectsAABB(rayOrigin, rayDir, collider.center, collider.size, out float dist) && dist < distToOriginalOrigin)
            {
                return false;
            }
        }
        //if there was no AABB hit, check against OBBs
        foreach (var collider in OBBColliders)
        {
            if (RayIntersectsOBB(rayOrigin, rayDir, collider.center, collider.size, collider.rotation, out float dist) && dist < distToOriginalOrigin)
            {
                return false;
            }
        }
        //if there were no AABB and OBB hits, check against spheres
        foreach (var collider in sphereColliders)
        {
            if (RayIntersectsSphere(rayOrigin, rayDir, collider.center, collider.radius, out float dist) && dist < distToOriginalOrigin)
            {
                return false;
            }
        }

        return true;
    }



    [BurstCompile]
    private (float3 rayOrigin, float3 rayDir, float lostmaxRayDist) ReflectRay(ColliderType hitColliderType, float3 cRayOrigin, float3 cRayDir, ColliderAABBStruct hitAABB, ColliderOBBStruct hitOBB, ColliderSphereStruct hitSphere)
    {
        float3 normal = float3.zero;
        float lostmaxRayDist;
        bool audioTargetHit;

        switch (hitColliderType)
        {
            case ColliderType.AABB:

                lostmaxRayDist = hitAABB.absorption;

                if (lostmaxRayDist != -1)
                {
                    //set origin trough the collider and retirn half of collider absorption because the ray has to leave the collider aswell
                    return (cRayOrigin + cRayDir * 0.0001f, cRayDir, lostmaxRayDist * 0.5f);
                }

                float3 localPoint = cRayOrigin - hitAABB.center;
                float3 absPoint = math.abs(localPoint);
                float3 halfExtents = hitAABB.size;
                normal = float3.zero;

                // Reflect the ray based on the closest axis
                if (halfExtents.x - absPoint.x < halfExtents.y - absPoint.y && halfExtents.x - absPoint.x < halfExtents.z - absPoint.z)
                {
                    normal.x = math.sign(localPoint.x);
                }
                else if (halfExtents.y - absPoint.y < halfExtents.x - absPoint.x && halfExtents.y - absPoint.y < halfExtents.z - absPoint.z)
                {
                    normal.y = math.sign(localPoint.y);
                }
                else
                {
                    normal.z = math.sign(localPoint.z);
                }

                audioTargetHit = hitAABB.audioTargetId != -1;

                break;

            case ColliderType.OBB:

                lostmaxRayDist = hitOBB.absorption;

                if (lostmaxRayDist != -1)
                {
                    //set origin trough the collider and retirn half of collider absorption because the ray has to leave the collider aswell
                    return (cRayOrigin + cRayDir * 0.0001f, cRayDir, lostmaxRayDist * 0.5f);
                }

                float3 localHit = math.mul(math.inverse(hitOBB.rotation), cRayOrigin - hitOBB.center);
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

                audioTargetHit = hitOBB.audioTargetId != -1;

                break;

            case ColliderType.Sphere:

                lostmaxRayDist = hitSphere.absorption;

                if (lostmaxRayDist != -1)
                {
                    //set origin trough the collider and retirn half of collider absorption because the ray has to leave the collider aswell
                    return (cRayOrigin + cRayDir * 0.0001f, cRayDir, lostmaxRayDist * 0.5f);
                }                    

                normal = math.normalize(cRayOrigin - hitSphere.center);

                audioTargetHit = hitSphere.audioTargetId != -1;

                break;

            default:
                audioTargetHit = false;
                break;
        }

        //update next ray direction (bouncing it of the hit wall)
        float3 newRayDir = math.reflect(cRayDir, normal);

        //update rays new origin (hit point)
        float3 newRayOrigin = cRayOrigin + newRayDir * 0.0001f;

        //fully absorb ray if an audiotarget was hit
        return (newRayOrigin, newRayDir, audioTargetHit ? 1 : 0);
    }
}
