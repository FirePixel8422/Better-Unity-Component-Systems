using Unity.Mathematics;
using UnityEngine;


public class AudioAABBCollider : AudioCollider
{
    [Header("AABB Collider Settings")]
    [SerializeField] private ColliderAABBStruct colliderStruct = ColliderAABBStruct.Default;
    private ColliderAABBStruct lastColliderStruct;


    public override ColliderType GetColliderType() => ColliderType.AABB;

    public override void AddToAudioSystem(NativeJobBatch<ColliderAABBStruct> aabbStructs, NativeJobBatch<ColliderOBBStruct> obbStructs, NativeJobBatch<ColliderSphereStruct> sphereStructs)
    {
        AudioColliderId = (short)aabbStructs.NextBatchCount;
        aabbStructs.Add(GetBakedColliderStruct());
    }
    public override void UpdateToAudioSystem(NativeJobBatch<ColliderAABBStruct> aabbStructs, NativeJobBatch<ColliderOBBStruct> obbStructs, NativeJobBatch<ColliderSphereStruct> sphereStructs)
    {
        aabbStructs[AudioColliderId] = GetBakedColliderStruct();
    }

    /// <summary>
    /// Get baked version off collider data as lightweight struct collider container for audio system usage
    /// </summary>
    private ColliderAABBStruct GetBakedColliderStruct()
    {
        ColliderAABBStruct colliderStructCopy = colliderStruct;

        Half3.Add(colliderStructCopy.Center, transform.position, out half3 mergedPosition);
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

        // Upload material properties from ScriptableObject and AudioTargetId from this component
        colliderStructCopy.MaterialProperties = AudioMaterialPropertiesSO != null ? AudioMaterialPropertiesSO.MaterialProperties : AudioMaterialProperties.Default;
        colliderStructCopy.AudioTargetId = AudioTargetId;

        return colliderStructCopy;
    }


    protected override void CheckColliderTransformation()
    {
        Vector3 cWorldPosition = cachedTransform.position;
        Vector3 cGlobalScale = IgnoreScale ? Vector3.zero : cachedTransform.lossyScale;

        if (cWorldPosition != lastWorldPosition ||
            (IgnoreScale == false && cGlobalScale != lastGlobalScale) ||
            colliderStruct != lastColliderStruct)
        {
            AudioColliderManager.UpdateColiderInSystem(this);

            UpdateSavedTransformation(cWorldPosition, cGlobalScale);
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
        ColliderAABBStruct colliderStructCopy = colliderStruct;

        Half3.Add(colliderStructCopy.Center, transform.position, out half3 mergedPosition);
        colliderStructCopy.Center = mergedPosition;

        Half3.Multiply(colliderStructCopy.Size, transform.lossyScale, out half3 scaledSize);
        colliderStructCopy.Size = scaledSize;

        Gizmos.DrawWireMesh(GlobalMeshes.cube, (float3)colliderStructCopy.Center, Quaternion.identity, (float3)colliderStructCopy.Size * 2);
    }
#endif
}