using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerControls : MonoBehaviour
{
    private InputManagerBueno inputManager;
    private Rigidbody rb;
    [SerializeField] private Transform cameraObject; 

    [Header("Normal Movement")]
    [SerializeField] private float movementSpeed = 6f;

    [Header("Vertical Movement")]
    [SerializeField] private float verticalSpeedMultiplier = 0.5f;
    [Tooltip("Tiempo aproximado (segundos) que tarda en frenar/acelerar en el eje vertical. Más alto = más suave/pesado.")]
    [SerializeField] private float verticalSmoothTime = 0.35f;

    [Header("Aceleración / Sprint")]
    [SerializeField] private float accelerationSpeed = 12f;
    [Tooltip("Tiempo aproximado (segundos) que tarda la velocidad horizontal en alcanzar el objetivo. Más bajo = más responsivo.")]
    [SerializeField] private float speedSmoothTime = 0.25f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float staminaDrain = 1f;
    [SerializeField] private float staminaRecovery = 0.75f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 6f;
    [Tooltip("Inclinación máxima (en grados) al subir o bajar. Evita que el cuerpo apunte casi vertical.")]
    [SerializeField] private float maxTiltAngle = 35f;
    [Tooltip("Tiempo (segundos) que tarda el pitch en llegar a su ángulo objetivo. Esta MISMA variable es la que lee la animación, así nunca se desincronizan.")]
    [SerializeField] private float pitchSmoothTime = 0.25f;

    // Hacia dónde está "mirando" horizontalmente en este momento. Se actualiza solo cuando
    // hay input horizontal real, así al detenerse (o al subir/bajar sin moverse a los lados)
    // el cuerpo mantiene su orientación en vez de quedar apuntando a donde sea que estaba yendo.
    private Vector3 facingDirection;
    private Quaternion smoothYaw;

    // Ángulo de pitch REALMENTE aplicado al cuerpo en este frame (ya suavizado).
    // La animación debe leer esta variable (GetCurrentPitch), nunca el input crudo,
    // para garantizar que la pose coincida siempre con la rotación visible.
    private float currentPitch;
    private float pitchVelocity;

    // --- Estado interno ---
    private float currentStamina;
    private float currentSpeed;
    private bool isExhausted; // true mientras el jugador está "sin aliento" tras gastar toda la stamina

    // Velocidades de referencia que usa SmoothDamp internamente (no tocar desde fuera)
    private float speedSmoothVelocity;
    private float verticalSmoothVelocity;
    private float currentVerticalSpeed;

    private void Awake()
    {
        inputManager = GetComponent<InputManagerBueno>();
        rb = GetComponent<Rigidbody>();

        if (cameraObject == null && Camera.main != null)
            cameraObject = Camera.main.transform;

        currentStamina = maxStamina;
        currentSpeed = movementSpeed;
        facingDirection = transform.forward;
        smoothYaw = Quaternion.LookRotation(facingDirection, Vector3.up);
    }

    public void HandleMovement()
    {
        float deltaTime = Time.fixedDeltaTime;

        bool isMoving =
            Mathf.Abs(inputManager.horizontalInput) > 0.1f ||
            Mathf.Abs(inputManager.verticalInput) > 0.1f ||
            Mathf.Abs(inputManager.upDownInput) > 0.1f;

        // --- Lógica de "agotado" para evitar el parpadeo del sprint al llegar a 0 stamina ---
        if (currentStamina <= 0f)
            isExhausted = true;

        if (!inputManager.accelerateInput)
            isExhausted = false;

        bool canAccelerate =
            inputManager.accelerateInput &&
            isMoving &&
            !isExhausted;

        UpdateStamina(canAccelerate, deltaTime);
        UpdateSpeed(canAccelerate, deltaTime);

        // --- Dirección relativa a la cámara (solo plano horizontal) ---
        Vector3 cameraForward = cameraObject.forward;
        Vector3 cameraRight = cameraObject.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 horizontalDirection =
            cameraForward * inputManager.verticalInput +
            cameraRight * inputManager.horizontalInput;

        if (horizontalDirection.sqrMagnitude > 1f)
            horizontalDirection.Normalize();

        // --- Movimiento vertical suavizado (evita que suba/baje brusco) ---
        float targetVerticalSpeed = inputManager.upDownInput * currentSpeed * verticalSpeedMultiplier;
        currentVerticalSpeed = Mathf.SmoothDamp(
            currentVerticalSpeed,
            targetVerticalSpeed,
            ref verticalSmoothVelocity,
            verticalSmoothTime
        );

        // Vector de movimiento final en 3D (horizontal + vertical suavizado).
        // Se usa TANTO para la velocidad como para la rotación: una sola fuente de verdad,
        // así el cuerpo siempre apunta exactamente hacia donde se está moviendo.
        Vector3 targetVelocity = horizontalDirection * currentSpeed + Vector3.up * currentVerticalSpeed;

        // Suavizado independiente por eje usando SmoothDamp da una sensación más "elástica" que MoveTowards
        rb.linearVelocity = Vector3.SmoothDamp(
            rb.linearVelocity,
            targetVelocity,
            ref velocitySmoothRef,
            speedSmoothTime
        );

        // La rotación se maneja aparte (yaw + pitch independientes, ver HandleRotation).
        HandleRotation(horizontalDirection);
    }

    // SmoothDamp para Vector3 necesita un Vector3 de referencia, no un float
    private Vector3 velocitySmoothRef;

    private void UpdateStamina(bool canAccelerate, float deltaTime)
    {
        currentStamina += (canAccelerate ? -staminaDrain : staminaRecovery) * deltaTime;
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }

    private void UpdateSpeed(bool canAccelerate, float deltaTime)
    {
        float targetSpeed = canAccelerate ? accelerationSpeed : movementSpeed;

        currentSpeed = Mathf.SmoothDamp(
            currentSpeed,
            targetSpeed,
            ref speedSmoothVelocity,
            speedSmoothTime
        );
    }

    private void HandleRotation(Vector3 horizontalDirection)
    {
        // Yaw: solo actualizamos hacia dónde "mira" el cuerpo si hay input horizontal real.
        // Si el jugador solo sube/baja (o no hace nada), se mantiene mirando hacia donde iba.
        if (horizontalDirection.sqrMagnitude > 0.01f)
            facingDirection = horizontalDirection.normalized;

        Quaternion yawTarget = Quaternion.LookRotation(facingDirection, Vector3.up);
        smoothYaw = Quaternion.Slerp(smoothYaw, yawTarget, rotationSpeed * Time.fixedDeltaTime);

        // Pitch: se suaviza aparte con SmoothDampAngle (no dentro del Slerp de arriba).
        // currentPitch queda guardado como el ángulo REAL que se está aplicando este frame,
        // así PlayerAnimation puede leerlo directamente y nunca queda desincronizado.
        float targetPitch = -inputManager.upDownInput * maxTiltAngle;
        currentPitch = Mathf.SmoothDampAngle(
            currentPitch,
            targetPitch,
            ref pitchVelocity,
            pitchSmoothTime
        );

        Quaternion pitchRotation = Quaternion.AngleAxis(currentPitch, Vector3.right);

        rb.MoveRotation(smoothYaw * pitchRotation);
    }

    // La animación debe usar ESTE valor (no el input crudo) para decidir si sigue en
    // pose de "subiendo/bajando" o ya puede volver a idle. Como es el mismo ángulo que
    // se está aplicando físicamente, la animación y la rotación quedan siempre sincronizadas.
    public float GetCurrentPitch() => currentPitch;

    // --- API pública para UI de stamina, etc. ---
    public float GetCurrentStamina() => currentStamina;
    public float GetMaxStamina() => maxStamina;
    public float GetStaminaNormalized() => currentStamina / maxStamina;
}