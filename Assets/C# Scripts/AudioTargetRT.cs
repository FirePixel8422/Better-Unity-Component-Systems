using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.Rendering;


[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter), typeof(AudioHighPassFilter), typeof(AudioReverbFilter))]
[BurstCompile]
public class AudioTargetRT : AudioColliderGroup
{
    [Header("Audio Settings:")]
    [Space(6)]
    [SerializeField] private AudioSettings settings;

    [SerializeField] private float volumeUpdateSpeed = 0.5f;
    [SerializeField] private float lowPassUpdateSpeed = 8500;

    public int id;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private AudioHighPassFilter highPass;
    private AudioReverbFilter reverb;



    private void Start()
    {
        source = GetComponent<AudioSource>();
        lowPass = GetComponent<AudioLowPassFilter>();
        highPass = GetComponent<AudioHighPassFilter>();
        reverb = GetComponent<AudioReverbFilter>();

        settings.baseVolume = source.volume;

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


    /// <summary>
    /// Update AudioTarget at realtime based on the AudioRaytracer's data
    /// </summary>
    /// <param name="audioStrength">float between 0 and 1 equal to percent of rays that hit this audiotarget</param>
    /// <param name="panStereo">what pan stereo value (-1, 1) direction the audio came from</param>
    /// <param name="mufflePercentage">float between 0 and 1 equal to how muffled the sound should be</param>
    public void UpdateAudioSource(float audioStrength, float panStereo, float mufflePercentage = 0)
    {
        settings.volume = settings.baseVolume * audioStrength;

        source.panStereo = panStereo;

        settings.lowPassCutOffFrequency = 22000 - (17000 * mufflePercentage);
    }

    private void OnUpdate()
    {
        float deltaTime = Time.deltaTime;

        source.volume = MathematicsLogic.MoveTowards(source.volume, settings.volume, volumeUpdateSpeed * deltaTime);
        lowPass.cutoffFrequency = MathematicsLogic.MoveTowards(lowPass.cutoffFrequency, settings.lowPassCutOffFrequency, lowPassUpdateSpeed * deltaTime);
    }


    private void OnDestroy()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }
}
