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

    [Header("Indicador en el mundo")]
    [Tooltip("Hijo del objeto: un Canvas (World Space) con la Image del botón. Colócalo delante del objeto en el editor.")]
    public GameObject worldPrompt;

    [Tooltip("El indicador siempre mira a la cámara del jugador.")]
public bool promptFacesCamera = true;

    // Registro estático: InspectViewer recorre esta lista para saber qué hay cerca.
    public static readonly List<InspectableObject> All = new();

void Awake()
{
    EnsureInspectableLayer();
    if (worldPrompt != null) worldPrompt.SetActive(false);
}

public void SetPromptVisible(bool visible)
{
    if (worldPrompt != null && worldPrompt.activeSelf != visible)
        worldPrompt.SetActive(visible);
}

void LateUpdate()
{
    if (!promptFacesCamera || worldPrompt == null || !worldPrompt.activeSelf) return;

    Camera cam = Camera.main;
    if (cam == null) return;

    // El Canvas mira en la misma dirección que la cámara (no se ve volteado ni inclinado).
    worldPrompt.transform.rotation = cam.transform.rotation;
}

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureInspectableLayer();
    }
#endif

    void EnsureInspectableLayer()
    {
        int inspectLayer = LayerMask.NameToLayer("Inspectable");
        if (inspectLayer != -1 && gameObject.layer == 0)
        {
            gameObject.layer = inspectLayer;
        }
    }

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