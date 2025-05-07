using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;


[BurstCompile]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    [SerializeField] Transform camTransform;
    [SerializeField] private float mouseSensitivity = 100f;

    private Rigidbody rb;
    private float xRotation = 0f;
    private float yRotation = 0f;


    [BurstCompile]
    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        Cursor.lockState = CursorLockMode.Locked;

        UpdateScheduler.Register(OnUpdate);

        //FillListWithhrtf_IRData();
    }


    //public int sampleCount = 128;
    //public List<float> irSampleList = new List<float>();
    //public List<float3> positionlisr = new List<float3>();

    //private void FillListWithhrtf_IRData()
    //{
    //    irSampleList.Clear();  // Just to be safe
    //    positionlisr.Clear();  // Just to be safe

    //    int count = math.min(math.min(sampleCount, HRTFLoader.hrtf_IRData.Length), HRTFLoader.hrtf_Positions.Length);  // Clamp to prevent out of range

    //    for (int i = 0; i < count; i++)
    //    {
    //        irSampleList.Add(HRTFLoader.hrtf_IRData[i]);
    //        positionlisr.Add(HRTFLoader.hrtf_Positions[i]);
    //    }

    //    Debug.Log($"Filled list with {irSampleList.Count} IR values.");
    //}

    [BurstCompile]
    private void OnUpdate()
    {
        Move();

        LookAround();
    }

    [BurstCompile]
    private void Move()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = new Vector3(moveX, 0f, moveZ).normalized;
        rb.velocity = transform.TransformDirection(moveDir) * moveSpeed + new Vector3(0f, rb.velocity.y, 0f);
    }

    [BurstCompile]
    private void LookAround()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        yRotation += mouseX;

        transform.localRotation = Quaternion.Euler(0, yRotation, 0f);
        camTransform.localRotation = Quaternion.Euler(xRotation, 0, 0f);
    }


    [BurstCompile]
    private void OnDestroy()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }
}
