using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PauseSettingsTransition : MonoBehaviour
{
    [Serializable]
    private struct RectPose
    {
        public Vector2 position;
        public Vector3 scale;

        public static RectPose Of(RectTransform r) =>
            new RectPose { position = r.anchoredPosition, scale = r.localScale };

        public void Apply(RectTransform r)
        {
            r.anchoredPosition = position;
            r.localScale = scale;
        }

        public static void Blend(RectTransform r, RectPose a, RectPose b, float t)
        {
            r.anchoredPosition = Vector2.LerpUnclamped(a.position, b.position, t);
            r.localScale = Vector3.LerpUnclamped(a.scale, b.scale, t);
        }
    }

    [Header("Elementos que se animan")]
    [SerializeField] private RectTransform semicircle;
    [SerializeField] private RectTransform settingsButton;   // el padre, el que tiene el componente Button
    [SerializeField] private RectTransform settingsLabel;

    [Header("Se ocultan al abrir (Reanudar, Guardar, Menú, Salir)")]
    [SerializeField] private GameObject[] hideWhileOpen;

    [Header("Panel de ajustes")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private Selectable firstSelectable;     // primer slider

    [Header("Tiempos")]
    [SerializeField] private float zoomDuration = 0.5f;
    [SerializeField] private float fadeDuration = 0.25f;

    [Header("Poses (usa los menús contextuales del componente)")]
    [SerializeField] private RectPose semicircleNormal, semicircleZoom;
    [SerializeField] private RectPose buttonNormal,     buttonZoom;
    [SerializeField] private RectPose labelNormal,      labelZoom;

    public bool IsOpen { get; private set; }
    public bool IsBusy { get; private set; }

    private Button settingsButtonComponent;
  [SerializeField] private MenuButtonHighlight settingsHighlight;
    private CanvasGroup panelGroup, backGroup;
    private CanvasGroup[] hideGroups;

    private void Awake()
    {
        ValidateReferences();

        settingsButtonComponent = settingsButton.GetComponent<Button>();

        if (settingsHighlight == null)
            settingsHighlight = settingsButton.GetComponentInChildren<MenuButtonHighlight>(true);
        if (settingsHighlight == null)
            Debug.LogError("No encuentro el MenuButtonHighlight del botón de Ajustes. Asígnalo en el Inspector.", this);

        panelGroup = GetGroup(settingsPanel);
        backGroup  = GetGroup(backButton.gameObject);

        hideGroups = new CanvasGroup[hideWhileOpen.Length];
        for (int i = 0; i < hideWhileOpen.Length; i++)
            hideGroups[i] = GetGroup(hideWhileOpen[i]);

        backButton.onClick.AddListener(Close);
        ResetImmediate();
    }

    private void ValidateReferences()
    {
        if (semicircle == null)     Debug.LogError("Falta asignar: Semicircle", this);
        if (settingsButton == null) Debug.LogError("Falta asignar: Settings Button", this);
        if (settingsLabel == null)  Debug.LogError("Falta asignar: Settings Label", this);
        if (settingsPanel == null)  Debug.LogError("Falta asignar: Settings Panel", this);
        if (backButton == null)     Debug.LogError("Falta asignar: Back Button", this);
        if (hideWhileOpen == null || hideWhileOpen.Length == 0)
            Debug.LogError("Hide While Open está vacío", this);
        else
            for (int i = 0; i < hideWhileOpen.Length; i++)
                if (hideWhileOpen[i] == null) Debug.LogError($"Hide While Open [{i}] está vacío", this);
    }

    private static CanvasGroup GetGroup(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
    }

    // Corta cualquier animación y deja todo en su estado normal
    // (también se puede llamar al cerrar la pausa por otra vía)
    public void ResetImmediate()
    {
        StopAllCoroutines();
        ApplyNormalState();
    }

    // Deja todo normal SIN cortar corrutinas (se usa dentro de CloseRoutine)
    private void ApplyNormalState()
    {
        IsOpen = false;
        IsBusy = false;

        semicircleNormal.Apply(semicircle);
        buttonNormal.Apply(settingsButton);
        labelNormal.Apply(settingsLabel);

        for (int i = 0; i < hideWhileOpen.Length; i++)
        {
            hideWhileOpen[i].SetActive(true);
            hideGroups[i].alpha = 1f;
            hideGroups[i].interactable = true;
            hideGroups[i].blocksRaycasts = true;
        }

        settingsPanel.SetActive(false);
        backButton.gameObject.SetActive(false);

        if (settingsHighlight != null) settingsHighlight.SetLocked(false);
        if (settingsButtonComponent != null) settingsButtonComponent.interactable = true;
    }

    public void Open()
    {
        if (IsOpen || IsBusy) return;
        StartCoroutine(OpenRoutine());
    }

    public void Close()
    {
        if (!IsOpen || IsBusy) return;
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        IsBusy = true;

        if (settingsButtonComponent != null) settingsButtonComponent.interactable = false;
        if (settingsHighlight != null) settingsHighlight.SetLocked(true);

        // Los otros botones dejan de responder desde el primer instante
        foreach (var g in hideGroups)
        {
            g.interactable = false;
            g.blocksRaycasts = false;
        }

        // 1) Zoom + los otros botones se desvanecen
        yield return Animate(zoomDuration, k =>
        {
            float e = Ease(k);
            RectPose.Blend(semicircle,     semicircleNormal, semicircleZoom, e);
            RectPose.Blend(settingsButton, buttonNormal,     buttonZoom,     e);
            RectPose.Blend(settingsLabel,  labelNormal,      labelZoom,      e);
            foreach (var g in hideGroups) g.alpha = 1f - Mathf.Clamp01(k * 2.5f);
        });
        foreach (var go in hideWhileOpen) go.SetActive(false);

        // 2) Aparece el panel y el botón Volver
        panelGroup.alpha = 0f;
        backGroup.alpha = 0f;
        settingsPanel.SetActive(true);
        backButton.gameObject.SetActive(true);

        yield return Animate(fadeDuration, k =>
        {
            panelGroup.alpha = k;
            backGroup.alpha = k;
        });

        IsOpen = true;
        IsBusy = false;
        Select(firstSelectable != null ? firstSelectable.gameObject : backButton.gameObject);
    }

    private IEnumerator CloseRoutine()
    {
        IsBusy = true;
        EventSystem.current.SetSelectedGameObject(null);

        // 1) Se esconde el panel y el botón Volver
        yield return Animate(fadeDuration, k =>
        {
            panelGroup.alpha = 1f - k;
            backGroup.alpha = 1f - k;
        });
        settingsPanel.SetActive(false);
        backButton.gameObject.SetActive(false);

        // 2) Zoom inverso + reaparecen los otros botones
        for (int i = 0; i < hideWhileOpen.Length; i++)
        {
            hideGroups[i].alpha = 0f;
            hideWhileOpen[i].SetActive(true);
        }

        yield return Animate(zoomDuration, k =>
        {
            float e = 1f - Ease(k);
            RectPose.Blend(semicircle,     semicircleNormal, semicircleZoom, e);
            RectPose.Blend(settingsButton, buttonNormal,     buttonZoom,     e);
            RectPose.Blend(settingsLabel,  labelNormal,      labelZoom,      e);
            foreach (var g in hideGroups) g.alpha = Mathf.Clamp01((k - 0.3f) / 0.7f);
        });

        ApplyNormalState(); // valores exactos, reactiva botones y desbloquea el resaltado
        Select(settingsButton.gameObject);
    }

    // Usa tiempo sin escala: funciona con Time.timeScale = 0
    private static IEnumerator Animate(float duration, Action<float> step)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            step(t / duration);
            yield return null;
        }
        step(1f);
    }

    private static float Ease(float k) => k * k * (3f - 2f * k);

    private static void Select(GameObject go)
    {
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(go);
    }

    // ---------- Ayudas del editor ----------

    [ContextMenu("1) Guardar pose NORMAL (estado actual)")]
    private void CaptureNormal()
    {
        semicircleNormal = RectPose.Of(semicircle);
        buttonNormal = RectPose.Of(settingsButton);
        labelNormal = RectPose.Of(settingsLabel);
    }

    [ContextMenu("2) Guardar pose ZOOM (estado actual)")]
    private void CaptureZoom()
    {
        semicircleZoom = RectPose.Of(semicircle);
        buttonZoom = RectPose.Of(settingsButton);
        labelZoom = RectPose.Of(settingsLabel);
    }

    [ContextMenu("3) Aplicar pose NORMAL")]
    private void ApplyNormalInEditor()
    {
        semicircleNormal.Apply(semicircle);
        buttonNormal.Apply(settingsButton);
        labelNormal.Apply(settingsLabel);
    }
}