using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Serializable]
    public class InputCombo
    {
        public string nombre; // solo para identificarlo en el inspector
        [Tooltip("Todos estos inputs deben estar presionados a la vez")]
        public List<InputActionReference> inputs = new();
    }

    [SerializeField] private GameObject glow;
    [SerializeField] private bool activarConHover = true;
    [Tooltip("Si CUALQUIERA de estos combos está presionado, se activa el glow")]
    [SerializeField] private List<InputCombo> combos = new();

    private bool hovering;
    private bool glowActivo;

    void Awake() => SetGlow(false);

    void OnEnable()
    {
        // Por si nadie más habilitó estas acciones (ej. no hay PlayerInput)
        foreach (var combo in combos)
            foreach (var input in combo.inputs)
                if (input != null) input.action.Enable();
    }

    void OnDisable()
    {
        hovering = false;
        SetGlow(false);
    }

    void Update()
    {
        bool porInput = AlgunComboPresionado();
        SetGlow((activarConHover && hovering) || porInput);
    }

    private bool AlgunComboPresionado()
    {
        foreach (var combo in combos)
        {
            if (combo.inputs.Count == 0) continue;

            bool todos = true;
            foreach (var input in combo.inputs)
            {
                if (input == null || !input.action.IsPressed())
                {
                    todos = false;
                    break;
                }
            }
            if (todos) return true;
        }
        return false;
    }

    private void SetGlow(bool estado)
    {
        if (glowActivo == estado) return; // evita SetActive cada frame
        glowActivo = estado;
        glow.SetActive(estado);
    }

    public void OnPointerEnter(PointerEventData e) => hovering = true;
    public void OnPointerExit(PointerEventData e) => hovering = false;
}