using Unity.Burst;



[System.Serializable]
[BurstCompile]
public struct AudioSettings
{
    public float baseVolume;

    public float volume;
    public float lowPassCutOffFrequency;
    //public float stereoPan;
}