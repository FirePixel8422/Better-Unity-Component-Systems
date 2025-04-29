using Unity.Burst;
using Unity.Mathematics;


[System.Serializable]
[BurstCompile]
public struct AudioRayResult
{
    public float distance;
    public int audioTargetId;

    public bool IsNull => distance == -1;

    public static AudioRayResult Null => new AudioRayResult
    {
        distance = -1,
        audioTargetId = -1,
    };

#if UNITY_EDITOR
    public float3 point;
#endif
}