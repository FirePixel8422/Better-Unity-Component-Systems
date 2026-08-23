using Fire_Pixel.Utility;
using System;
using Unity.Mathematics;
using UnityEngine;


[RequireComponent(typeof(AudioSource))]
public class AudioSpatializer : MonoBehaviour
{
    [SerializeField] private AudioSpatializerSettingsSO settingsSO;
    [SerializeField] private AudioSpatializerSettings settings;

    [Header("References")]
    [SerializeField] private Transform listenerTransform;
    [SerializeField] private AudioReverbFilter reverb;

    [Range(0, 5)]
    [SerializeField] private float volumeMultiplier = 1;

    public AudioTargetRTSettings audioTargetSettings;

    private MuffleDSP muffleDSP;
    private BinauralDSP binauralDSP;
    private ReverbDSP reverbDSP;

    private float3 cachedLocalDir;
    private float cachedListenerDistance;
    
    private int sampleRate;



    private void OnEnable() => CallbackScheduler.RegisterCallback(CallbackType.FixedUpdate, OnLateUpdate);
    private void OnDisable() => CallbackScheduler.RegisterCallback(CallbackType.FixedUpdate, OnLateUpdate);

    private void Awake()
    {
        settings = settingsSO == null ? AudioSpatializerSettings.Default : settingsSO.settings;
        settings.Bake();

        sampleRate = AudioSettings.outputSampleRate;

        //reverb = transform.AddComponent<AudioReverbFilter>();
        //reverb.reverbPreset = AudioReverbPreset.Concerthall;
        //reverb.reverbPreset = AudioReverbPreset.User;

        muffleDSP = new MuffleDSP();
        binauralDSP = new BinauralDSP();
        reverbDSP = new ReverbDSP();

        // Activate the AudioSource after all initializations are done
        GetComponent<AudioSource>().Play();
    }

    public void UpdateSpatializer(AudioTargetRTSettings newSettings)
    {
        audioTargetSettings = newSettings;
        reverb.dryLevel = math.lerp(settings.ReverbDryLevel.min, settings.ReverbDryLevel.max, audioTargetSettings.ReverbStrength);
    }

    private void OnLateUpdate()
    {
        float3 worldDir = audioTargetSettings.PercievedAudioPosition - (float3)listenerTransform.position;
        cachedLocalDir = math.normalize(listenerTransform.InverseTransformDirection(worldDir));

        cachedListenerDistance = math.distance(transform.position, listenerTransform.position);
    }
    
    
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels != 2) return;

        muffleDSP.Process(data, settings.MuffleCurve, settings.MuffleCutoff, sampleRate, audioTargetSettings.MuffleStrength);
        reverbDSP.Process(data, settings.ReverbVolumeCurve, audioTargetSettings.ReverbVolume, settings.ReverbDryBoost);
        binauralDSP.Process(data, settings, sampleRate, cachedLocalDir, cachedListenerDistance);

        // Apply modifications to every sample of current audiosample buffer
        for (int i = 0; i < data.Length; i += 2)
        {
            float leftSample = data[i];
            float rightSample = data[i + 1];

            data[i] = leftSample * volumeMultiplier;
            data[i + 1] = rightSample * volumeMultiplier;
        }
    }

    private void OnDestroy()
    {
        settings.Dispose();
    }
}
