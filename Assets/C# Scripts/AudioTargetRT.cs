using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter), typeof(AudioHighPassFilter), typeof(AudioReverbFilter))]
[BurstCompile]
public class AudioTargetRT : AudioColliderGroup
{
    [Header("Audio Settings:")]
    [Space(6)]
    [SerializeField] private AudioSettings settings;
    [SerializeField] private float baseVolume;

    [SerializeField] private float volumeUpdateSpeed = 0.5f;
    [SerializeField] private float lowPassUpdateSpeed = 8500;

    public int id;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private AudioHighPassFilter highPass;
    private AudioReverbFilter reverb;



    [BurstCompile]
    private void Start()
    {
        source = GetComponent<AudioSource>();
        lowPass = GetComponent<AudioLowPassFilter>();
        highPass = GetComponent<AudioHighPassFilter>();
        reverb = GetComponent<AudioReverbFilter>();

        baseVolume = source.volume;
        settings.volume = baseVolume;

        UpdateScheduler.Register(OnUpdate);
    }


    #region Get Colliders Override Method

    [BurstCompile]
    /// <summary>
    /// Add all colliders of this AudioGroup to the native arrays of the custom physics engine.
    /// override: also set the audioTargetId of all colliders to the id of this script
    /// </summary>
    public override void GetColliders(
        NativeArray<ColliderAABBStruct> _AABBs, int AABBsStartIndex,
        NativeArray<ColliderOBBStruct> _OBBs, int OBBsStartIndex,
        NativeArray<ColliderSphereStruct> _spheres, int spheresStartIndex)
    {
        int colliderCount = AABBCount;

        //add all boxes to the native array
        for (int i = 0; i < colliderCount; i++)
        {
            ColliderAABBStruct box = axisAlignedBoxes[i];

            //account for transform position and set groupId
            box.center += (float3)transform.position;
            box.size *= transform.localScale;

            box.audioTargetId = id;

            _AABBs[AABBsStartIndex + i] = box;
        }

        colliderCount = OBBCount;

        //add all boxes to the native array
        for (int i = 0; i < colliderCount; i++)
        {
            ColliderOBBStruct box = orientedBoxes[i];

            //account for transform position and set groupId
            box.center += (float3)transform.position;
            box.rotation = transform.rotation;
            box.size *= transform.localScale;

            box.audioTargetId = id;

            _OBBs[OBBsStartIndex + i] = box;
        }

        colliderCount = SphereCount;

        //add all spheres to the native array
        for (int i = 0; i < colliderCount; i++)
        {
            ColliderSphereStruct sphere = spheres[i];

            //account for transform position and set groupId
            sphere.center += (float3)transform.position;
            sphere.radius *= math.max(transform.localScale.x, math.max(transform.localScale.y, transform.localScale.z));

            sphere.audioTargetId = id;

            _spheres[spheresStartIndex + i] = sphere;
        }
    }

    #endregion


    [BurstCompile]
    /// <summary>
    /// Update AudioTarget at realtime based on the AudioRaytracer's data
    /// </summary>
    /// <param name="audioStrength">float between 0 and 1 equal to percent of rays that hit this audiotarget</param>
    /// <param name="panStereo">what pan stereo value (-1, 1) direction the audio came from</param>
    /// <param name="mufflePercentage">float between 0 and 1 equal to how muffled the sound should be, 0 is 100% muffled</param>
    public void UpdateAudioSource(AudioSettings newSettings)
    {
        newSettings.volume = baseVolume;

        //DEBUG






        settings = newSettings;

        //0 = 100% muffled audio
        settings.muffle = 250 + curve.Evaluate(newSettings.muffle) * 21750f;

        source.panStereo = newSettings.panStereo;
    }

    //BAD
    //BAD
    //BAD
    //BAD
    //BAD
    //BAD

    public AnimationCurve curve;



    [BurstCompile]
    private void OnUpdate()
    {
        float deltaTime = Time.deltaTime;

        //maybe make this method smarter, make it so it takes MAX volumeUpdatepeed to change from a to b

        source.volume = MathematicsLogic.MoveTowards(source.volume, settings.volume, volumeUpdateSpeed * deltaTime);
        lowPass.cutoffFrequency = MathematicsLogic.MoveTowards(lowPass.cutoffFrequency, settings.muffle, math.max(lowPassUpdateSpeed, settings.muffle - lowPass.cutoffFrequency) * deltaTime);
    }


    [BurstCompile]
    private void OnDestroy()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }
}
