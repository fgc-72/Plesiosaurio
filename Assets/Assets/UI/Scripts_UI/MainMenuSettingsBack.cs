using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuSettingsBack : MonoBehaviour
{
    [SerializeField] private GameObject panelSettings;
    [SerializeField] private GameObject panelPrincipal;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject firstSelected; // ButtonPlay, o el botón que quieras enfocar al volver

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(GoBack);
    }

    public void GoBack()
    {
        panelSettings.SetActive(false);
        panelPrincipal.SetActive(true);

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    private void Update()
{
    if (!panelSettings.activeInHierarchy) return;

    var kb = UnityEngine.InputSystem.Keyboard.current;
    var gp = UnityEngine.InputSystem.Gamepad.current;

    if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
        (gp != null && gp.buttonEast.wasPressedThisFrame))
        GoBack();
}
}