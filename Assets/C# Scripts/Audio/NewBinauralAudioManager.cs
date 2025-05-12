using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


public class NewBinauralAudioManager : MonoBehaviour
{
    [SerializeField] private string hrtfName = "2D HRTF.json";

    public static Weak2DBasedHRIRDataBase hrirDatabase;
    public static int azimuthCount;
    public static int sampleCount;
    


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadHRIRDatabase()
    {
        //bit cheesy but this is the only way to ensure the manager is loaded before any other scripts
        NewBinauralAudioManager scriptInstance = FindObjectOfType<NewBinauralAudioManager>();


        string path = Path.Combine(Application.streamingAssetsPath, scriptInstance.hrtfName);
        if (!File.Exists(path))
        {
            Debug.LogError($"HRIR file not found at: {path}");
            return;
        }

        string jsonText = File.ReadAllText(path);
        hrirDatabase = JsonUtility.FromJson<Weak2DBasedHRIRDataBase>(jsonText);

        //check if data was succesfully loaded
        if (hrirDatabase == null)
        {
            Debug.LogError("Failed to parse HRIR JSON.");
            return;
        }

        azimuthCount = hrirDatabase.azimuthCount;
        sampleCount = hrirDatabase.sampleCount;

        Debug.Log($"Loaded HRIR database: {hrirDatabase.elevationCount} elevations, {hrirDatabase.azimuthCount} azimuths, {hrirDatabase.sampleCount} samples per IR.");
    }

    public static void GetHRIRData(float azimuth, float elevation, out float leftEarHRIR, out float rightEarHRIR)
    {
        int closestHRIRIndex = GetClosestMatch(azimuth, hrirDatabase.IndexMap);

        // Copy the HRIR data for the left and right ears
        leftEarHRIR = hrirDatabase.L[closestHRIRIndex];
        rightEarHRIR = hrirDatabase.R[closestHRIRIndex];
    }

    public static int GetClosestMatch(float target, int[] values)
    {
        int closest = values[0];
        float smallestDifference = math.abs(target - closest);

        int valueCount = values.Length;

        for (int i = 1; i < valueCount; i++)
        {
            float diff = math.abs(target - values[i]);

            if (diff < smallestDifference)
            {
                smallestDifference = diff;
                closest = values[i];
            }
        }

        return closest;
    }




    public static void ConvolveWithOverlap(float inputLeftSample, float inputRightSample, float hrirLeft, float hrirRight, ref float overlapLeft, ref float overlapRight, out float outputLeft, out float outputRight)
    {
        // Multiply input with HRIR and add overlap
        outputLeft = (inputLeftSample * hrirLeft) + overlapLeft;
        outputRight = (inputRightSample * hrirRight) + overlapRight;

        // Update overlap for next sample (if needed, here just store input for simplicity)
        overlapLeft = inputLeftSample;
        overlapRight = inputRightSample;
    }

}
