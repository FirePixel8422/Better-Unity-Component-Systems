using Unity.Burst;
using Unity.Mathematics;



[System.Serializable]
[BurstCompile]
public struct AudioTargetData
{
    public float muffle;

    public float echoStrength;
    public float echoTime;

    public float3 position;

    public AudioTargetData(float muffle, float echoStrength, float echoTime, float3 position)
    {
        this.muffle = muffle;

        this.echoStrength = echoStrength;
        this.echoTime = echoTime;

        this.position = position;
    }

    public AudioTargetData(AudioTargetData newSettings)
    {
        muffle = newSettings.muffle;

        echoStrength = newSettings.echoStrength;
        echoTime = newSettings.echoTime;

        position = newSettings.position;
    }
}