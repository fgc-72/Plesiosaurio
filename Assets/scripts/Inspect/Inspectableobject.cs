using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Se pone en cada objeto del mundo que se pueda inspeccionar.
/// Solo guarda datos (qué mostrar y desde qué rango); InspectViewer hace el resto.
/// </summary>
public class InspectableObject : MonoBehaviour
{
    [Header("Contenido")]
    [Tooltip("Prefab 'limpio' que se muestra en el visor (solo modelo, sin scripts ni colliders).")]
    public GameObject inspectPrefab;

    [Tooltip("Rotación inicial con la que aparece en el visor.")]
    public Vector3 startRotation = Vector3.zero;

    [Tooltip("Activo: el visor mide el modelo y lo ajusta al tamaño de pantalla. Apagado: se usa solo 'Inspect Scale' tal cual.")]
    public bool autoFit = true;

    [Tooltip("Con Auto Fit: multiplicador (1 = tamaño normal, 1.5 = 50% más grande). Sin Auto Fit: escala directa del modelo.")]
    [Min(0.001f)] public float inspectScale = 1f;

    [Header("Interacción")]
    [Min(0.1f)] public float range = 2.5f;

    // Registro estático: InspectViewer recorre esta lista para saber qué hay cerca.
    public static readonly List<InspectableObject> All = new();

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Muestra el rango en la Scene view siempre (en tenue) y en amarillo al seleccionarlo.
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, range);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}