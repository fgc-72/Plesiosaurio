using UnityEngine;

// Singleton persistente compatible con el nuevo SaveSystem.
// Si existe SaveSystem, delega en él para guardar en JSON con persistencia real.
// Mantiene también PlayerPrefs como respaldo secundario para no romper llamadas existentes.
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
    private const string MaraXKey = "CheckpointMaraX";
    private const string MaraYKey = "CheckpointMaraY";
    private const string MaraZKey = "CheckpointMaraZ";
    private const string UnlockVerticalKey = "UnlockVertical";
    private const string UnlockSprintKey = "UnlockSprint";
    private const string UnlockHeadbuttKey = "UnlockHeadbutt";
    private const string UnlockListenKey = "UnlockListen";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Solo destruye el componente duplicado, NO el GameObject entero
            // (evita destruir WakeUpPoint u otros GameObjects en Acto 2)
            Destroy(this);
            return;
        }

        Instance = this;
        // Solo aplicar DontDestroyOnLoad si no es hijo de otro objeto
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        // NOTA: Se eliminó el ClearCheckpoint() automático que formateaba la partida cada vez que arrancaba el juego.
    }

    public void SaveCheckpoint(string sceneName, Vector3 position, Vector3 maraPosition, CheckpointUnlocks unlocks)
    {
        // 1. Guardar en SaveSystem JSON
        SaveData data = SaveSystem.CreateSaveDataFromCurrentScene(sceneName);
        if (data != null)
        {
            data.sceneName = SaveSystem.NormalizeSceneName(sceneName);
            data.namiPosition = position;
            data.maraPosition = maraPosition;
            data.canMoveVertical = unlocks.canMoveVertical;
            data.canSprint = unlocks.canSprint;
            data.canHeadbutt = unlocks.canHeadbutt;
            data.canListen = unlocks.canListen;
            SaveSystem.Save(data);
        }

        // 2. Respaldo secundario en PlayerPrefs
        PlayerPrefs.SetInt(HasCheckpointKey, 1);
        PlayerPrefs.SetString(SceneKey, sceneName);
        PlayerPrefs.SetFloat(PosXKey, position.x);
        PlayerPrefs.SetFloat(PosYKey, position.y);
        PlayerPrefs.SetFloat(PosZKey, position.z);
        PlayerPrefs.SetFloat(MaraXKey, maraPosition.x);
        PlayerPrefs.SetFloat(MaraYKey, maraPosition.y);
        PlayerPrefs.SetFloat(MaraZKey, maraPosition.z);
        PlayerPrefs.SetInt(UnlockVerticalKey, unlocks.canMoveVertical ? 1 : 0);
        PlayerPrefs.SetInt(UnlockSprintKey, unlocks.canSprint ? 1 : 0);
        PlayerPrefs.SetInt(UnlockHeadbuttKey, unlocks.canHeadbutt ? 1 : 0);
        PlayerPrefs.SetInt(UnlockListenKey, unlocks.canListen ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"Checkpoint guardado en {sceneName}: Nami={position}, Mara={maraPosition}");
    }

    public bool HasCheckpointFor(string sceneName)
    {
        if (SaveSystem.HasSaveFile())
        {
            SaveData data = SaveSystem.Load();
            if (data != null && SaveSystem.IsSceneMatch(data.sceneName, sceneName))
                return true;
        }

        return PlayerPrefs.GetInt(HasCheckpointKey, 0) == 1 &&
               SaveSystem.IsSceneMatch(PlayerPrefs.GetString(SceneKey, ""), sceneName);
    }

    public Vector3 GetCheckpointPosition()
    {
        if (SaveSystem.HasSaveFile())
        {
            SaveData data = SaveSystem.Load();
            if (data != null) return data.namiPosition;
        }

        return new Vector3(
            PlayerPrefs.GetFloat(PosXKey, 0f),
            PlayerPrefs.GetFloat(PosYKey, 0f),
            PlayerPrefs.GetFloat(PosZKey, 0f)
        );
    }

    public Vector3 GetCheckpointMaraPosition()
    {
        if (SaveSystem.HasSaveFile())
        {
            SaveData data = SaveSystem.Load();
            if (data != null) return data.maraPosition;
        }

        return new Vector3(
            PlayerPrefs.GetFloat(MaraXKey, 0f),
            PlayerPrefs.GetFloat(MaraYKey, 0f),
            PlayerPrefs.GetFloat(MaraZKey, 0f)
        );
    }

    public CheckpointUnlocks GetCheckpointUnlocks()
    {
        if (SaveSystem.HasSaveFile())
        {
            SaveData data = SaveSystem.Load();
            if (data != null)
            {
                return new CheckpointUnlocks
                {
                    canMoveVertical = data.canMoveVertical,
                    canSprint = data.canSprint,
                    canHeadbutt = data.canHeadbutt,
                    canListen = data.canListen,
                };
            }
        }

        return new CheckpointUnlocks
        {
            canMoveVertical = PlayerPrefs.GetInt(UnlockVerticalKey, 0) == 1,
            canSprint = PlayerPrefs.GetInt(UnlockSprintKey, 0) == 1,
            canHeadbutt = PlayerPrefs.GetInt(UnlockHeadbuttKey, 0) == 1,
            canListen = PlayerPrefs.GetInt(UnlockListenKey, 0) == 1,
        };
    }

    // Útil para reiniciar el juego desde cero
    public void ClearCheckpoint()
    {
        SaveSystem.DeleteSave();
        PlayerPrefs.SetInt(HasCheckpointKey, 0);
        PlayerPrefs.Save();
    }
}
