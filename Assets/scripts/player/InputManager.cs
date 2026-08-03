using UnityEngine;

public class InputManager : MonoBehaviour
{
    private InputSystem_Actions playerInput;

    public Vector2 movementInput;

    public float horizontalInput;
    public float verticalInput;
    public float upDownInput;

    private void OnEnable()
    {
        if (playerInput == null)
        {
            playerInput = new InputSystem_Actions();

            playerInput.Player.Move.performed += ctx =>
                movementInput = ctx.ReadValue<Vector2>();

            playerInput.Player.Move.canceled += _ =>
                movementInput = Vector2.zero;
        }

        playerInput.Enable();
    }

    private void OnDisable()
    {
        playerInput.Disable();
    }

    public void HandleAllInputs()
    {
        horizontalInput = movementInput.x;
        verticalInput = movementInput.y;

        upDownInput = 0f;

        if (playerInput.Player.Jump.IsPressed())
            upDownInput += 1f;

        if (playerInput.Player.GoingDown.IsPressed())
            upDownInput -= 1f;
    }
}