using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerControls : MonoBehaviour
{
    private InputManagerBueno inputManager;

    private Rigidbody rb;

    private Transform cameraObject;

    [Header("Movement")]
    public float movementSpeed = 6f;
    public float acceleration = 8f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    private void Awake()
    {
        inputManager = GetComponent<InputManagerBueno>();
        rb = GetComponent<Rigidbody>();

        cameraObject = Camera.main.transform;
    }

    public void HandleMovement()
    {
        Vector3 cameraForward = cameraObject.forward;
        Vector3 cameraRight = cameraObject.right;

        // Ignora la inclinación de la cámara para que W siempre avance
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection =
            cameraForward * inputManager.verticalInput +
            cameraRight * inputManager.horizontalInput +
            Vector3.up * inputManager.upDownInput;

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        Vector3 targetVelocity = moveDirection * movementSpeed;

        rb.linearVelocity = Vector3.Lerp(
            rb.linearVelocity,
            targetVelocity,
            acceleration * Time.fixedDeltaTime);
    }

    public void HandleRotation()
    {
        Vector3 cameraForward = cameraObject.forward;
        Vector3 cameraRight = cameraObject.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 targetDirection =
            cameraForward * inputManager.verticalInput +
            cameraRight * inputManager.horizontalInput;

        if (targetDirection.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }
}