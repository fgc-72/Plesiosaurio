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

    // Mientras esto es true, HandleAnimations no hace nada: deja que ScriptedEvent
    // decida qué animación se reproduce (ej. una de "arrastrado por la corriente").
    private bool animationOverride;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        inputManager = GetComponent<InputManagerBueno>();
        playerControls = GetComponent<PlayerControls>();
    }

    // Llamado por ScriptedEvent al iniciar/terminar una cinemática.
    public void SetAnimationOverride(bool isOverridden)
    {
        animationOverride = isOverridden;
    }

    public void HandleAnimations()
    {
        if (animationOverride)
            return;

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