using System.Collections.Generic;



[System.Serializable]
public class WeakHRIRDataBase
{
    public List<float> hrir_l;
    public List<float> hrir_r;
    public List<float> elevations;
    public List<float> azimuths;

    public int elevationCount;
    public int azimuthCount;
    public int sampleCount;
}