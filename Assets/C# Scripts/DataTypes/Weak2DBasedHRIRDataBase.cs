



using System.Collections.Generic;

[System.Serializable]
public class Weak2DBasedHRIRDataBase
{
    public Dictionary<int, float> L;
    public Dictionary<int, float> R;

    public int[] IndexMap;

    public int elevationCount;
    public int azimuthCount;
    public int sampleCount;
}