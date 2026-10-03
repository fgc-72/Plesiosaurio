using System.Collections;
using UnityEngine;

/// <summary>
/// Componente disparador de pensamientos. Se puede colocar en cualquier GameObject con Collider (modo Trigger)
/// para activar un diálogo al entrar Nami, o llamarse desde eventos de Unity (UnityEvent) con TriggerDialogue().
/// </summary>
[RequireComponent(typeof(Collider))]
public class ThoughtTrigger : MonoBehaviour
{
    [Header("Configuración del Diálogo")]
    [Tooltip("ID exacto del diálogo en DialogueDatabase (ej: 'perdida_mara', 'escucha', 'nami_atisbo', 'nami_senal').")]
    [SerializeField] private string dialogueId;

    [Tooltip("Retraso opcional en segundos antes de que aparezca el pensamiento tras cruzar el trigger.")]
    [SerializeField] private float delay = 0f;

    [Tooltip("Si es true, el diálogo solo se reproducirá una única vez.")]
    [SerializeField] private bool triggerOnce = true;

    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce)
            return;

        if (!other.CompareTag(playerTag))
            return;

        TriggerDialogue();
    }

    /// <summary>
    /// Se puede llamar también manualmente desde botones, cinemáticas o eventos UnityEvent (como onBroken de rocas).
    /// </summary>
    public void TriggerDialogue()
    {
        if (hasTriggered && triggerOnce)
            return;

        hasTriggered = true;

        if (delay > 0f)
            StartCoroutine(TriggerWithDelayRoutine());
        else
            SendDialogue();
    }

    private IEnumerator TriggerWithDelayRoutine()
    {
        yield return new WaitForSeconds(delay);
        SendDialogue();
    }

    private void SendDialogue()
    {
        if (ThoughtUI.Instance != null)
        {
            ThoughtUI.Instance.ShowById(dialogueId);
        }
        else
        {
            Debug.LogWarning($"[ThoughtTrigger] No se encontró ThoughtUI.Instance en la escena para reproducir '{dialogueId}'.", this);
        }
    }
}
