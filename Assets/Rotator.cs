using System.Collections;
using Unity.Mathematics;
using UnityEngine;



public class Rotator : MonoBehaviour
{
    [SerializeField] private float rotSpeedMin, rotspeedMax;

    [SerializeField] private float interval;
    private float elapsed;


    private void Update()
    {
        elapsed += Time.deltaTime;

        if (elapsed >= interval)
        {
            elapsed = 0;
        }
        else
        {
            return;
        }

        float speedUp = UnityEngine.Random.Range(rotSpeedMin, rotspeedMax);
        float speedRight = UnityEngine.Random.Range(rotSpeedMin, rotspeedMax);
        float speedForward = UnityEngine.Random.Range(rotSpeedMin, rotspeedMax);

        transform.Rotate(transform.up, speedUp, Space.World);
        transform.Rotate(transform.right, speedRight, Space.World);
        transform.Rotate(transform.forward, speedForward, Space.World);

        transform.GetChild(0).transform.Rotate(transform.up, -speedUp, Space.World);
        transform.GetChild(0).transform.Rotate(transform.right, -speedRight, Space.World);
        transform.GetChild(0).transform.Rotate(transform.forward, -speedForward, Space.World);
    }
}
