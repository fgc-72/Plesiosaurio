using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    
    InputManager inputManager;
    PlayerControls playerControls;

    void Awake()
    {
        inputManager = GetComponent<InputManager>();
        playerControls = GetComponent<PlayerControls>();
    }

    void Update()
    {
        inputManager.HandleAllInputs();
    }

    void FixedUpdate()
    {
       playerControls.HandleAllMovement();
    }
}
