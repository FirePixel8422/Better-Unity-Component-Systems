using Unity.Mathematics;
using UnityEngine;


public class AudioOBBCollider : AudioCollider
{
    [Tooltip("Set to true to use the gameObjects rotation for the collider")]
    [SerializeField] private bool includeGameObjectRotation = true;
    [SerializeField] private Vector3 rotationEulerOffset;

    [Header("OBB Collider Settings")]
    [SerializeField] private ColliderOBBStruct colliderStruct = ColliderOBBStruct.Default;
    private ColliderOBBStruct lastColliderStruct;
    private Quaternion lastFinalRotation;


    public override ColliderType GetColliderType() => ColliderType.OBB;

    public override void AddToAudioSystem(NativeJobBatch<ColliderAABBStruct> aabbStructs, NativeJobBatch<ColliderOBBStruct> obbStructs, NativeJobBatch<ColliderSphereStruct> sphereStructs)
    {
        AudioColliderId = (short)obbStructs.NextBatchCount;
        obbStructs.Add(GetBakedColliderStruct());
    }
    public override void UpdateToAudioSystem(NativeJobBatch<ColliderAABBStruct> aabbStructs, NativeJobBatch<ColliderOBBStruct> obbStructs, NativeJobBatch<ColliderSphereStruct> sphereStructs)
    {
        obbStructs[AudioColliderId] = GetBakedColliderStruct();
    }

    /// <summary>
    /// Get baked version off collider data as lightweight struct collider container for audio system usage
    /// </summary>
    private ColliderOBBStruct GetBakedColliderStruct()
    {
        ColliderOBBStruct colliderStructCopy = colliderStruct;

        if (includeGameObjectRotation)
        {
            colliderStructCopy.Rotation *= transform.rotation;
        }
        if (rotationEulerOffset != Vector3.zero)
        {
            colliderStructCopy.Rotation *= Quaternion.Euler(rotationEulerOffset);
        }

        Half3.Add(transform.rotation * (float3)colliderStructCopy.Center, transform.position, out half3 mergedPosition);
        colliderStructCopy.Center = mergedPosition;

        if (IgnoreScale)
        {
            Half3.Multiply(colliderStructCopy.Size, lastGlobalScale, out half3 scaledSize);
            colliderStructCopy.Size = scaledSize;
        }
        else
        {
            Half3.Multiply(colliderStructCopy.Size, transform.lossyScale, out half3 scaledSize);
            colliderStructCopy.Size = scaledSize;
        }

        // Invert rotation for audio system calculation optimization later
        colliderStructCopy.Rotation = math.inverse(colliderStructCopy.Rotation);

        // Upload material properties from ScriptableObject and AudioTargetId from this component
        colliderStructCopy.MaterialProperties = AudioMaterialPropertiesSO != null ? AudioMaterialPropertiesSO.MaterialProperties : AudioMaterialProperties.Default;
        colliderStructCopy.AudioTargetId = AudioTargetId;

        return colliderStructCopy;
    }


    protected override void CheckColliderTransformation()
    {
        cachedTransform.GetPositionAndRotation(out Vector3 cWorldPosition, out Quaternion finalRotation);
        finalRotation *= Quaternion.Euler(rotationEulerOffset);

        Vector3 cGlobalScale = IgnoreScale ? Vector3.zero : cachedTransform.lossyScale;

        if (cWorldPosition != lastWorldPosition ||
            finalRotation != lastFinalRotation ||
            (IgnoreScale == false && cGlobalScale != lastGlobalScale) ||
            colliderStruct != lastColliderStruct)
        {
            AudioColliderManager.UpdateColiderInSystem(this);

            UpdateSavedTransformation(cWorldPosition, cGlobalScale);
            lastFinalRotation = finalRotation;
        }
    }
    protected override void UpdateSavedTransformation(Vector3 cWorldPosition, Vector3 cGlobalScale)
    {
        base.UpdateSavedTransformation(cWorldPosition, cGlobalScale);
        lastColliderStruct = colliderStruct;
    }


#if UNITY_EDITOR
    public override void DrawColliderGizmo()
    {
        ColliderOBBStruct colliderStructCopy = colliderStruct;

        if (includeGameObjectRotation)
        {
            colliderStructCopy.Rotation *= transform.rotation;
        }
        if (rotationEulerOffset != Vector3.zero)
        {
            colliderStructCopy.Rotation *= Quaternion.Euler(rotationEulerOffset);
        }

        Half3.Add(transform.rotation * (float3)colliderStructCopy.Center, transform.position, out half3 mergedPosition);
        colliderStructCopy.Center = mergedPosition;

        Half3.Multiply(colliderStructCopy.Size, transform.lossyScale, out half3 scaledSize);
        colliderStructCopy.Size = scaledSize;

        Gizmos.DrawWireMesh(GlobalMeshes.cube, (float3)colliderStructCopy.Center, colliderStructCopy.Rotation, (float3)colliderStructCopy.Size * 2);
    }
#endif
}