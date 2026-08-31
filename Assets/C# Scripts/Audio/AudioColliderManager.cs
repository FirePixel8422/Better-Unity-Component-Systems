using Fire_Pixel.Utility;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;


[Serializable]
public class AudioColliderManager
{
    [SerializeField] private int startCapacity = 5;

    private static List<AudioCollider> colliders;

    private static List<AudioCollider> sphereColliderRefs;
    private static List<AudioCollider> aabbColliderRefs;
    private static List<AudioCollider> obbColliderRefs;

    public static NativeJobBatch<ColliderAABBStruct> AABBColliders { get; private set; }
    public static NativeJobBatch<ColliderOBBStruct> OBBColliders { get; private set; }
    public static NativeJobBatch<ColliderSphereStruct> SphereColliders { get; private set; }

    public static Action OnColliderUpdate { get; set; }


    public void Init()
    {
        colliders = new List<AudioCollider>(startCapacity);

        sphereColliderRefs = new List<AudioCollider>(startCapacity);
        aabbColliderRefs = new List<AudioCollider>(startCapacity);
        obbColliderRefs = new List<AudioCollider>(startCapacity);

        AABBColliders = new NativeJobBatch<ColliderAABBStruct>(startCapacity, Allocator.Persistent);
        OBBColliders = new NativeJobBatch<ColliderOBBStruct>(startCapacity, Allocator.Persistent);
        SphereColliders = new NativeJobBatch<ColliderSphereStruct>(startCapacity, Allocator.Persistent);
    }
    public void Dispose()
    {
        OnColliderUpdate = null;

        AABBColliders.Dispose();
        OBBColliders.Dispose();
        SphereColliders.Dispose();
    }


    #region Add/Remove/Update Collider in system

    public static void AddColiderToSystem(AudioCollider target)
    {
        target.AddToAudioSystem(AABBColliders, OBBColliders, SphereColliders);
        colliders.Add(target);

        // Add to type-specific ref list
        switch (target.GetColliderType())
        {
            case ColliderType.Sphere:
                sphereColliderRefs.Add(target);
                break;

            case ColliderType.AABB:
                aabbColliderRefs.Add(target);
                break;

            case ColliderType.OBB:
                obbColliderRefs.Add(target);
                break;
        }
    }

    public static void RemoveColiderFromSystem(AudioCollider target)
    {
        if (target == null) return;

        short toRemoveId = target.AudioColliderId;
        ColliderType type = target.GetColliderType();

        // Remove from master list
        colliders.RemoveSwapBack(target);

        // Remove from type-specific list and native list in O1 time
        switch (type)
        {
            case ColliderType.Sphere:
                SwapRemove(SphereColliders, sphereColliderRefs, toRemoveId);
                break;

            case ColliderType.AABB:
                SwapRemove(AABBColliders, aabbColliderRefs, toRemoveId);
                break;

            case ColliderType.OBB:
                SwapRemove(OBBColliders, obbColliderRefs, toRemoveId);
                break;
        }
    }
    private static void SwapRemove<T>(NativeJobBatch<T> nativeList, List<AudioCollider> refList, short toRemoveId) where T : unmanaged
    {
        if (toRemoveId < 0 || toRemoveId >= refList.Count || toRemoveId >= nativeList.NextBatch.Length)
            return; // skip invalid remove

        int lastIndex = refList.Count - 1;

        if (toRemoveId != lastIndex)
        {
            refList[toRemoveId] = refList[lastIndex];
            refList[toRemoveId].AudioColliderId = toRemoveId;
        }

        refList.RemoveAt(lastIndex);
        nativeList.RemoveAtSwapBack(toRemoveId);
    }

    public static void UpdateColiderInSystem(AudioCollider targetCollider)
    {
        targetCollider.UpdateToAudioSystem(AABBColliders, OBBColliders, SphereColliders);
    }

    #endregion


    public static void UpdateJobBatch()
    {
        OnColliderUpdate?.Invoke();

        AABBColliders.UpdateJobBatch();
        OBBColliders.UpdateJobBatch();
        SphereColliders.UpdateJobBatch();
    }


#if UNITY_EDITOR
    [Header("DEBUG")]
    [SerializeField] private bool drawColliderGizmos = true;

    [field: SerializeField] public Color ColliderGizmosColor { get; private set; }
    [field: SerializeField] public Color AudioTargetGizmosColor { get; private set; }

    public void DrawGizmos()
    {
        AudioCollider[] colliders = GameObject.FindObjectsByType<AudioCollider>(FindObjectsSortMode.None);

        if (drawColliderGizmos)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                bool isAudioTarget = colliders[i].transform.HasComponent<AudioTargetRT>();
                Gizmos.color = isAudioTarget ?
                    AudioTargetGizmosColor :
                    ColliderGizmosColor;

                colliders[i].DrawColliderGizmo();
            }
        }
    }
#endif

}
