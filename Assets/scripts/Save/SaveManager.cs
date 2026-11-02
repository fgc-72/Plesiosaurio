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
        // y desactivamos temporalmente a Nami si quedó persistente en DontDestroyOnLoad
        if (scene.name.Equals("MENU", System.StringComparison.OrdinalIgnoreCase) ||
            scene.name.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
        {
            SaveSystem.PendingLoadData = null;
            PlayerControls player = Object.FindFirstObjectByType<PlayerControls>();
            if (player != null)
            {
                player.transform.root.gameObject.SetActive(false);
            }
            return;
        }

        if (SaveSystem.PendingLoadData != null)
        {
            StartCoroutine(RestoreGameStateCoroutine(SaveSystem.PendingLoadData));
        }
        else if (SaveSystem.IsSceneMatch(scene.name, "acto2"))
        {
            StartCoroutine(CaptureAct2InitialSpawnCoroutine());
        }
    }

    private IEnumerator CaptureAct2InitialSpawnCoroutine()
    {
        yield return new WaitForEndOfFrame();

        PlayerControls playerControls = null;
        while (playerControls == null)
        {
            playerControls = Object.FindFirstObjectByType<PlayerControls>();
            yield return null;
        }

        // Si hay una cinemática en curso (como el despertar del Acto 2), esperamos a que termine
        // y el jugador reciba el control (IsControlEnabled == true)
        while (playerControls != null && !playerControls.IsControlEnabled)
        {
            yield return null;
        }

        if (playerControls != null)
        {
            Vector3 startPos = playerControls.transform.position;
            Quaternion startRot = playerControls.transform.rotation;
            CheckpointRespawner.SetAct2InitialSpawn(startPos, startRot);
            Debug.Log($"[SaveManager] Capturada posición inicial libre de Acto 2: {startPos}");
        }
    }

    private IEnumerator RestoreGameStateCoroutine(SaveData data)
    {
        // Esperamos a fin de frame para que todos los Awake y Start de la escena terminen
        yield return new WaitForEndOfFrame();

        Debug.Log($"[SaveManager] Restaurando partida guardada en escena '{data.sceneName}'...");

        // 1. Restaurar jugador (Nami)
        PlayerControls playerControls = Object.FindFirstObjectByType<PlayerControls>();

        // Si entramos a Acto 2 tras un inicio en frío y Nami no vino por DontDestroyOnLoad,
        // la traemos de SampleScene de forma aditiva en segundo plano
        if (playerControls == null && SaveSystem.IsSceneMatch(data.sceneName, "acto2"))
        {
            Debug.Log("[SaveManager] PlayerControls no encontrado en Acto 2. Cargando Nami desde SampleScene de forma aditiva...");
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
            playerControls = Object.FindFirstObjectByType<PlayerControls>();
            if (playerControls != null)
            {
                DontDestroyOnLoad(playerControls.transform.root.gameObject);
            }
            yield return SceneManager.UnloadSceneAsync("SampleScene");
        }

        if (playerControls != null)
        {
            playerControls.transform.root.gameObject.SetActive(true);
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
            Physics.SyncTransforms();
            playerControls.SyncRotationToCurrentTransform();

            // Restaurar habilidades
            InputManagerBueno inputManager = playerControls.GetComponent<InputManagerBueno>();
            if (inputManager != null)
            {
                inputManager.canMoveVertical = data.canMoveVertical;
                inputManager.canSprint = data.canSprint;
                inputManager.canHeadbutt = data.canHeadbutt;
                inputManager.canListen = data.canListen;
            }

            if (SaveSystem.IsSceneMatch(data.sceneName, "acto2"))
            {
                CheckpointRespawner.SetAct2InitialSpawn(data.namiPosition, Quaternion.Euler(data.namiEulerAngles));
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
