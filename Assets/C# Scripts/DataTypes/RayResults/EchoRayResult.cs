using UnityEngine;



[Tooltip("LOS checks to the player")]
public struct EchoRayResult
{
    public float distanceTraveled;


    public static EchoRayResult Default()
    {
        return new EchoRayResult
        {
            distanceTraveled = 0
        };
    }
}