using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private InputManagerBueno inputManager;
    private PlayerControls playerControls;
    private PlayerAnimation playerAnimation;

    private void Awake()
    {
        inputManager = GetComponent<InputManagerBueno>();
        playerControls = GetComponent<PlayerControls>();
        playerAnimation = GetComponent<PlayerAnimation>();
    }

    private void Update()
    {
        inputManager.HandleAllInputs();

        playerControls.HandleRotation();

        playerAnimation.HandleAnimations();
    }

    private void FixedUpdate()
    {
        playerControls.HandleMovement();
    }
}