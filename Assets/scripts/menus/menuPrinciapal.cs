using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class menuPrincipal : MonoBehaviour
{
    [Header("Botones (Opcionales - se autoconfiguran si se dejan vacíos)")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button playButton;

    private void Start()
    {
        InitButtons();
    }

    private void OnEnable()
    {
        UpdateButtonStates();
    }

    private void InitButtons()
    {
        // 1. Si playButton no está asignado, buscarlo por nombre estándar
        if (playButton == null)
        {
            GameObject playObj = GameObject.Find("ButtonPlay");
            if (playObj != null) playButton = playObj.GetComponent<Button>();
        }

        // 2. Si continueButton no está asignado, buscar si ya existe uno en la jerarquía
        if (continueButton == null)
        {
            GameObject contObj = GameObject.Find("ButtonContinue");
            if (contObj != null) continueButton = contObj.GetComponent<Button>();
        }

        // 3. Si hay partida guardada y no existe continueButton en la escena,
        // clonamos ButtonPlay dinámicamente con su mismo aspecto, fuente y animaciones.
        if (continueButton == null && playButton != null && SaveSystem.HasSaveFile())
        {
            CreateDynamicContinueButton();
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueGame);
            continueButton.onClick.AddListener(ContinueGame);
        }

        UpdateButtonStates();
    }

    private void CreateDynamicContinueButton()
    {
        GameObject clone = Instantiate(playButton.gameObject, playButton.transform.parent);
        clone.name = "ButtonContinue";
        clone.transform.SetSiblingIndex(playButton.transform.GetSiblingIndex());

        RectTransform rt = clone.GetComponent<RectTransform>();
        RectTransform playRt = playButton.GetComponent<RectTransform>();
        if (rt != null && playRt != null)
        {
            // Coloca el botón de continuar encima del botón de Jugar
            rt.anchoredPosition = new Vector2(playRt.anchoredPosition.x, playRt.anchoredPosition.y + 110f);
        }

        TMP_Text text = clone.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = "Continuar";
        }

        continueButton = clone.GetComponent<Button>();
    }

    public void UpdateButtonStates()
    {
        bool hasSave = SaveSystem.HasSaveFile();
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(hasSave);
        }
    }

    public void ContinueGame()
    {
        UISfx.PlaySelect();

        if (SaveSystem.HasSaveFile())
        {
            SaveSystem.LoadGame();
        }
        else
        {
            PlayGame();
        }
    }

    public void PlayGame()
    {
        // Nueva partida: limpia el guardado anterior para empezar desde cero
        SaveSystem.DeleteSave();

        UISfx.PlaySelect();

        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadScene("SampleScene");
        else
            SceneManager.LoadScene("SampleScene"); // respaldo si el SceneFader no está en la escena
    }

    public void QuitGame()
    {
        UISfx.PlayBack();
        Application.Quit();
    }
}
