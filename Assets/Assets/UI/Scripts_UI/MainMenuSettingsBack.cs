using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MainMenuSettingsBack : MonoBehaviour
{
    [SerializeField] private GameObject panelSettings;
    [SerializeField] private GameObject panelPrincipal;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject firstSelected; // ButtonPlay, o el botón que quieras enfocar al volver

    private int openedFrame = -1;

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
        if (!panelSettings.activeInHierarchy)
        {
            openedFrame = -1;
            return;
        }

        // Ignora el input del frame en que se abrió el panel
        if (openedFrame < 0) openedFrame = Time.frameCount;
        if (Time.frameCount <= openedFrame + 1) return;

        var kb = Keyboard.current;
        var gp = Gamepad.current;

        bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame) ||
                      (gp != null && gp.buttonEast.wasPressedThisFrame);

        if (cancel)
        {
            UISfx.PlayBack();
            GoBack();
        }
    }
}