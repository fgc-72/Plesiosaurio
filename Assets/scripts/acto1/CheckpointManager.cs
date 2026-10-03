using UnityEngine;

// Singleton persistente (igual patrón que SceneFader/LocalizationManager).
// Guarda la ÚLTIMA posición de checkpoint en PlayerPrefs, así si el jugador
// cierra el juego y vuelve, no tiene que repetir todo desde el principio.
//
// NOTA: esto es un sistema de checkpoints simple (posición + escena), NO un
// sistema de guardado completo. Cuando armen el guardado real más adelante,
// esto puede seguir usándose como la parte que recuerda "dónde continuar".
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    private const string HasCheckpointKey = "HasCheckpoint";
    private const string SceneKey = "CheckpointScene";
    private const string PosXKey = "CheckpointX";
    private const string PosYKey = "CheckpointY";
    private const string PosZKey = "CheckpointZ";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ClearCheckpoint();
    }

    public void SaveCheckpoint(string sceneName, Vector3 position)
    {
        PlayerPrefs.SetInt(HasCheckpointKey, 1);
        PlayerPrefs.SetString(SceneKey, sceneName);
        PlayerPrefs.SetFloat(PosXKey, position.x);
        PlayerPrefs.SetFloat(PosYKey, position.y);
        PlayerPrefs.SetFloat(PosZKey, position.z);
        PlayerPrefs.Save();

        Debug.Log($"Checkpoint guardado en {sceneName}: {position}");
    }

    public bool HasCheckpointFor(string sceneName)
    {
        return PlayerPrefs.GetInt(HasCheckpointKey, 0) == 1 &&
               PlayerPrefs.GetString(SceneKey, "") == sceneName;
    }

    public Vector3 GetCheckpointPosition()
    {
        return new Vector3(
            PlayerPrefs.GetFloat(PosXKey, 0f),
            PlayerPrefs.GetFloat(PosYKey, 0f),
            PlayerPrefs.GetFloat(PosZKey, 0f)
        );
    }

    // Útil para probar el juego desde cero sin borrar PlayerPrefs a mano.
    public void ClearCheckpoint()
    {
        PlayerPrefs.SetInt(HasCheckpointKey, 0);
        PlayerPrefs.Save();
    }
}
