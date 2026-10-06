using UnityEngine;

// Singleton persistente (igual patrón que SceneFader/LocalizationManager).
// Guarda la ÚLTIMA posición de checkpoint en PlayerPrefs, así si el jugador
// cierra el juego y vuelve, no tiene que repetir todo desde el principio.
//
// NOTA: esto es un sistema de checkpoints simple (posición + escena), NO un
// sistema de guardado completo. Cuando armen el guardado real más adelante,
// esto puede seguir usándose como la parte que recuerda "dónde continuar".
// Agrupa las habilidades desbloqueadas del tutorial, para no tener que pasar
// 4 bools sueltos por todos lados.
[System.Serializable]
public struct CheckpointUnlocks
{
    public bool canMoveVertical;
    public bool canSprint;
    public bool canHeadbutt;
    public bool canListen;
}

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    private const string HasCheckpointKey = "HasCheckpoint";
    private const string SceneKey = "CheckpointScene";
    private const string PosXKey = "CheckpointX";
    private const string PosYKey = "CheckpointY";
    private const string PosZKey = "CheckpointZ";
    private const string UnlockVerticalKey = "UnlockVertical";
    private const string UnlockSprintKey = "UnlockSprint";
    private const string UnlockHeadbuttKey = "UnlockHeadbutt";
    private const string UnlockListenKey = "UnlockListen";

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

    public void SaveCheckpoint(string sceneName, Vector3 position, CheckpointUnlocks unlocks)
    {
        PlayerPrefs.SetInt(HasCheckpointKey, 1);
        PlayerPrefs.SetString(SceneKey, sceneName);
        PlayerPrefs.SetFloat(PosXKey, position.x);
        PlayerPrefs.SetFloat(PosYKey, position.y);
        PlayerPrefs.SetFloat(PosZKey, position.z);
        PlayerPrefs.SetInt(UnlockVerticalKey, unlocks.canMoveVertical ? 1 : 0);
        PlayerPrefs.SetInt(UnlockSprintKey, unlocks.canSprint ? 1 : 0);
        PlayerPrefs.SetInt(UnlockHeadbuttKey, unlocks.canHeadbutt ? 1 : 0);
        PlayerPrefs.SetInt(UnlockListenKey, unlocks.canListen ? 1 : 0);
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

    public CheckpointUnlocks GetCheckpointUnlocks()
    {
        return new CheckpointUnlocks
        {
            canMoveVertical = PlayerPrefs.GetInt(UnlockVerticalKey, 0) == 1,
            canSprint = PlayerPrefs.GetInt(UnlockSprintKey, 0) == 1,
            canHeadbutt = PlayerPrefs.GetInt(UnlockHeadbuttKey, 0) == 1,
            canListen = PlayerPrefs.GetInt(UnlockListenKey, 0) == 1,
        };
    }

    // Útil para probar el juego desde cero sin borrar PlayerPrefs a mano.
    public void ClearCheckpoint()
    {
        PlayerPrefs.SetInt(HasCheckpointKey, 0);
        PlayerPrefs.Save();
    }
}