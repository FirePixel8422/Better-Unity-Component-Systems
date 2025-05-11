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


    // Round elevation to the nearest multiple of 10
    public static int RoundToPowerOf10(float elevation)
    {
        return Mathf.RoundToInt(elevation / 10f) * 10;
    }

    // Round azimuth to the nearest multiple of 5
    public static int RoundToPowerOf5(float azimuth)
    {
        return Mathf.RoundToInt(azimuth / 5f) * 5;
    }


    public static void GetHRIRData(float azimuth, float elevation, ref float[] leftEarHRIR, ref float[] rightEarHRIR)
    {
        // Round azimuth and elevation
        int roundedAzimuth = RoundToPowerOf5(azimuth);
        int roundedElevation = RoundToPowerOf10(elevation);

        // Ensure the rounded values are within valid ranges
        if (roundedAzimuth < 0 || roundedAzimuth >= hrirDatabase.azimuthCounts[roundedElevation])
        {
            Debug.LogError($"Azimuth {roundedAzimuth} out of range for elevation {roundedElevation}");
            return;
        }

        if (roundedElevation < 0 || roundedElevation >= hrirDatabase.elevationCount)
        {
            Debug.LogError($"Elevation {roundedElevation} out of range.");
            return;
        }

        // Get the index for the HRIR data based on rounded azimuth and elevation
        int azimuthIndex = roundedAzimuth; // Use the rounded azimuth as the index for this elevation
        int sampleCount = hrirDatabase.L[roundedElevation, azimuthIndex].Length; // Variable sample count for each azimuth/elevation

        // Copy the HRIR data for the left and right ears
        Array.Copy(hrirDatabase.L[roundedElevation][azimuthIndex], leftEarHRIR, sampleCount);
        Array.Copy(hrirDatabase.R[roundedElevation][azimuthIndex], rightEarHRIR, sampleCount);
    }






    public static void ConvolveWithOverlap(float[] inputLeft, float[] inputRight, float[] hrirLeft, float[] hrirRight, float[] overlapLeft, float[] overlapRight, float[] outputLeft, float[] outputRight)
    {
        int numFrames = inputLeft.Length; // assuming both input arrays have the same length
        int hrirLength = hrirLeft.Length;

        int overlapLength = overlapLeft.Length;

        // Convolve both left and right channels in the same loop
        for (int i = 0; i < numFrames; i++)
        {
            float leftSample = 0f;
            float rightSample = 0f;

            for (int j = 0; j < hrirLength; j++)
            {
                int inputIndex = i - j;

                // Left channel
                if (inputIndex >= 0)
                    leftSample += inputLeft[inputIndex] * hrirLeft[j];
                else
                    leftSample += overlapLeft[overlapLength + inputIndex] * hrirLeft[j];

                // Right channel
                if (inputIndex >= 0)
                    rightSample += inputRight[inputIndex] * hrirRight[j];
                else
                    rightSample += overlapRight[overlapLength + inputIndex] * hrirRight[j];
            }

            // Store the processed samples in the output arrays
            outputLeft[i] = leftSample;
            outputRight[i] = rightSample;
        }

        // Update overlap buffer for next frame (for left and right channels)
        for (int i = 0; i < overlapLength; i++)
        {
            int leftSrcIndex = numFrames - overlapLength + i;
            overlapLeft[i] = (leftSrcIndex >= 0) ? inputLeft[leftSrcIndex] : 0f;

            int rightSrcIndex = numFrames - overlapLength + i;
            overlapRight[i] = (rightSrcIndex >= 0) ? inputRight[rightSrcIndex] : 0f;
        }
    }
}
