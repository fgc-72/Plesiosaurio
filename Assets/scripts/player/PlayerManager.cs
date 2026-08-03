using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private InputManager inputManager;
    private PlayerControls playerControls;

    private void Awake()
    {
        inputManager = GetComponent<InputManager>();
        playerControls = GetComponent<PlayerControls>();
    }

    private void Update()
    {
        inputManager.HandleAllInputs();

        playerControls.HandleRotation();
    }

    private void FixedUpdate()
    {
        playerControls.HandleMovement();
    }
}