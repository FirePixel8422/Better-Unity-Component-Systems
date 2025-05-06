using Unity.Burst;
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
    }

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
