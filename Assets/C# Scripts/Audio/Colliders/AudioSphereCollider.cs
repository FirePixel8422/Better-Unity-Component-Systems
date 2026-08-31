using Unity.Mathematics;
using UnityEngine;


public class AudioSphereCollider : AudioCollider
{
    [Header("Sphere Collider Settings")]
    [SerializeField] private ColliderSphereStruct colliderStruct = ColliderSphereStruct.Default;
    private ColliderSphereStruct lastColliderStruct;


    public override ColliderType GetColliderType() => ColliderType.Sphere;

    public override void AddToAudioSystem(NativeJobBatch<ColliderAABBStruct> aabbStructs, NativeJobBatch<ColliderOBBStruct> obbStructs, NativeJobBatch<ColliderSphereStruct> sphereStructs)
    {
        AudioColliderId = (short)sphereStructs.NextBatchCount;
        sphereStructs.Add(GetBakedColliderStruct());
    }
    public override void UpdateToAudioSystem(NativeJobBatch<ColliderAABBStruct> aabbStructs, NativeJobBatch<ColliderOBBStruct> obbStructs, NativeJobBatch<ColliderSphereStruct> sphereStructs)
    {
        sphereStructs[AudioColliderId] = GetBakedColliderStruct();
    }

    /// <summary>
    /// Get baked version off collider data as lightweight struct collider container for audio system usage
    /// </summary>
    private ColliderSphereStruct GetBakedColliderStruct()
    {
        ColliderSphereStruct colliderStructCopy = colliderStruct;

        Half3.Add(colliderStructCopy.Center, transform.position, out half3 mergedPosition);
        colliderStructCopy.Center = mergedPosition;

        if (IgnoreScale)
        {
            Half.Multiply(colliderStructCopy.Radius, GetLargestComponent(lastGlobalScale), out half scaledRadius);
            colliderStructCopy.Radius = scaledRadius;
        }
        else
        {
            Half.Multiply(colliderStructCopy.Radius, GetLargestComponent(transform.lossyScale), out half scaledRadius);
            colliderStructCopy.Radius = scaledRadius;
        }

        // Upload material properties from ScriptableObject and AudioTargetId from this component
        colliderStructCopy.MaterialProperties = AudioMaterialPropertiesSO != null ? AudioMaterialPropertiesSO.MaterialProperties : AudioMaterialProperties.Default;
        colliderStructCopy.AudioTargetId = AudioTargetId;

        return colliderStructCopy;
    }
    private half GetLargestComponent(Vector3 input)
    {
        return (half)math.max(
            input.x, 
            math.max(input.y,
            input.z));
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
        ColliderSphereStruct colliderStructCopy = colliderStruct;

        Half3.Add(colliderStructCopy.Center, transform.position, out half3 mergedPosition);
        colliderStructCopy.Center = mergedPosition;
        
        Half.Multiply(colliderStructCopy.Radius, GetLargestComponent(transform.lossyScale), out half scaledRadius);
        colliderStructCopy.Radius = scaledRadius;

        Gizmos.DrawWireSphere((float3)colliderStructCopy.Center, (float)colliderStructCopy.Radius);
    }
#endif
}