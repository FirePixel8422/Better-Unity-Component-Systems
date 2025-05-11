using System.IO;
using Unity.Mathematics;
using UnityEngine;

public class TEMP_SofaLoader : MonoBehaviour
{
    [SerializeField] private string hrtfFileName = "SofaData.json"; // Your JSON file
    private float[] irData;
    private float3[] positions;



    [System.Serializable]
    public class HRIRData
    {
        public float3[] positions;
        public float[] ir_data;  // Flattened list of IR data
    }

    private void Start()
    {
        LoadAndPrintHRIRData();
    }

    private void LoadAndPrintHRIRData()
    {
        // Path to the file in StreamingAssets
        string path = Path.Combine(Application.streamingAssetsPath, hrtfFileName);

        // Check if the file exists
        if (!File.Exists(path))
        {
            Debug.LogError($"HRIR file not found at: {path}");
            return;
        }

        // Read the content of the file
        string jsonText = File.ReadAllText(path);

        // Deserialize the JSON into the HRIRData object
        HRIRData hrirData = JsonUtility.FromJson<HRIRData>(jsonText);

        // Ensure the data was loaded correctly
        if (hrirData == null)
        {
            Debug.LogError("Failed to parse HRIR JSON.");
            return;
        }

        // Assign the loaded IR data to the irData array
        irData = hrirData.ir_data;
        positions = hrirData.positions;

        // Calculate the number of positions and samples per position
        int totalDataCount = irData.Length;
        int positionCount = hrirData.positions.Length;
        int samplesPerPosition = totalDataCount / positionCount;

        // Print the number of positions and samples per position
        Debug.Log($"Total IR Data Length: {totalDataCount}");
        Debug.Log($"Positions: {positionCount}");
        Debug.Log($"Samples per Position: {samplesPerPosition}");

        // Verify the total data length matches the expected size (positions * samples per position)
        if (totalDataCount != positionCount * samplesPerPosition)
        {
            Debug.LogError("Data length mismatch! Please check the structure of your IR data.");
        }
    }


    private void OnDrawGizmosSelected()
    {
        if (irData == null || irData.Length == 0) return;

        // Visualize positions with spheres in the editor
        for (int i = 0; i < positions.Length; i++)
        {
            // Calculate the position of the sphere (this is a simple layout, assuming positions are spaced equally)
            Vector3 position = positions[i]; // You can change this layout logic depending on your data

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(position, 0.5f);  // Draw a small sphere for each position
        }
    }
}
