using Unity.Mathematics;
using UnityEngine;


[RequireComponent(typeof(AudioRayTracer))]
public class AudioRayDebugger : MonoBehaviour
{
    [SerializeField] private Color originColor;
    [EditorReadOnly] public float3 RayOrigin;

    [field: WarningIf(ComparisonType.True, "Debugging enabled, may affect performance")]
    [field: SerializeField] public bool EnableDebugging { get; private set; }

    [Range(0, 50000)]
    [SerializeField] private int gizmoLimit;

    [ShowIf(nameof(EnableDebugging))]
    public Wrapper DebugData;

    [System.Serializable]
    public struct Wrapper
    {
        public bool DrawRayHitsGizmos;
        [ShowIf(nameof(DrawRayHitsGizmos))]
        public Color RayHitColor;

        public bool DrawEchoRayGizmos;
        [ShowIf(nameof(DrawEchoRayGizmos))]
        public Color EchoRayColor;

        public bool DrawRayTrailsGizmos;
        [ShowIf(nameof(DrawRayTrailsGizmos))]
        public Color RayTrailColor;

        [EditorReadOnly] public float3[] AudioTargetPositions;
        [EditorReadOnly] public int MaxMuffleHits;
        [EditorReadOnly] public ushort[] MuffleRayHits;
        [EditorReadOnly] public float[] MufflePercent01;

        [EditorReadOnly] public AudioRayHitResult[] RayResults;
        [EditorReadOnly] public byte[] RayResultCounts;
        [EditorReadOnly] public half[] EchoRayDistances;
    }



    private void Reset()
    {
        GetComponent<AudioRayTracer>().Debugger = this;
    }

    private void OnDrawGizmos()
    {
        float3 rayOrigin = RayOrigin;

        Gizmos.color = originColor;
        Gizmos.DrawWireCube(rayOrigin, Vector3.one * 0.25f);
        Gizmos.DrawWireCube(rayOrigin, Vector3.one * 0.2f);

        if (Application.isPlaying == false) return;

        if (DebugData.RayResults.HasData() && EnableDebugging)
        {
            float3 prevRayHitPoint;

            int maxRayHits = DebugData.RayResults.Length / DebugData.RayResultCounts.Length;
            int setResultAmountsCount = DebugData.RayResultCounts.Length;
            int cSetResultCount;

            if (setResultAmountsCount * maxRayHits > gizmoLimit)
            {
                DebugLogger.LogWarning($"Max Gizmos Reached: '{gizmoLimit}', please turn of gizmos to not fry CPU");

                setResultAmountsCount = gizmoLimit / maxRayHits;
            }

            for (int i = 0; i < setResultAmountsCount; i++)
            {
                cSetResultCount = DebugData.RayResultCounts[i];
                prevRayHitPoint = rayOrigin;

                // Ray hit markers and trails
                for (int i2 = 0; i2 < cSetResultCount; i2++)
                {
                    int cRayHitId = i * maxRayHits + i2;
                    float3 cRayHitPoint = DebugData.RayResults[cRayHitId].HitPoint;

                    if (DebugData.DrawRayHitsGizmos)
                    {
                        Gizmos.color = DebugData.RayHitColor;
                        Gizmos.DrawWireCube(cRayHitPoint, Vector3.one * 0.1f);
                    }
                    if (DebugData.DrawRayTrailsGizmos)
                    {
                        Gizmos.color = DebugData.RayTrailColor;
                        Gizmos.DrawLine(prevRayHitPoint, cRayHitPoint);
                        prevRayHitPoint = cRayHitPoint;
                    }
                }
            }

            for (int i = 0; i < DebugData.RayResults.Length; i++)
            {
                if (DebugData.DrawEchoRayGizmos)
                {
                    if (DebugData.EchoRayDistances[i] != 0)
                    {
                        Gizmos.color = DebugData.EchoRayColor;
                        Gizmos.DrawLine(rayOrigin, (float3)DebugData.RayResults[i].HitPoint);
                    }
                }
            }
        }
    }
}