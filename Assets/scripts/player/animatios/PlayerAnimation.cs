using UnityEngine;

[RequireComponent(typeof(PlayerControls))]
public class PlayerAnimation : MonoBehaviour
{
    // Por debajo de este ángulo (en grados) consideramos que el cuerpo ya está "nivelado"
    // y puede volver a idle/nado normal. Un pequeño margen evita parpadeos cerca de 0°.
    [SerializeField] private float pitchIdleThreshold = 3f;

    private Animator animator;
    private InputManagerBueno inputManager;
    private PlayerControls playerControls;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        inputManager = GetComponent<InputManagerBueno>();
        playerControls = GetComponent<PlayerControls>();
    }

    public void HandleAnimations()
    {
        bool isMovingHorizontal =
            Mathf.Abs(inputManager.horizontalInput) > 0.1f ||
            Mathf.Abs(inputManager.verticalInput) > 0.1f;

        // Usamos el pitch REAL ya aplicado al cuerpo (no el input crudo).
        // Así, mientras el plesiosaurio siga físicamente inclinado, la animación
        // se mantiene en "subiendo/bajando" — y solo pasa a idle cuando el cuerpo
        // de verdad terminó de nivelarse. Elimina el desfase entre pose y rotación.
        float currentPitch = playerControls.GetCurrentPitch();

        bool isGoingUp = currentPitch < -pitchIdleThreshold;
        bool isGoingDown = currentPitch > pitchIdleThreshold;

        int animationState;

        // PRIORIDAD
        if (isGoingUp)
        {
            animationState = 2;
        }
        else if (isGoingDown)
        {
            animationState = 3;
        }
        else if (isMovingHorizontal)
        {
            animationState = 1;
        }
        else
        {
            animationState = 0;
        }

        animator.SetInteger("AnimationState", animationState);
    }
}