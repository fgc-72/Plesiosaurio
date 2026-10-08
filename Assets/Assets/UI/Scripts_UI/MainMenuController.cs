using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenuController : MonoBehaviour
{
    [Header("Botones en orden de arriba a abajo")]
    [SerializeField] private Button[] buttons;

    private GameObject lastSelected;

    private void Start()
    {
        SetupWrapAroundNavigation();
        Select(buttons[0].gameObject);
    }

    private void OnEnable()
    {
        // Si el menú se reactiva (por ejemplo al volver de Settings), vuelve a seleccionar
        if (buttons != null && buttons.Length > 0)
            Select(lastSelected != null ? lastSelected : buttons[0].gameObject);
    }

    private void Update()
    {
        if (!buttons[0].gameObject.activeInHierarchy) return;
        var es = EventSystem.current;
        if (es == null) return;

        // Si el jugador hace clic con el mouse en el vacío, la selección se pierde: la recuperamos
        if (es.currentSelectedGameObject == null)
            es.SetSelectedGameObject(lastSelected != null ? lastSelected : buttons[0].gameObject);
        else
            lastSelected = es.currentSelectedGameObject;
    }

    private void Select(GameObject go)
    {
        EventSystem.current.SetSelectedGameObject(null); // limpia primero para que se refresque el resaltado
        EventSystem.current.SetSelectedGameObject(go);
        lastSelected = go;
    }

    // Hace que al pasar del último botón se vuelva al primero y viceversa
    private void SetupWrapAroundNavigation()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp   = buttons[(i - 1 + buttons.Length) % buttons.Length];
            nav.selectOnDown = buttons[(i + 1) % buttons.Length];
            buttons[i].navigation = nav;
        }
    }
    public void FocusMenu()
{
    Select(lastSelected != null && lastSelected.activeInHierarchy
           ? lastSelected
           : buttons[0].gameObject);
}
}