using UnityEngine;

/// <summary>
/// Guarda cuál es la misión activa. Al ser una clase estática no vive en ninguna escena,
/// así que sobrevive a los cambios de escena sin necesitar DontDestroyOnLoad.
/// </summary>
public static class MissionState
{
    public static string CurrentMissionId;

    // Si tenés desactivado el "Domain Reload" al entrar en Play, las variables estáticas
    // se quedarían con el valor de la partida anterior. Esto las limpia al arrancar.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        CurrentMissionId = null;
    }
}
