using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


[BurstCompile]
public class BinauralAudioManager : MonoBehaviour
{
    public static BinauralAudioManager Instance { get; private set; }

    [SerializeField] private string hrtfName = "Better HRTF.json";

    public static HRIRDatabase hrirDatabase;


    private void Awake()
    {
        Instance = this;

        LoadHRIRDatabase();
    }

    private void LoadHRIRDatabase()
    {
        string path = Path.Combine(Application.streamingAssetsPath, hrtfName);
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




    [BurstCompile]
    public static void ApplyHRIRToAudio(in NativeArray<float> leftEarHRIR, in NativeArray<float> rightEarHRIR, in NativeArray<float> audioSignal, ref NativeArray<float> outputLeftEar, ref NativeArray<float> outputRightEar)
    {
        int sampleCount = leftEarHRIR.Length;

        // Convolution: Apply the HRIR to the audio signal for both left and right ears
        for (int i = 0; i < audioSignal.Length; i++)
        {
            // Apply left ear HRIR
            for (int j = 0; j < sampleCount; j++)
            {
                if (i - j >= 0) // Make sure we don't go out of bounds
                {
                    outputLeftEar[i] += audioSignal[i - j] * leftEarHRIR[j];
                }
            }

            // Apply right ear HRIR
            for (int j = 0; j < sampleCount; j++)
            {
                if (i - j >= 0) // Make sure we don't go out of bounds
                {
                    outputRightEar[i] += audioSignal[i - j] * rightEarHRIR[j];
                }
            }
        }
    }



    // Sample function to get HRIR data for a given direction
    [BurstCompile]
    public static void GetHRIRDataForDirection(float azimuth, float elevation, in HRIRDatabase hrirDatabase, ref NativeArray<float> leftEarHRIR, ref NativeArray<float> rightEarHRIR)
    {
        // Convert the direction (azimuth, elevation) to indices
        int elevationIndex = 0;
        int azimuthIndex = 0;
        DirectionToIndices(azimuth, elevation, hrirDatabase.elevationCount, hrirDatabase.azimuthCount, ref elevationIndex, ref azimuthIndex);

        // Fetch the corresponding HRIR data
        int sampleCount = hrirDatabase.sampleCount;
        int index = elevationIndex * hrirDatabase.azimuthCount + azimuthIndex;

        // Manually copy the HRIR data for both ears (left and right) to avoid using CopyFrom
        for (int i = 0; i < sampleCount; i++)
        {
            leftEarHRIR[i] = hrirDatabase.hrir_l[index * sampleCount + i];
            rightEarHRIR[i] = hrirDatabase.hrir_r[index * sampleCount + i];
        }
    }

    [BurstCompile]
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



    private void OnDestroy()
    {
        // Clean up using the extension method
        hrirDatabase.hrir_l.DisposeIfCreated();
        hrirDatabase.hrir_r.DisposeIfCreated();
        hrirDatabase.elevations.DisposeIfCreated();
        hrirDatabase.azimuths.DisposeIfCreated();

        // Nullify the database to avoid accidental access after disposal
        hrirDatabase = default;
    }
}
