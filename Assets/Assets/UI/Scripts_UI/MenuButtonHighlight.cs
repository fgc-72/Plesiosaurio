using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MenuButtonHighlight : MonoBehaviour,
    IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    [Header("Referencias")]
    [SerializeField] private TMP_Text label;

    [Header("Flecha")]
    [SerializeField] private bool showArrow = true;
    public bool ShowArrow => showArrow;

    [Header("Ícono (opcional)")]
    [SerializeField] private RectTransform icon;
    [SerializeField] private float selectedIconScale = 1.2f;

    [Header("Escala del texto")]
    [SerializeField] private bool scaleLabel = true;
    [SerializeField] private float selectedLabelScale = 1.2f;

    [SerializeField] private float scaleSpeed = 14f;

    [Header("Texto")]
    [SerializeField] private Color normalColor   = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.4f, 1f);

    [Header("Resplandor (Underlay de TMP)")]
    [SerializeField] private Color glowColor = Color.white;
    [SerializeField, Range(0f, 1f)]  private float glowSoftness = 0.6f;
    [SerializeField, Range(-1f, 1f)] private float glowDilate   = 0.3f;

    private Vector3 iconBaseScale = Vector3.one;
    private Vector3 labelBaseScale = Vector3.one;
    private bool selected;
    private bool locked;
    private bool Highlighted => selected || locked;

    private bool IconIsValid => icon != null;
    private bool LabelScaleIsValid => scaleLabel && label != null;

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (IconIsValid) iconBaseScale = icon.localScale;
        if (label != null) labelBaseScale = label.rectTransform.localScale;
        ApplyVisuals();
    }

    private void Update()
    {
        // Con el bloqueo activo, la transición manda sobre las escalas
        if (locked) return;

        float k = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);

        if (IconIsValid)
        {
            Vector3 t = iconBaseScale * (Highlighted ? selectedIconScale : 1f);
            icon.localScale = Vector3.Lerp(icon.localScale, t, k);
        }

        if (LabelScaleIsValid)
        {
            Vector3 t = labelBaseScale * (Highlighted ? selectedLabelScale : 1f);
            label.rectTransform.localScale = Vector3.Lerp(label.rectTransform.localScale, t, k);
        }
    }

    private void OnDisable()
    {
        selected = false;
        ApplyVisuals();
        if (IconIsValid) icon.localScale = iconBaseScale;
        if (LabelScaleIsValid) label.rectTransform.localScale = labelBaseScale;
    }

    public void SetLocked(bool value)
    {
        locked = value;
        ApplyVisuals();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var s = GetComponent<Selectable>();
        if (s != null && !s.IsInteractable()) return;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData)   { selected = true;  ApplyVisuals(); }
    public void OnDeselect(BaseEventData eventData) { selected = false; ApplyVisuals(); }

    private void ApplyVisuals()
    {
        if (label == null) return;
        bool on = Highlighted;

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