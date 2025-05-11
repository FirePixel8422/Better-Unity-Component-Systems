using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


public class BinauralAudioManager : MonoBehaviour
{
    [SerializeField] private string hrtfName = "Better HRTF.json";

    public static HRIRDatabase hrirDatabase;
    


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadHRIRDatabase()
    {
        //bit cheesy but this is the only way to ensure the manager is loaded before any other scripts
        BinauralAudioManager scriptInstance = FindObjectOfType<BinauralAudioManager>();


        string path = Path.Combine(Application.streamingAssetsPath, scriptInstance.hrtfName);
        if (!File.Exists(path))
        {
            Debug.LogError($"HRIR file not found at: {path}");
            return;
        }

        string jsonText = File.ReadAllText(path);
        WeakHRIRDataBase weakData = JsonUtility.FromJson<WeakHRIRDataBase>(jsonText);

        //check if data was succesfully loaded
        if (weakData == null)
        {
            Debug.LogError("Failed to parse HRIR JSON.");
            return;
        }

        hrirDatabase = ConvertToNative(weakData);

        //immediately discard weak data
        weakData = null;

        Debug.Log($"Loaded HRIR database: {hrirDatabase.elevationCount} elevations, {hrirDatabase.azimuthCount} azimuths, {hrirDatabase.sampleCount} samples per IR.");
        Debug.Log($"Loaded HRIR database: {hrirDatabase.elevations.Length} elevations total, {hrirDatabase.azimuths.Length} azimuths total.");
    }


    /// <summary>
    /// Convert weak managed data to native based data container
    /// </summary>
    public static HRIRDatabase ConvertToNative(WeakHRIRDataBase jsonData)
    {
        return new HRIRDatabase(
            new NativeArray<float>(jsonData.hrir_l.ToArray(), Allocator.Persistent),
            new NativeArray<float>(jsonData.hrir_r.ToArray(), Allocator.Persistent),
            new NativeArray<float>(jsonData.elevations.ToArray(), Allocator.Persistent),
            new NativeArray<float>(jsonData.azimuths.ToArray(), Allocator.Persistent),
            jsonData.elevationCount,
            jsonData.azimuthCount,
            jsonData.sampleCount
        );
    }



    // Sample function to get HRIR data for a given direction
    public static void GetHRIRDataForDirection(float azimuth, float elevation, float[] leftEarHRIR, float[] rightEarHRIR)
    {
        // Convert the direction (azimuth, elevation) to indices
        int elevationIndex = 0;
        int azimuthIndex = 0;
        DirectionToIndices(azimuth, elevation, hrirDatabase.elevationCount, hrirDatabase.azimuthCount, ref elevationIndex, ref azimuthIndex);

        // Fetch the corresponding HRIR data
        int sampleCount = hrirDatabase.sampleCount;
        int index = azimuthIndex * hrirDatabase.elevationCount + elevationIndex;

        // Manually copy the HRIR data for both ears (left and right) to avoid using CopyFrom
        for (int i = 0; i < sampleCount; i++)
        {
            leftEarHRIR[i] = hrirDatabase.hrir_l[index * sampleCount + i];
            rightEarHRIR[i] = hrirDatabase.hrir_r[index * sampleCount + i];
        }
    }

    private static void DirectionToIndices(float azimuth, float elevation, int elevationCount, int azimuthCount, ref int elevationIndex, ref int azimuthIndex)
    {
        if(azimuth > 180f)
        {
            azimuth -= 360f; // Normalize azimuth to the range [-180, 180]
        }
        else if (azimuth < -180f)
        {
            azimuth += 360f; // Normalize azimuth to the range [-180, 180]
        }

        // Normalize the azimuth (if needed)
        azimuth = math.clamp(azimuth, -180f, 180f); // Clamp the azimuth to the valid range (example: -180 to 180)
        elevation = math.clamp(elevation, -90f, 90f); // Clamp the elevation to the valid range (-90 to 90)

        // Map azimuth to the nearest index
        azimuthIndex = (int)((azimuth + 180f) / 360f * azimuthCount);

        // Map elevation to the nearest index
        elevationIndex = (int)((elevation + 90f) / 180f * elevationCount);
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





    private void OnDestroy()
    {
        // Clean up using the extension method
        hrirDatabase.Dispose();

        // Nullify the database to avoid accidental access after disposal
        hrirDatabase = default;
    }


    private void OnDrawGizmosSelected()
    {
        if (hrirDatabase.hrir_l.IsCreated == false)
            return;

        Gizmos.color = Color.cyan;

        int cGizmoCount = 0;

        for (int i = 0; i < hrirDatabase.elevations.Length; i++)
        {
            for (int j = 0; j < hrirDatabase.azimuths.Length; j++)
            {
                cGizmoCount++;

                if(cGizmoCount > 1000)
                    return;

                float azimuth = hrirDatabase.azimuths[j];
                float elevation = hrirDatabase.elevations[i];
                Vector3 direction = Quaternion.Euler(elevation, azimuth, 0) * Vector3.forward;

                // Position at 10 units away from origin
                Vector3 pointPosition = transform.position + direction * 10f;

                // Draw a small sphere at the point
                Gizmos.DrawSphere(pointPosition, 0.15f);
            }
        }
    }

}
