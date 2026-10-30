using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestor persistente en tiempo de ejecución. Escucha los cambios de escena
/// y aplica la restauración de posición, rotación, habilidades y misiones
/// cuando SaveSystem tiene un PendingLoadData en espera.
/// </summary>
public class SaveManager : MonoBehaviour
{
    private static SaveManager instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (instance != null) return;

        GameObject go = new GameObject("SaveManager");
        instance = go.AddComponent<SaveManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        // Si venimos del menú principal a una escena de menú, limpiamos pendientes
        if (scene.name.Equals("MENU", System.StringComparison.OrdinalIgnoreCase) ||
            scene.name.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
        {
            SaveSystem.PendingLoadData = null;
            return;
        }

        if (SaveSystem.PendingLoadData != null)
        {
            StartCoroutine(RestoreGameStateCoroutine(SaveSystem.PendingLoadData));
        }
    }

    private IEnumerator RestoreGameStateCoroutine(SaveData data)
    {
        // Esperamos a fin de frame para que todos los Awake y Start de la escena terminen
        yield return new WaitForEndOfFrame();

        Debug.Log($"[SaveManager] Restaurando partida guardada en escena '{data.sceneName}'...");

        // 1. Restaurar jugador (Nami)
        PlayerControls playerControls = Object.FindFirstObjectByType<PlayerControls>();
        if (playerControls != null)
        {
            Rigidbody namiRb = playerControls.GetComponent<Rigidbody>();
            if (namiRb != null)
            {
                namiRb.position = data.namiPosition;
                namiRb.rotation = Quaternion.Euler(data.namiEulerAngles);
                namiRb.linearVelocity = Vector3.zero;
                namiRb.angularVelocity = Vector3.zero;
            }

            playerControls.transform.position = data.namiPosition;
            playerControls.transform.eulerAngles = data.namiEulerAngles;
            playerControls.SetControlEnabled(true);

            // Restaurar habilidades
            InputManagerBueno inputManager = playerControls.GetComponent<InputManagerBueno>();
            if (inputManager != null)
            {
                inputManager.canMoveVertical = data.canMoveVertical;
                inputManager.canSprint = data.canSprint;
                inputManager.canHeadbutt = data.canHeadbutt;
                inputManager.canListen = data.canListen;
            }
        }
        else
        {
            Debug.LogWarning("[SaveManager] No se encontró a PlayerControls al restaurar la partida.");
        }

        // 2. Restaurar Mara (si aplica)
        MaraGuide maraGuide = Object.FindFirstObjectByType<MaraGuide>();
        if (maraGuide != null)
        {
            if (data.hasMara)
            {
                maraGuide.gameObject.SetActive(true);
                Rigidbody maraRb = maraGuide.GetComponent<Rigidbody>();
                if (maraRb != null)
                {
                    maraRb.position = data.maraPosition;
                    maraRb.rotation = Quaternion.Euler(data.maraEulerAngles);
                    maraRb.linearVelocity = Vector3.zero;
                    maraRb.angularVelocity = Vector3.zero;
                }
                maraGuide.transform.position = data.maraPosition;
                maraGuide.transform.eulerAngles = data.maraEulerAngles;
                maraGuide.enabled = true;
            }
            else
            {
                // En el Acto 2 Mara ya no está con Nami
                maraGuide.gameObject.SetActive(false);
            }
        }

        // 3. Restaurar misión activa
        if (!string.IsNullOrEmpty(data.currentMissionId))
        {
            MissionState.CurrentMissionId = data.currentMissionId;
            MissionUI missionUI = Object.FindFirstObjectByType<MissionUI>();
            if (missionUI != null)
            {
                missionUI.ShowMission(data.currentMissionId);
            }
        }

        // 4. Restaurar progreso de rocas rotas si aplica
        if (data.brokenRocksCount > 0)
        {
            RockSequenceManager rockSeq = Object.FindFirstObjectByType<RockSequenceManager>();
            if (rockSeq != null)
            {
                FieldInfo field = typeof(RockSequenceManager).GetField("brokenByPlayerCount", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(rockSeq, data.brokenRocksCount);
                    MethodInfo updateMethod = typeof(RockSequenceManager).GetMethod("UpdateMissionCounter", BindingFlags.NonPublic | BindingFlags.Instance);
                    updateMethod?.Invoke(rockSeq, null);
                }
            }
        }

        Debug.Log($"[SaveManager] Restauración de '{data.savepointName}' completada con éxito.");
        SaveSystem.PendingLoadData = null;
    }
}
