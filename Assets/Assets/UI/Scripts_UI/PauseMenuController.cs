using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PauseMenuController : MonoBehaviour
{
    private enum Pending { None, MainMenu, QuitGame }

    [Header("Input")]
    [SerializeField] private InputActionReference pauseAction;

    [Header("Botón de pausa del HUD (opcional)")]
    [SerializeField] private Button pauseHudButton;

    [Header("Paneles")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject optionsGroup;
    [SerializeField] private GameObject confirmGroup;
    [SerializeField] private TMP_Text confirmText;

    [Header("Opciones (de arriba a abajo)")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Confirmación")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Textos")]
    [SerializeField] private string mainMenuQuestion = "¿Deseas salir a la pantalla de inicio?";
    [SerializeField] private string quitQuestion     = "¿Deseas salir del juego?";

    [Header("Escena")]
    [SerializeField] private string mainMenuScene = "MainMenu"; // debe estar en Build Profiles → Scene List

    public bool IsPaused { get; private set; }

    private Pending pending = Pending.None;
    private GameObject lastSelected;
    private bool prevCursorVisible;
    private CursorLockMode prevLockState;

    private void Awake()
    {
        if (pauseHudButton != null) pauseHudButton.onClick.AddListener(Pause);
        resumeButton.onClick.AddListener(Resume);
        settingsButton.onClick.AddListener(OnSettings);
        saveButton.onClick.AddListener(OnSave);
        mainMenuButton.onClick.AddListener(() => ShowConfirm(Pending.MainMenu));
        quitButton.onClick.AddListener(() => ShowConfirm(Pending.QuitGame));
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(CancelConfirm);

        SetupVertical(new[] { resumeButton, settingsButton, saveButton, mainMenuButton, quitButton });
        SetupHorizontal(confirmButton, cancelButton);

        confirmGroup.SetActive(false);
        pausePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (pauseAction == null) return;
        pauseAction.action.performed += OnPausePerformed;
        pauseAction.action.Enable();
    }

    private void OnDisable()
    {
        if (pauseAction == null) return;
        pauseAction.action.performed -= OnPausePerformed;
    }

    private void OnDestroy()
    {
        if (IsPaused) Time.timeScale = 1f; // por si la escena se destruye estando en pausa
    }

    private void Update()
    {
        if (!IsPaused) return;

        var es = EventSystem.current;
        GameObject sel = es.currentSelectedGameObject;

        // Si se pierde la selección (clic en el vacío, grupo oculto), la recuperamos
        if (sel == null || !sel.activeInHierarchy)
            Select(lastSelected != null && lastSelected.activeInHierarchy
                   ? lastSelected
                   : DefaultSelection());
        else
            lastSelected = sel;
    }

    // ---------- Input ----------

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        if (!IsPaused)               Pause();
        else if (pending != Pending.None) CancelConfirm(); // atrás desde la confirmación
        else                         Resume();
    }

    // ---------- Pausa / Reanudar ----------

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        prevCursorVisible = Cursor.visible;
        prevLockState = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Time.timeScale = 0f;
        pausePanel.SetActive(true);
        ShowOptions(resumeButton);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        pending = Pending.None;

        pausePanel.SetActive(false);
        Time.timeScale = 1f;

        Cursor.visible = prevCursorVisible;
        Cursor.lockState = prevLockState;

        EventSystem.current.SetSelectedGameObject(null);
    }

    // ---------- Opciones ----------

    private void ShowOptions(Button focus)
    {
        pending = Pending.None;
        optionsGroup.SetActive(true);
        confirmGroup.SetActive(false);
        Select(focus.gameObject);
    }

    private void OnSettings() { Debug.Log("Ajustes: pendiente de implementar"); }
    private void OnSave()     { Debug.Log("Guardar: pendiente de implementar"); }

    // ---------- Confirmación ----------

    private void ShowConfirm(Pending action)
    {
        pending = action;
        confirmText.text = action == Pending.MainMenu ? mainMenuQuestion : quitQuestion;

        optionsGroup.SetActive(false);
        confirmGroup.SetActive(true);
        Select(cancelButton.gameObject); // empieza en Cancelar por seguridad
    }

    private void CancelConfirm()
    {
        Button back = pending == Pending.QuitGame ? quitButton : mainMenuButton;
        ShowOptions(back);
    }

    private void Confirm()
    {
        switch (pending)
        {
            case Pending.MainMenu:
                Time.timeScale = 1f;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                SceneManager.LoadScene(mainMenuScene);
                break;

            case Pending.QuitGame:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    // ---------- Selección y navegación ----------

    private GameObject DefaultSelection() =>
        confirmGroup.activeInHierarchy ? cancelButton.gameObject : resumeButton.gameObject;

    private void Select(GameObject go)
    {
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(go);
        lastSelected = go;
    }

    private static void SetupVertical(Button[] list)
    {
        for (int i = 0; i < list.Length; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp   = list[(i - 1 + list.Length) % list.Length];
            nav.selectOnDown = list[(i + 1) % list.Length];
            list[i].navigation = nav;
        }
    }

    private static void SetupHorizontal(Button a, Button b)
    {
        a.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = b, selectOnRight = b };
        b.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = a, selectOnRight = a };
    }
}