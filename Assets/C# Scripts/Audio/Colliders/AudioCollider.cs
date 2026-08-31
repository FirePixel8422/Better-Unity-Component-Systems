using UnityEngine;


public abstract class AudioCollider : MonoBehaviour
{
    [SerializeField] protected AudioMaterialPropertiesSO AudioMaterialPropertiesSO;

    [Header("Can increase performance:")]
    [Tooltip("Set this to true if collider data does not change at runtime")]
    [SerializeField] protected bool isStatic = true;

    [Tooltip("Set this to true if collider never scales at runtime")]
    [SerializeField] protected bool ignoreScale = true;

    public bool IsStatic => isStatic;
    public bool IgnoreScale => ignoreScale;

    [EditorReadOnly] public short AudioColliderId;
    [EditorReadOnly] public short AudioTargetId;

    [SerializeField, EditorReadOnly] protected Transform cachedTransform;
    [SerializeField, EditorReadOnly] protected Vector3 lastWorldPosition;
    [SerializeField, EditorReadOnly] protected Vector3 lastGlobalScale;


    private void Awake()
    {
        cachedTransform = transform;

        if (TryGetComponent(out AudioTargetRT audioTargetRT))
        {
            audioTargetRT.Id.OnValueChanged += UpdateAudioTargetId;
        }
        else
        {
            AudioTargetId = -1;
        }

        if (IsStatic == false)
        {
            AudioColliderManager.OnColliderUpdate += CheckColliderTransformation;
        }
        lastWorldPosition = transform.position;
        lastGlobalScale = transform.lossyScale;
    }
    private void UpdateAudioTargetId(short newId)
    {
        AudioTargetId = newId;
        AudioColliderManager.UpdateColiderInSystem(this);
    }

    protected virtual void UpdateSavedTransformation(Vector3 cWorldPosition, Vector3 cGlobalScale)
    {
        lastWorldPosition = cWorldPosition;

        if (IgnoreScale) return;
        lastGlobalScale = cGlobalScale;
    }

    public virtual ColliderType GetColliderType() => ColliderType.None;

    /// <summary>
    /// Add audio collider as struct data into the corresponding native array at correct index and increment index
    /// </summary>
    public virtual void AddToAudioSystem(NativeJobBatch<ColliderAABBStruct> aabbStructs, NativeJobBatch<ColliderOBBStruct> obbStructs, NativeJobBatch<ColliderSphereStruct> sphereStructs) { }

    /// <summary>
    /// Update audio collider as struct data into the corresponding native array at correct index based on assigned AudioColliderId
    /// </summary>
    public virtual void UpdateToAudioSystem(NativeJobBatch<ColliderAABBStruct> aabbStructs, NativeJobBatch<ColliderOBBStruct> obbStructs, NativeJobBatch<ColliderSphereStruct> sphereStructs) { }


    private void OnEnable() => AudioColliderManager.AddColiderToSystem(this);
    private void OnDisable() => AudioColliderManager.RemoveColiderFromSystem(this);

    private void OnDestroy()
    {
        if (IsStatic == false)
        {
            AudioColliderManager.OnColliderUpdate -= CheckColliderTransformation;
        }
    }

    protected virtual void CheckColliderTransformation() { }


#if UNITY_EDITOR
    private bool prevIsStatic;
    public void SetIsStaticValue(bool value)
    {
        isStatic = value;
        prevIsStatic = value;
    }

    // Enforce equal staticness on all attached colliders and AudioTargetRT on the same gameobject
    private void OnValidate()
    {
        if (Application.isPlaying || prevIsStatic == IsStatic) return;

        prevIsStatic = IsStatic;

        if (TryGetComponent(out AudioTargetRT audiotarget))
        {
            audiotarget.SetIsStaticValue(IsStatic);
        }

        AudioCollider[] audioColliders = GetComponents<AudioCollider>();
        for (int i = 0; i < audioColliders.Length; i++)
        {
            if (audioColliders[i] == this) return;

            audioColliders[i].SetIsStaticValue(IsStatic);
        }

        DebugLogger.Log($"Possible AudioTarget and all attached AudioColliders set to the same static value", audioColliders.Length != 1 || audiotarget != null);
    }


    private void OnDrawGizmosSelected()
    {
        bool isAudioTarget = transform.HasComponent<AudioTargetRT>();
        Gizmos.color = isAudioTarget ?
            AudioRaytracingManager.ColliderManager.AudioTargetGizmosColor :
            AudioRaytracingManager.ColliderManager.ColliderGizmosColor;

        DrawColliderGizmo();
    }
    public virtual void DrawColliderGizmo() { }
#endif
}