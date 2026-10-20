using UnityEngine;

/// <summary>
/// Ponlo en el objeto raíz del panel de ajustes (el que se activa/desactiva).
/// Mientras el panel esté abierto, el botón "Ajustes" del menú se ve como seleccionado
/// (solo visualmente) usando el SetLocked de MenuButtonHighlight.
/// </summary>
public class SettingsButtonLock : MonoBehaviour
{
    [Tooltip("El MenuButtonHighlight del botón Ajustes del menú principal.")]
    [SerializeField] private MenuButtonHighlight settingsButtonHighlight;

    private void OnEnable()
    {
        if (settingsButtonHighlight != null) settingsButtonHighlight.SetLocked(true);
    }

    private void OnDisable()
    {
        if (settingsButtonHighlight != null) settingsButtonHighlight.SetLocked(false);
    }
}
