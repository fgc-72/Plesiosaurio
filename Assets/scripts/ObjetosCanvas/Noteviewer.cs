using System.Collections.Generic;
using UnityEngine;

public class InteractableNote : MonoBehaviour
{
    [Header("Contenido")]
    [Tooltip("Fondo de la nota (carta escaneada o foto de papel). Debe importarse como Sprite (2D and UI).")]
    public Sprite background;

    [Tooltip("Texto que se dibuja encima. Déjalo vacío si la imagen ya trae todo.")]
    [TextArea(3, 10)] public string overlayText;

    [Header("Interacción")]
    [Min(0.1f)] public float range = 2.5f;

    // Registro estático: NoteViewer recorre esta lista para saber cuál nota está cerca.
    public static readonly List<InteractableNote> All = new();

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Para ver el rango en la Scene view al seleccionar el objeto.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}