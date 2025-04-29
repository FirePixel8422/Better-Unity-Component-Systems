using Unity.Burst;



[System.Serializable]
[BurstCompile]
public struct AudioSettings
{
    public float volume;
    public float muffle;
    public float panStereo;

    public AudioSettings(float _volume, float _muffle, float _panStereo)
    {
        volume = _volume;
        muffle = _muffle;
        panStereo = _panStereo;
    }

    public AudioSettings(AudioSettings newSettings)
    {
        volume = newSettings.volume;
        muffle = newSettings.muffle;
        panStereo = newSettings.panStereo;
    }
}