using UnityEngine;

public class InputManager : MonoBehaviour
{
    InputSystem_Actions playerInput;

    public Vector2 movementInput;
    public float verticalInput;
    public float horizontalInput;
    
    private void OnEnable()
    {
        if (playerInput == null)
        {
            playerInput = new InputSystem_Actions();
            playerInput.Player.Move.performed += i => movementInput = i.ReadValue<Vector2>();
            playerInput.Player.Move.canceled += _ => movementInput = Vector2.zero; 
        }

        playerInput.Enable();
    }

    private void OnDisable()
    {
        playerInput.Disable();
    }

    public void HandleAllInputs()
    {
        HandleMovementInput();
    }


    private void HandleMovementInput()
    {
        verticalInput = movementInput.y;
        horizontalInput = movementInput.x;
    }
}
