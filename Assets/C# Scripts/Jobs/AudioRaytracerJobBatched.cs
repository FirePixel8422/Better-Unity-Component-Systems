using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
public struct AudioRaytracerJobBatched : IJobParallelForBatch
{
    // Core Raytracer Job Data
    [ReadOnly, NoAlias] public float3 RayOrigin;
    [ReadOnly, NoAlias] public NativeArray<half3> RayDirections;

    [ReadOnly, NoAlias] public NativeArray<ColliderAABBStruct> AABBColliders;
    [ReadOnly, NoAlias] public int AABBColliderCount;
    [ReadOnly, NoAlias] public NativeArray<ColliderOBBStruct> OBBColliders;
    [ReadOnly, NoAlias] public int OBBColliderCount;
    [ReadOnly, NoAlias] public NativeArray<ColliderSphereStruct> SphereColliders;
    [ReadOnly, NoAlias] public int SphereColliderCount;

    [ReadOnly, NoAlias] public NativeArray<float3> AudioTargetPositions;
    [ReadOnly, NoAlias] public int TotalAudioTargets;

    [ReadOnly, NoAlias] public float MaxRayLife;
    [ReadOnly, NoAlias] public int MaxHitsPerRay;


    // Global Raycast data (single return arrays)
#if UNITY_EDITOR
    [NativeDisableParallelForRestriction]
    [WriteOnly, NoAlias] public NativeArray<AudioRayHitResult> RayHitResults;

    [NativeDisableParallelForRestriction]
    [WriteOnly, NoAlias] public NativeArray<byte> RayHitResultCounts;
#endif

    [NativeDisableParallelForRestriction]
    [WriteOnly, NoAlias] public NativeArray<half> EchoRayDistances;


    // Per AudioTarget Data (flattened 2D arrays based on TotalAudioTargets and thread usage)
    [NativeDisableParallelForRestriction]
    [NoAlias] public NativeArray<ushort> MuffleRayHits;

    [NoAlias, ReadOnly] public float MaxMuffleHitDistance;


    private const float EPSILON = 0.0001f;


    [BurstCompile]
    public void Execute(int rayStartIndex, int totalRays)
    {
        int batchCount = MuffleRayHits.Length / TotalAudioTargets;
        int batchId = rayStartIndex * batchCount / RayDirections.Length;

        // Save local copy of RayOrigin
        float3 cRayOrigin;

        #region Reset Array Data

        // Reset array data completely before starting
        for (int i = 0; i < totalRays * MaxHitsPerRay; i++)
        {
            int rayIndex = rayStartIndex + i;

            EchoRayDistances[rayIndex] = (half)0;
#if UNITY_EDITOR
            RayHitResults[rayIndex] = new AudioRayHitResult();
#endif
        }
        // Reset muffleRayHit count assigned to this batch
        for (short i = 0; i < TotalAudioTargets; i++)
        {
            MuffleRayHits[batchId * TotalAudioTargets + i] = 0;
        }

        #endregion

        // 1 batch is "totalRays" amount of rays, rayStartIndex as starting index 
        for (int localRayId = 0; localRayId < totalRays; localRayId++)
        {
            int rayIndex = rayStartIndex + localRayId;

            float3 cRayDir = RayDirections[rayIndex];
            cRayOrigin = RayOrigin;

            byte cRayHits = 0;
            int rayResultId;
            float cRayLife = MaxRayLife;
            bool isRayAlive = true;


            // Loop while ray has bounces and lifetime left
            while (isRayAlive)
            {
                // Intersection tests for environment ray: AABB, OBB, Sphere
                // Check if a collider was hit (aka. the ray didnt go out of bounds)
                if (ShootRayCast(cRayOrigin, cRayDir, out AudioRayHitResult rayResult, out ColliderType hitColliderType, out float rayHitDist, out ColliderAABBStruct hitAABB, out ColliderOBBStruct hitOBB, out ColliderSphereStruct hitSphere))
                {
                    // Update new ray origin, ray totalDist and add 1 bounce
                    cRayOrigin += cRayDir * rayHitDist;
                    cRayLife -= rayHitDist;
                    cRayHits += 1;

                    rayResultId = rayIndex * MaxHitsPerRay + cRayHits - 1;
#if UNITY_EDITOR
                    // For debugging like drawing gizmos
                    rayResult.HitPoint = (half3)cRayOrigin;
#endif

                    #region Echo rays to player (check if hit ray point can return to original origin point)

                    // Offset the hit point a bit so it doesnt intersect with same collider again
                    float3 offsettedRayHitWorldPoint = cRayOrigin - cRayDir * EPSILON;

                    // Shoot return ray to the original origin
                    float3 returnRayDir = math.normalize(RayOrigin - offsettedRayHitWorldPoint);

                    // Calculate distance to the original origin
                    float distToStartOrigin = math.distance(RayOrigin, cRayOrigin);

                    // If nothing was hit, aka the ray go to the player succesfully store the distance to the current main ray position
                    if (CanRaySeePoint(offsettedRayHitWorldPoint, returnRayDir, distToStartOrigin))
                    {
                        half echoMultiplier = hitColliderType switch
                        {
                            ColliderType.AABB => hitAABB.MaterialProperties.Echo,
                            ColliderType.OBB => hitOBB.MaterialProperties.Echo,
                            ColliderType.Sphere => hitSphere.MaterialProperties.Echo,
                            _ => (half)1,
                        };
                        Half.Multiply(distToStartOrigin, echoMultiplier, out half echoRayPower);

                        EchoRayDistances[rayResultId] = echoRayPower;
                    }

                    #endregion


                    #region Muffle rays to all audio targets (check if ray can get to audiotarget)

                    // Raycast to each AudioTarget position
                    for (short AudioTargetId = 0; AudioTargetId < TotalAudioTargets; AudioTargetId++)
                    {
                        int muffleRayId = batchId * TotalAudioTargets + AudioTargetId;

                        // Offset the hit point a bit so it doesnt intersect with same collider again
                        offsettedRayHitWorldPoint = cRayOrigin - cRayDir * EPSILON;

                        // Get Position of current AudioTarget and direction towards it with cRayOrigin
                        float3 audioTargetPosition = AudioTargetPositions[AudioTargetId];
                        float3 rayToTargetDir = math.normalize(audioTargetPosition - offsettedRayHitWorldPoint);

                        // Calculate distance to the audio target
                        float distToTarget = math.distance(offsettedRayHitWorldPoint, audioTargetPosition);

                        // If target isnt further away then MaxMuffleHitDistance, cast a ray from the hit point to the audio target
                        if (distToTarget < MaxMuffleHitDistance && CanRaySeeAudioTarget(offsettedRayHitWorldPoint, rayToTargetDir, distToTarget, AudioTargetId))
                        {
                            // If the ray to the audio target is clear, increment the appropriate entry in MuffleRayHits
                            MuffleRayHits[muffleRayId] += 1;
                        }
                    }

                    #endregion


                    // Check if ray is finished (if rayHits is more than MaxHitsPerRay or totalDist is equal or exceeds MaxRayDist)
                    if (cRayHits >= MaxHitsPerRay || cRayLife <= 0)
                    {
                        isRayAlive = false; // Ray wont bounce another time.
                    }
                    else
                    {
                        // If ray is still alive, update next ray direction and origin (bouncing it of the hit normal)
                        ReflectRay(hitColliderType, hitAABB, hitOBB, hitSphere, ref cRayOrigin, ref cRayDir, ref cRayLife);

                        // If last rayLife gets consumed by the hit collider, kill it
                        if (cRayLife < 0)
                        {
                            isRayAlive = false;
                        }
                    }

#if UNITY_EDITOR
                    // Add hit result to return data array
                    RayHitResults[rayResultId] = rayResult;
#endif
                }
                // Ray went out of bounds (Didnt hit anything), kill ray instantly
                else
                {
#if UNITY_EDITOR
                    RayHitResultCounts[rayIndex] = cRayHits;
#endif
                    break;
                }
            }

#if UNITY_EDITOR
            // Ray ended, write total ray hits into ResultCounts
            RayHitResultCounts[rayIndex] = cRayHits;
#endif
        }
    }


    #region Collider Intersection Checks (The Actual Raycasting Part)

    /// <summary>
    /// Checks all colliders for intersection with the ray and returns the closest hit collider and its type, data, and distance.
    /// </summary>
    /// <returns>True if the ray hits any collider; otherwise, false.</returns>
    [BurstCompile]
    private bool ShootRayCast(float3 cRayOrigin, float3 cRayDir,
        out AudioRayHitResult rayResult, out ColliderType hitColliderType, out float closestDist,
        out ColliderAABBStruct hitAABB, out ColliderOBBStruct hitOBB, out ColliderSphereStruct hitSphere)
    {
        float dist;
        closestDist = float.MaxValue;
        rayResult = new AudioRayHitResult();

        hitColliderType = ColliderType.None;
        hitAABB = new ColliderAABBStruct();
        hitOBB = new ColliderOBBStruct();
        hitSphere = new ColliderSphereStruct();

        // Sphere intersections
        for (int i = 0; i < SphereColliderCount; i++)
        {
            var tempSphere = SphereColliders[i];

            // If collider is hit AND it is the closest hit so far
            if (RayIntersectsSphere(cRayOrigin, cRayDir, tempSphere.Center, tempSphere.Radius, out dist) && dist < closestDist)
            {
                hitColliderType = ColliderType.Sphere;
                hitSphere = tempSphere;
                closestDist = dist;
            }
        }
        // Box intersections (AABB)
        for (int i = 0; i < AABBColliderCount; i++)
        {
            var tempAABB = AABBColliders[i];

            // If collider is hit AND it is the closest hit so far
            if (RayIntersectsAABB(cRayOrigin, cRayDir, tempAABB.Center, tempAABB.Size, out dist) && dist < closestDist)
            {
                hitColliderType = ColliderType.AABB;
                hitAABB = tempAABB;
                closestDist = dist;
            }
        }
        // Rotated Box intersections (OBB)
        for (int i = 0; i < OBBColliderCount; i++)
        {
            var tempOBB = OBBColliders[i];

            // If collider is hit AND it is the closest hit so far
            if (RayIntersectsOBB(cRayOrigin, cRayDir, tempOBB.Center, tempOBB.Size, tempOBB.Rotation, out dist) && dist < closestDist)
            {
                hitColliderType = ColliderType.OBB;
                hitOBB = tempOBB;
                closestDist = dist;
            }
        }

        // Return whether a hit was detected
        return hitColliderType != ColliderType.None;
    }


    [BurstCompile]
    private bool RayIntersectsAABB(float3 rayOrigin, float3 rayDir, float3 Center, float3 halfExtents, out float distance)
    {
        float3 min = Center - halfExtents;
        float3 max = Center + halfExtents;

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

    /// <summary>
    /// OBB Colliders have their "Rotation" pre-calculated as inverse quaternion for optimization.
    /// </summary>
    [BurstCompile]
    private bool RayIntersectsOBB(float3 rayOrigin, float3 rayDir, float3 Center, float3 halfExtents, quaternion invRotation, out float distance)
    {
        float3 localOrigin = math.mul(invRotation, rayOrigin - Center);
        float3 localDir = math.mul(invRotation, rayDir);

        return RayIntersectsAABB(localOrigin, localDir, float3.zero, halfExtents, out distance);
    }

    [BurstCompile]
    private bool RayIntersectsSphere(float3 rayOrigin, float3 rayDir, float3 Center, float Radius, out float distance)
    {
        float3 oc = rayOrigin - Center;
        float a = math.dot(rayDir, rayDir);
        float b = 2.0f * math.dot(oc, rayDir);
        float c = math.dot(oc, oc) - Radius * Radius;
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

    #endregion


    /// <summary>
    /// Check if world point is visible from the ray origin, meaning no colliders are in the way.
    /// Used for echo rays returning to player.
    /// </summary>
    [BurstCompile]
    private bool CanRaySeePoint(float3 rayOrigin, float3 rayDir, float distToTarget)
    {
        float dist;

        // Check against Spheres
        for (int i = 0; i < SphereColliderCount; i++)
        {
            var tempSphere = SphereColliders[i];
            if (RayIntersectsSphere(rayOrigin, rayDir, tempSphere.Center, tempSphere.Radius, out dist) && dist < distToTarget)
            {
                return false;
            }
        }
        // Check against AABBs
        for (int i = 0; i < AABBColliderCount; i++)
        {
            var tempAABB = AABBColliders[i];
            if (RayIntersectsAABB(rayOrigin, rayDir, tempAABB.Center, tempAABB.Size, out dist) && dist < distToTarget)
            {
                return false;
            }
        }
        // Check against OBBs
        for (int i = 0; i < OBBColliderCount; i++)
        {
            var tempOBB = OBBColliders[i];
            if (RayIntersectsOBB(rayOrigin, rayDir, tempOBB.Center, tempOBB.Size, tempOBB.Rotation, out dist) && dist < distToTarget)
            {
                return false;
            }
        }
        return true;
    }


    /// <summary>
    /// Check if audiotarget is visible from the ray origin.
    /// used for muffle rays towards audio targets.
    /// </summary>
    [BurstCompile]
    private bool CanRaySeeAudioTarget(float3 rayOrigin, float3 rayDir, float distToStartOrigin, int AudioTargetId)
    {
        // Check against Spheres
        for (int i = 0; i < SphereColliderCount; i++)
        {
            var tempSphere = SphereColliders[i];

            // Skip colliders that belong to the audiotarget, since we otherwise are unable to get to audioTargetPosition
            if (tempSphere.AudioTargetId == AudioTargetId) continue;

            if (RayIntersectsSphere(rayOrigin, rayDir, tempSphere.Center, tempSphere.Radius, out float dist) && dist < distToStartOrigin)
            {
                return false;
            }
        }
        // Check against AABBs
        for (int i = 0; i < AABBColliderCount; i++)
        {
            var tempAABB = AABBColliders[i];

            // Skip colliders that belong to the audiotarget, since we otherwise are unable to get to audioTargetPosition
            if (tempAABB.AudioTargetId == AudioTargetId) continue;

            if (RayIntersectsAABB(rayOrigin, rayDir, tempAABB.Center, tempAABB.Size, out float dist) && dist < distToStartOrigin)
            {
                return false;
            }
        }
        // Check against OBBs
        for (int i = 0; i < OBBColliderCount; i++)
        {
            var tempOBB = OBBColliders[i];

            // Skip colliders that belong to the audiotarget, since we otherwise are unable to get to audioTargetPosition
            if (tempOBB.AudioTargetId == AudioTargetId) continue;

            if (RayIntersectsOBB(rayOrigin, rayDir, tempOBB.Center, tempOBB.Size, tempOBB.Rotation, out float dist) && dist < distToStartOrigin)
            {
                return false;
            }
        }

        // If no colliders were hit, return true
        return true;
    }


    /// <summary>
    /// Calculate the new ray direction and origin after a hit, based on the hit collider type, so it "bounces" of the hits surface. Also subtract rayLife by aborption value of hit collider.
    /// </summary>
    [BurstCompile]
    private void ReflectRay(ColliderType hitColliderType, ColliderAABBStruct hitAABB, ColliderOBBStruct hitOBB, ColliderSphereStruct hitSphere, ref float3 cRayOrigin, ref float3 cRayDir, ref float cRayLife)
    {
        float3 normal = float3.zero;
        float absorption = 0;

        switch (hitColliderType)
        {
            case ColliderType.AABB:

                float3 localPoint = cRayOrigin - hitAABB.Center;
                float3 absPoint = math.abs(localPoint);
                float3 halfExtents = hitAABB.Size;
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

                absorption = hitAABB.MaterialProperties.Absorption;
                break;

            case ColliderType.OBB:

                float3 localHit = math.mul(math.inverse(hitOBB.Rotation), cRayOrigin - hitOBB.Center);
                float3 localHalfExtents = hitOBB.Size;

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

                normal = math.mul(hitOBB.Rotation, localNormal);
                absorption = hitOBB.MaterialProperties.Absorption;
                break;

            case ColliderType.Sphere:

                normal = math.normalize(cRayOrigin - hitSphere.Center);
                absorption = hitSphere.MaterialProperties.Absorption;
                break;

            default:
                break;
        }

        // Update next ray direction (bouncing it of the hit wall)
        cRayDir = math.reflect(cRayDir, normal);

        // Update rays new origin (hit point)
        cRayOrigin += cRayDir * EPSILON;

        // Drain raylife based on hit collider absorption
        cRayLife -= MaxRayLife * absorption;
    }
}
