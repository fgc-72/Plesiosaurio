using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Ponlo en el objeto del ScrollRect del panel de ajustes.
/// Cuando la selección cambia (mando/teclado) a un elemento del Content que queda
/// fuera o cortado en el viewport, desplaza el contenido para que se vea.
/// Ignora los cambios de selección causados por el mouse (hover) para evitar
/// que el contenido se mueva bajo el puntero y entre en un bucle.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class ScrollRectAutoScroll : MonoBehaviour
{
    [SerializeField] private float padding = 24f;
    [SerializeField] private float speed = 14f;
    [Tooltip("Segundos tras mover el mouse durante los que no se hace auto-scroll.")]
    [SerializeField] private float mouseIgnoreTime = 0.15f;

    [Header("Stick derecho del mando")]
    [SerializeField] private bool rightStickScroll = true;
    [SerializeField] private float stickSpeed = 1200f;
    [SerializeField, Range(0f, 0.9f)] private float stickDeadzone = 0.2f;

    private ScrollRect scrollRect;
    private RectTransform viewport;
    private GameObject lastSelected;
    private RectTransform target;
    private float lastMouseMoveTime = -10f;
    private readonly Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)transform;
    }

    private void OnDisable()
    {
        lastSelected = null;
        target = null;
    }

    private void Update()
    {
        // Scroll libre con el stick derecho (tiene prioridad sobre el auto-scroll).
        var gp = Gamepad.current;
        if (rightStickScroll && gp != null && scrollRect.content != null)
        {
            float y = gp.rightStick.y.ReadValue();
            if (Mathf.Abs(y) > stickDeadzone)
            {
                scrollRect.StopMovement();
                var p = scrollRect.content.anchoredPosition;
                p.y -= y * stickSpeed * Time.unscaledDeltaTime; // stick arriba = ver lo de arriba
                scrollRect.content.anchoredPosition = p;
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                target = null;
                return;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.01f)
            lastMouseMoveTime = Time.unscaledTime;

        var es = EventSystem.current;
        GameObject sel = es != null ? es.currentSelectedGameObject : null;

        if (sel != lastSelected)
        {
            lastSelected = sel;
            bool fromMouse = Time.unscaledTime - lastMouseMoveTime < mouseIgnoreTime;
            bool inContent = sel != null && scrollRect.content != null
                             && sel.transform.IsChildOf(scrollRect.content);

            target = (inContent && !fromMouse) ? (RectTransform)sel.transform : null;
        }

        if (target == null || !target.gameObject.activeInHierarchy) return;

        // Posición del elemento en el espacio local del viewport.
        target.GetWorldCorners(corners);
        float bottom = viewport.InverseTransformPoint(corners[0]).y;
        float top = viewport.InverseTransformPoint(corners[1]).y;
        Rect vp = viewport.rect;

        float delta = 0f;
        if (top > vp.yMax - padding) delta = top - (vp.yMax - padding);      // se sale por arriba
        else if (bottom < vp.yMin + padding) delta = bottom - (vp.yMin + padding);   // se sale por abajo

        if (Mathf.Abs(delta) < 0.5f) { target = null; return; }

        scrollRect.StopMovement();
        float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        var pos = scrollRect.content.anchoredPosition;
        pos.y -= delta * k;
        scrollRect.content.anchoredPosition = pos;

        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
    }
}