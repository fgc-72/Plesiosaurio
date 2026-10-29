using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MainMenuSettingsBack : MonoBehaviour
{
    [SerializeField] private GameObject panelSettings;
    [SerializeField] private GameObject panelPrincipal;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject firstSelected;

    private int openedFrame = -1;
private void OnEnable()
{
    Debug.LogWarning("MainMenuSettingsBack ACTIVADO", this);
}

private void OnDisable()
{
    Debug.LogWarning("MainMenuSettingsBack DESACTIVADO", this);
}
    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(GoBack);
    }

    public void GoBack()
    {
        Debug.Log("GoBack llamado", this);
        panelSettings.SetActive(false);
        panelPrincipal.SetActive(true);

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    private void Update()
    {
        if (Time.frameCount % 120 == 0) Debug.Log("Update corre", this);

        if (!panelSettings.activeInHierarchy)
        {
            openedFrame = -1;
            return;
        }
        if (backButton != null && !backButton.gameObject.activeSelf)
            backButton.gameObject.SetActive(true);
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