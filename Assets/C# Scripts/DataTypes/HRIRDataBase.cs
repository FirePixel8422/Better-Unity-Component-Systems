using Unity.Burst;
using Unity.Collections;


[BurstCompile]
[System.Serializable]
public struct HRIRDatabase
{
    public NativeArray<float> hrir_l;
    public NativeArray<float> hrir_r;
    public NativeArray<float> elevations;
    public NativeArray<float> azimuths;

    public int elevationCount;
    public int azimuthCount;
    public int sampleCount;


    public HRIRDatabase(NativeArray<float> hrir_l, NativeArray<float> hrir_r, NativeArray<float> elevations, NativeArray<float> azimuths, int elevationCount, int azimuthCount, int sampleCount)
    {
        this.hrir_l = hrir_l;
        this.hrir_r = hrir_r;
        this.elevations = elevations;
        this.azimuths = azimuths;
        this.elevationCount = elevationCount;
        this.azimuthCount = azimuthCount;
        this.sampleCount = sampleCount;
    }


    /// <summary>
    /// Dispose all NativeArrays in this struct.
    /// </summary>
    [BurstCompile]
    public void Dispose()
    {
        hrir_l.DisposeIfCreated();
        hrir_r.DisposeIfCreated();
        elevations.DisposeIfCreated();
        azimuths.DisposeIfCreated();
    }
}