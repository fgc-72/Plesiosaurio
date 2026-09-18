using UnityEngine;
using UnityEngine.InputSystem;

public class InputManagerBueno : MonoBehaviour
{
    private InputSystem_Actions playerInput;

    public Vector2 movementInput;
    public float horizontalInput;
    public float verticalInput;
    public float upDownInput;

    public bool accelerateInput;
    public bool detectiveInput;

    // Headbutt es una acción de "un solo disparo" (no se mantiene presionada como Sprint),
    // así que se maneja distinto: se activa por evento y se "consume" una sola vez.
    private bool headbuttInput;

    private void Awake()
    {
        playerInput = new InputSystem_Actions();

        playerInput.Player.Move.performed += ctx =>
            movementInput = ctx.ReadValue<Vector2>();

        playerInput.Player.Move.canceled += _ =>
            movementInput = Vector2.zero;

        // IMPORTANTE: esto requiere que exista una acción llamada "Headbutt" dentro
        // del Action Map "Player" en tu asset de Input Actions (mismo lugar donde
        // están Move, Sprint, Jump, etc.). Bindéala al botón que quieras (ej. X/Cuadrado).
        playerInput.Player.Headbutt.performed += _ => headbuttInput = true;
    }

    private void OnEnable()
    {
        playerInput.Enable();
    }

    private void OnDisable()
    {
        playerInput.Disable();
    }

    public void HandleAllInputs()
    {
        // Movimiento
        horizontalInput = movementInput.x;
        verticalInput = movementInput.y;

        // Movimiento vertical
        upDownInput = 0f;

        if (playerInput.Player.Jump.IsPressed())
            upDownInput += 1f;

        if (playerInput.Player.GoingDown.IsPressed())
            upDownInput -= 1f;

        // Aceleración
        accelerateInput = playerInput.Player.Sprint.IsPressed();

        // Modo escucha: en gamepad es un combo (L1+R1), en PC es UNA sola tecla.
        // Cualquiera de los dos métodos activa el mismo booleano — DetectiveController
        // no tiene que saber ni le importa cuál se usó.
        bool gamepadCombo = playerInput.Player.LT.IsPressed() &&
                            playerInput.Player.RT.IsPressed();

        bool pcKey = Keyboard.current != null &&
                     Keyboard.current.qKey.isPressed;

        detectiveInput = gamepadCombo || pcKey;
    }

    // HeadbuttController debe llamar esto para "leer y apagar" el input.
    // Evita que un solo click dispare el cabezazo varias veces seguidas
    // mientras el flag siga en true entre frames.
    public bool ConsumeHeadbuttInput()
    {
        if (!headbuttInput)
            return false;

        headbuttInput = false;
        return true;
    }
}