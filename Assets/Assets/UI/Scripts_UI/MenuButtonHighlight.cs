using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class MenuButtonHighlight : MonoBehaviour,
    IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    [Header("Referencias")]
    [SerializeField] private TMP_Text label;

    [Header("Flecha")]
    [SerializeField] private bool showArrow = true; // false en los botones de la pausa con ícono
    public bool ShowArrow => showArrow;

    [Header("Ícono (opcional)")]
    [SerializeField] private RectTransform icon;
    [SerializeField] private float selectedIconScale = 1.2f;
    [SerializeField] private float iconScaleSpeed = 14f;

    [Header("Texto")]
    [SerializeField] private Color normalColor   = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.4f, 1f);

    [Header("Resplandor (Underlay de TMP)")]
    [SerializeField] private Color glowColor = Color.white;
    [SerializeField, Range(0f, 1f)]   private float glowSoftness = 0.6f;
    [SerializeField, Range(-1f, 1f)]  private float glowDilate   = 0.3f;

    private Vector3 iconBaseScale = Vector3.one;
    private bool highlighted;

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (icon != null) iconBaseScale = icon.localScale;
        SetHighlighted(false);
    }

    private void Update()
    {
        if (icon == null) return;
        Vector3 target = iconBaseScale * (highlighted ? selectedIconScale : 1f);
        icon.localScale = Vector3.Lerp(icon.localScale, target,
            1f - Mathf.Exp(-iconScaleSpeed * Time.unscaledDeltaTime));
    }

    private void OnDisable()
    {
        SetHighlighted(false);
        if (icon != null) icon.localScale = iconBaseScale; // sin animación al reabrir
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData)   => SetHighlighted(true);
    public void OnDeselect(BaseEventData eventData) => SetHighlighted(false);

    private void SetHighlighted(bool on)
    {
        highlighted = on;
        if (label == null) return;

        label.color = on ? selectedColor : normalColor;

        Material mat = label.fontMaterial;
        if (on)
        {
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, glowColor);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, glowSoftness);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, glowDilate);
        }
        else
        {
            mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        }
        label.UpdateMeshPadding();
    }
}