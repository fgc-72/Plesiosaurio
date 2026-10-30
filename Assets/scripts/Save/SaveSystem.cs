using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sistema central de guardado y carga en formato JSON.
/// Se encarga de serializar el estado del juego en un archivo en Application.persistentDataPath,
/// capturar el estado actual de la escena y coordinar la carga de escenas con restauración.
/// </summary>
public static class SaveSystem
{
    private const string SaveFileName = "savegame.json";

    public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    /// <summary>
    /// Guarda en memoria la partida pendiente de aplicar apenas termine de cargar la escena.
    /// </summary>
    public static SaveData PendingLoadData { get; set; }

    /// <summary>
    /// Caché en memoria del último guardado realizado.
    /// </summary>
    public static SaveData LatestSaveData { get; private set; }

    public static event Action<SaveData> OnGameSaved;
    public static event Action OnSaveDeleted;

    /// <summary>
    /// Comprueba si existe un archivo de partida guardada válido.
    /// </summary>
    public static bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }

    /// <summary>
    /// Guarda los datos provistos en disco de forma atómica.
    /// </summary>
    public static bool Save(SaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[SaveSystem] Intento de guardar datos nulos.");
            return false;
        }

        try
        {
            string json = JsonUtility.ToJson(data, true);
            string tempPath = SaveFilePath + ".tmp";

            File.WriteAllText(tempPath, json);
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
            }
            File.Move(tempPath, SaveFilePath);

            LatestSaveData = data;
            Debug.Log($"[SaveSystem] Partida guardada con éxito en {SaveFilePath}");

            OnGameSaved?.Invoke(data);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Error al guardar partida: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Carga y deserializa el archivo de guardado desde el disco.
    /// </summary>
    public static SaveData Load()
    {
        if (!HasSaveFile())
            return null;

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            LatestSaveData = data;
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Error al leer archivo de guardado: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Elimina el archivo de guardado (para iniciar una nueva partida limpia).
    /// </summary>
    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
            }
            string tempPath = SaveFilePath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            LatestSaveData = null;
            PendingLoadData = null;
            Debug.Log("[SaveSystem] Partida eliminada.");

            OnSaveDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Error al eliminar guardado: {ex.Message}");
        }
    }

    /// <summary>
    /// Captura el estado en vivo de los objetos en la escena actual para crear un SaveData listo para guardar.
    /// </summary>
    public static SaveData CreateSaveDataFromCurrentScene(string savepointName = "Checkpoint")
    {
        var data = new SaveData();
        data.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        data.sceneName = NormalizeSceneName(SceneManager.GetActiveScene().name);
        data.savepointName = savepointName;

        // 1. Jugador (Nami)
        PlayerControls playerControls = UnityEngine.Object.FindFirstObjectByType<PlayerControls>();
        if (playerControls != null)
        {
            Rigidbody rb = playerControls.GetComponent<Rigidbody>();
            if (rb != null)
            {
                data.namiPosition = rb.position;
                data.namiEulerAngles = rb.rotation.eulerAngles;
            }
            else
            {
                data.namiPosition = playerControls.transform.position;
                data.namiEulerAngles = playerControls.transform.eulerAngles;
            }

            // Habilidades desbloqueadas
            InputManagerBueno inputManager = playerControls.GetComponent<InputManagerBueno>();
            if (inputManager != null)
            {
                data.canMoveVertical = inputManager.canMoveVertical;
                data.canSprint = inputManager.canSprint;
                data.canHeadbutt = inputManager.canHeadbutt;
                data.canListen = inputManager.canListen;
            }
        }
        else
        {
            Debug.LogWarning("[SaveSystem] No se encontró a PlayerControls en la escena activa.");
        }

        // 2. Mara (si está en la escena y activa)
        MaraGuide maraGuide = UnityEngine.Object.FindFirstObjectByType<MaraGuide>();
        if (maraGuide != null && maraGuide.gameObject.activeInHierarchy)
        {
            data.hasMara = true;
            data.maraPosition = maraGuide.transform.position;
            data.maraEulerAngles = maraGuide.transform.eulerAngles;
        }
        else
        {
            data.hasMara = false;
        }

        // 3. Misión activa
        data.currentMissionId = MissionState.CurrentMissionId;

        // 4. Progreso de rocas rotas
        RockSequenceManager rockSeq = UnityEngine.Object.FindFirstObjectByType<RockSequenceManager>();
        if (rockSeq != null)
        {
            data.brokenRocksCount = rockSeq.BrokenCount;
        }

        return data;
    }

    /// <summary>
    /// Inicia la carga de la partida: lee el archivo, establece PendingLoadData y cambia a la escena guardada.
    /// </summary>
    public static bool LoadGame()
    {
        SaveData data = Load();
        if (data == null)
        {
            Debug.LogWarning("[SaveSystem] No se pudo cargar: no hay archivo de guardado.");
            return false;
        }

        PendingLoadData = data;
        string targetScene = NormalizeSceneName(data.sceneName);

        Debug.Log($"[SaveSystem] Cargando escena '{targetScene}' para restaurar checkpoint '{data.savepointName}'...");

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.LoadScene(targetScene);
        }
        else
        {
            SceneManager.LoadScene(targetScene);
        }

        return true;
    }

    /// <summary>
    /// Normaliza nombres de escena para evitar discrepancias históricas (ej: Acto1 vs SampleScene).
    /// </summary>
    public static string NormalizeSceneName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "SampleScene";

        if (rawName.Equals("Acto1", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("Acto1-mitad", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("SampleScene", StringComparison.OrdinalIgnoreCase))
        {
            return "SampleScene";
        }

        if (rawName.Equals("Acto2", StringComparison.OrdinalIgnoreCase) ||
            rawName.Equals("acto2", StringComparison.OrdinalIgnoreCase))
        {
            return "acto2";
        }

        return rawName;
    }

    /// <summary>
    /// Compara dos nombres de escena considerando alias conocidos del proyecto.
    /// </summary>
    public static bool IsSceneMatch(string sceneA, string sceneB)
    {
        if (string.IsNullOrEmpty(sceneA) || string.IsNullOrEmpty(sceneB)) return false;
        return string.Equals(NormalizeSceneName(sceneA), NormalizeSceneName(sceneB), StringComparison.OrdinalIgnoreCase);
    }
}
