using Unity.Burst;
using Unity.Mathematics;



[System.Serializable]
[BurstCompile]
public struct AudioTargetData
{
    public float muffle;
    public float3 position;

    public AudioTargetData(float muffle, float3 position)
    {
        this.muffle = muffle;
        this.position = position;
    }

    public AudioTargetData(AudioTargetData newSettings)
    {
        muffle = newSettings.muffle;
        position = newSettings.position;
    }
}