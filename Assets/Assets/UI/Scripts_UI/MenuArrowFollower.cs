using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class MenuArrowFollower : MonoBehaviour
{
    [Header("Posición")]
    [SerializeField] private Vector2 offset = new Vector2(-15f, 0f);
    [SerializeField] private bool followTextEdge = true;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 18f;

    [Header("Levitación")]
    [SerializeField] private float pulseAmount = 0.12f; // pulso de escala (±12%)
    [SerializeField] private float pulseSpeed  = 2.5f;
    [SerializeField] private float bobHeight   = 4f;    // subida/bajada en unidades del Canvas (0 = desactivado)

    private RectTransform rect;
    private RectTransform currentTarget;
    private Vector3 basePos;
    private Vector3 baseScale;
    private bool snapNext = true;

    private void Awake()
    {
        rect = (RectTransform)transform;
        baseScale = rect.localScale;
        SetVisible(false);
    }

    private void LateUpdate()
    {
      var es = EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;

        // Solo seguimos botones del menú principal que estén realmente visibles
    MenuButtonHighlight hl = selected != null ? selected.GetComponent<MenuButtonHighlight>() : null;
    bool valid = hl != null && hl.ShowArrow && selected.activeInHierarchy;

        if (!valid)
        {
            SetVisible(false);
            currentTarget = null;
            snapNext = true;   // al volver, aparece directo sin deslizarse desde otro lado
            return;
        }

        if (selected.transform is RectTransform sel && sel != currentTarget)
            currentTarget = sel;

        if (currentTarget == null) return;

        Vector3 targetPos = GetTargetPosition(currentTarget);

        if (snapNext)
        {
            basePos = targetPos;
            snapNext = false;
            SetVisible(true);
        }
        else
        {
            basePos = Vector3.Lerp(basePos, targetPos,
                1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime));
        }

        float wave = Mathf.Sin(Time.unscaledTime * pulseSpeed);
        float canvasScale = rect.parent != null ? rect.parent.lossyScale.y : 1f;

        rect.position = basePos + Vector3.up * wave * bobHeight * canvasScale;
        rect.localScale = baseScale * (1f + wave * pulseAmount);
    }

    private Vector3 GetTargetPosition(RectTransform target)
    {
        Vector3 worldEdge;

        TMP_Text tmp = followTextEdge ? target.GetComponentInChildren<TMP_Text>() : null;
        if (tmp != null)
        {
            tmp.ForceMeshUpdate();
            Bounds b = tmp.textBounds;
            worldEdge = tmp.transform.TransformPoint(new Vector3(b.min.x, b.center.y, 0f));
        }
        else
        {
            Rect r = target.rect;
            worldEdge = target.TransformPoint(new Vector3(r.xMin, r.center.y, 0f));
        }

        float scale = rect.parent != null ? rect.parent.lossyScale.x : 1f;
        return worldEdge + new Vector3(offset.x * scale, offset.y * scale, 0f);
    }

    private void SetVisible(bool visible)
    {
        var img = GetComponent<UnityEngine.UI.Graphic>();
        if (img != null) img.enabled = visible;
    }
}