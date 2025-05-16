using Unity.Burst;
using UnityEngine;



[Tooltip("LOS checks to the player")]
public struct EchoRayResult
{
    public float distanceTraveled;

    public void Reset()
    {
        distanceTraveled = 0f;
    }
}