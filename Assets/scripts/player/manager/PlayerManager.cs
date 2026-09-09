using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private InputManagerBueno inputManager;
    private PlayerControls playerControls;
    private PlayerAnimation playerAnimation;
    private HeadbuttController headbuttController;

    private void Awake()
    {
        inputManager = GetComponent<InputManagerBueno>();
        playerControls = GetComponent<PlayerControls>();
        playerAnimation = GetComponent<PlayerAnimation>();
        headbuttController = GetComponent<HeadbuttController>();
    }

    private void Update()
    {
        // Leer los inputs.
        inputManager.HandleAllInputs();

        // Cabezazo (input de un solo disparo + cooldown).
        headbuttController.HandleHeadbutt();

        // Actualizar las animaciones.
        playerAnimation.HandleAnimations();
    }

    private void FixedUpdate()
    {
        // Movimiento físico.
        playerControls.HandleMovement();
    }
}