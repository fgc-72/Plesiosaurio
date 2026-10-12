using UnityEngine;
using UnityEngine.EventSystems;

public class HoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject glow;

    void Awake() => glow.SetActive(false);

    public void OnPointerEnter(PointerEventData e) => glow.SetActive(true);
    public void OnPointerExit(PointerEventData e) => glow.SetActive(false);
}