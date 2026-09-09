using UnityEngine;

public class InputManagerBueno : MonoBehaviour
{
    private InputSystem_Actions playerInput;

    public Vector2 movementInput;
    public float horizontalInput;
    public float verticalInput;
    public float upDownInput;

    public bool accelerateInput;
    public bool detectiveInput;
    private bool headbuttInput;

    private void Awake()
    {
        playerInput = new InputSystem_Actions();

        playerInput.Player.Move.performed += ctx =>
            movementInput = ctx.ReadValue<Vector2>();

        playerInput.Player.Move.canceled += _ =>
            movementInput = Vector2.zero;

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

        // Detective: LT + RT
        detectiveInput = playerInput.Player.LT.IsPressed() &&
                          playerInput.Player.RT.IsPressed();
    }

    public bool ConsumeHeadbuttInput()
    {
        if (!headbuttInput)
            return false;

        headbuttInput = false;
        return true;
    }
}