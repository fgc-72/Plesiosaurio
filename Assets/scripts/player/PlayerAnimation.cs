using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private InputManagerBueno inputManager;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        inputManager = GetComponent<InputManagerBueno>();
    }

    public void HandleAnimations()
    {
        bool isMoving =
            Mathf.Abs(inputManager.horizontalInput) > 0.1f ||
            Mathf.Abs(inputManager.verticalInput) > 0.1f;

        bool isGoingUp = inputManager.upDownInput > 0.1f;
        bool isGoingDown = inputManager.upDownInput < -0.1f;

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
        else if (isMoving)
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