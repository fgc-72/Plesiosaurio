using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ponlo en el objeto raíz del panel de ajustes (el que se activa/desactiva).
/// Versión pasiva: solo actúa cuando NO hay nada seleccionado.
/// - Al abrirse, si nadie seleccionó nada, selecciona el primer elemento.
/// - Si la selección se pierde (clic en el vacío), la devuelve al último elemento.
/// No toca la navegación ni el resaltado, y respeta la selección hecha por otros
/// scripts (por ejemplo MenuButtonHighlight al pasar el mouse).
/// </summary>
public class SettingsPanelSelection : MonoBehaviour
{
    [Header("Elementos en orden de arriba a abajo (el primero es el inicial)")]
    [SerializeField] private Selectable[] items;

    [Header("Opcional")]
    [Tooltip("Si está activo, una selección fuera de este panel también se considera perdida.")]
    [SerializeField] private bool restrictToPanel = false;

    [Tooltip("Configura Up/Down entre los elementos. Déjalo apagado si otro script ya maneja la navegación.")]
    [SerializeField] private bool setupNavigation = false;

    [Tooltip("Selectables que nunca deben quedarse seleccionados (por ejemplo el Scrollbar).")]
    [SerializeField] private Selectable[] ignoredSelectables;

    private GameObject lastSelected;

    private bool IsIgnored(GameObject go)
    {
        if (ignoredSelectables == null) return false;
        foreach (var s in ignoredSelectables)
            if (s != null && s.gameObject == go) return true;
        return false;
    }

    private void Awake()
    {
        if (setupNavigation) SetupVertical();
    }

    private void OnEnable()
    {
        lastSelected = null;
    }

    private void Update()
    {
        var es = EventSystem.current;
        if (es == null || items == null || items.Length == 0) return;

        GameObject sel = es.currentSelectedGameObject;

        bool lost = sel == null
                    || !sel.activeInHierarchy
                    || IsIgnored(sel)
                    || (restrictToPanel && !sel.transform.IsChildOf(transform));

        if (lost)
        {
            GameObject target = (lastSelected != null && lastSelected.activeInHierarchy)
                ? lastSelected
                : items[0].gameObject;
            Select(target);
        }
        else
        {
            lastSelected = sel; // solo registramos, no tocamos nada
        }
    }

    private void Select(GameObject go)
    {
        if (go == null) return;
        var es = EventSystem.current;
        if (es.currentSelectedGameObject == go) return; // no reseleccionar lo ya seleccionado

        es.SetSelectedGameObject(null);
        es.SetSelectedGameObject(go);
        lastSelected = go;
    }

    private void SetupVertical()
    {
        int n = items.Length;
        for (int i = 0; i < n; i++)
        {
            if (items[i] == null) continue;

            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = items[(i - 1 + n) % n],
                selectOnDown = items[(i + 1) % n]
                // Left/Right en null: el Slider los usa para cambiar su valor.
            };
            items[i].navigation = nav;
        }
    }
}