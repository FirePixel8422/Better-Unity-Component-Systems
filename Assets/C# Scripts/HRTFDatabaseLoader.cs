using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;


public class HRTFDatabaseLoader : MonoBehaviour
{
    [Header("Path to your HRTF JSON file (relative to StreamingAssets)")]
    public string jsonFileName = "output_hrtf.json";

    //[HideInInspector]
    public HRTFDatabase hrtfDatabase;

    public static HRTFDatabaseLoader Instance;

    private void Awake()
    {
        Instance = this;

        DontDestroyOnLoad(gameObject);

        LoadHRTFDatabase();
    }

    private void LoadHRTFDatabase()
    {
        if (!System.IO.File.Exists(jsonFileName))
        {
            Debug.LogError($"HRTF file not found at: {jsonFileName}");
            return;
        }

        string jsonText = System.IO.File.ReadAllText(jsonFileName);
        hrtfDatabase = JsonUtility.FromJson<HRTFDatabase>(jsonText);

        if (hrtfDatabase != null)
        {
            print($"Loaded HRTF database: {hrtfDatabase.positions.Count} positions,");

            if (hrtfDatabase.ir_data.Count > 0)
            {
                print($"Each IR has {hrtfDatabase.ir_data[0].Count} receivers, each with {hrtfDatabase.ir_data[0][0]} samples.");
            }
        }
        else
        {
            Debug.LogError("Failed to parse HRTF JSON.");
        }
    }


    private void OnDrawGizmosSelected()
    {
        if (hrtfDatabase == null || hrtfDatabase.positions == null)
            return;
        Gizmos.color = Color.cyan;
        foreach (var position in hrtfDatabase.positions)
        {
            Gizmos.DrawWireSphere(position * 3, 0.1f);
        }
    }
}

// Class matching the JSON structure
[Serializable]
public class HRTFDatabase
{
    public List<float3> positions;
    public List<List<float3>> ir_data;
}
