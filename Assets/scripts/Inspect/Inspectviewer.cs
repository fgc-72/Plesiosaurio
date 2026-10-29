using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Va UNA sola vez en la escena (por ejemplo en el Canvas).
/// - Detecta el InspectableObject más cercano dentro de su rango.
/// - Muestra/oculta el aviso "presiona X".
/// - Al interactuar, instancia el prefab en un escenario lejano, lo renderiza
///   con una cámara aparte a una RenderTexture y deja girarlo.
/// </summary>
public class InspectViewer : MonoBehaviour
{
    [Header("Jugador (si lo dejas vacío busca el tag 'Player')")]
    [SerializeField] Transform player;

    [Header("UI")]
    [SerializeField] GameObject promptRoot;     // aviso "Presiona E / X"
    [SerializeField] GameObject inspectRoot;    // panel oscuro (padre del RawImage); se activa/desactiva entero
    [SerializeField] RawImage inspectImage;     // pantalla completa, muestra la RenderTexture

    [Header("Escenario de inspección")]
    [SerializeField] Transform stage;           // objeto vacío MUY lejos del mundo
    [SerializeField] Camera inspectCamera;      // hija de stage, desactivada de inicio
    [SerializeField] float targetSize = 1f;     // tamaño al que se normaliza cualquier modelo

    [Header("Rotación")]
    [SerializeField] float stickSpeed = 150f;       // grados por segundo (stick / flechas)
    [SerializeField] float mouseSensitivity = 0.3f; // grados por pixel
    [SerializeField] bool mouseNeedsClick = false;  // false = rota con solo mover el mouse

    [Header("Opciones")]
    [SerializeField] bool pauseTimeWhileOpen = false;

    [Header("Se desactivan mientras el visor está abierto (cámara del jugador, movimiento, Player Input...)")]
    [SerializeField] Behaviour[] disableWhileOpen;
    readonly List<Behaviour> disabledByUs = new();

    [Header("Eventos (para bloquear movimiento, etc.)")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    [Header("Input (opcional: si lo dejas vacío usa E / X, Esc / Círculo, stick derecho / flechas)")]
    [SerializeField] InputActionReference interactAction;
    [SerializeField] InputActionReference closeAction;
    [SerializeField] InputActionReference rotateAction;

    InputAction interact;
    InputAction close;
    InputAction rotate;

    public static bool IsOpen { get; private set; }

    Transform pivot;
    RenderTexture rt;
    bool isOpen;
    InspectableObject lastReported;

    void Awake()
    {
        // Si hay referencias en el Inspector se usan esas (configurables sin tocar código).
        // Si no, se crean bindings por defecto.
        if (interactAction != null) interact = interactAction.action;
        else
        {
            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonSouth");   // X en PlayStation / A en Xbox
        }

        if (closeAction != null) close = closeAction.action;
        else
        {
            close = new InputAction("CloseInspect", InputActionType.Button);
            close.AddBinding("<Keyboard>/escape");
            close.AddBinding("<Gamepad>/buttonEast");       // Círculo en PlayStation / B en Xbox
        }

        if (rotateAction != null) rotate = rotateAction.action;
        else
        {
            rotate = new InputAction("Rotate", InputActionType.Value, expectedControlType: "Vector2");
            rotate.AddBinding("<Gamepad>/rightStick");
            rotate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
        }

        if (player == null)
            ResolvePlayer();

        promptRoot.SetActive(false);
        SetViewVisible(false);

        // Fondo transparente: el oscurecido lo da el panel (inspectRoot), no la cámara.
        inspectCamera.clearFlags = CameraClearFlags.SolidColor;
        inspectCamera.backgroundColor = Color.clear;
        inspectCamera.enabled = false;
    }

    void OnEnable()
    {
        interact.Enable();
        close.Enable();
        rotate.Enable();
    }

    void OnDisable()
    {
        interact.Disable();
        close.Disable();
        rotate.Disable();
        if (isOpen) SetBlocked(false);
        if (isOpen && pauseTimeWhileOpen) Time.timeScale = 1f;
        isOpen = false;
        IsOpen = false;
    }

    void Update()
    {
        if (isOpen)
        {
            if (WasInteractOrClosePressed())
            {
                CloseInspect();
                return;
            }
            HandleRotation();
            return;
        }

        InspectableObject current = FindClosestInRange();
        if (promptRoot != null) promptRoot.SetActive(current != null);

        if (current != lastReported)
        {
            if (current != null)
            {
                Transform pTransform = ResolvePlayer();
                float dist = pTransform != null ? Vector3.Distance(pTransform.position, current.transform.position) : 0f;
                Debug.Log($"[InspectViewer] Objeto en rango: {current.name} (distancia: {dist:F1}m, rango: {current.range}m)");
            }
            lastReported = current;
        }

        if (current != null && WasInteractPressed())
            OpenInspect(current);
    }

    bool WasInteractPressed()
    {
        if (interact != null && interact.WasPressedThisFrame()) return true;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    bool WasInteractOrClosePressed()
    {
        if (interact != null && interact.WasPressedThisFrame()) return true;
        if (close != null && close.WasPressedThisFrame()) return true;
        if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)) return true;
        if (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame)) return true;
        return false;
    }

    Transform ResolvePlayer()
    {
        // 1. Si player ya fue asignado y sigue activo, aseguramos apuntar al objeto con PlayerControls o Rigidbody
        if (player != null && player.gameObject.activeInHierarchy)
        {
            var pc = player.GetComponentInChildren<PlayerControls>();
            if (pc != null) return pc.transform;

            var rb = player.GetComponentInChildren<Rigidbody>();
            if (rb != null) return rb.transform;

            return player;
        }

        // 2. Buscar por PlayerControls en la escena (es el componente que procesa el movimiento real de Nami)
        var controls = FindFirstObjectByType<PlayerControls>();
        if (controls != null)
        {
            player = controls.transform;
            return player;
        }

        // 3. Buscar por GameObject con tag "Player" y tomar su Rigidbody o hijo con controles
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
        {
            var pc = go.GetComponentInChildren<PlayerControls>();
            if (pc != null)
            {
                player = pc.transform;
                return player;
            }

            var rb = go.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                player = rb.transform;
                return player;
            }

            player = go.transform;
            return player;
        }

        // 4. Fallback a la cámara principal
        if (Camera.main != null)
            return Camera.main.transform;

        return null;
    }

    InspectableObject FindClosestInRange()
    {
        Transform pTransform = ResolvePlayer();
        if (pTransform == null) return null;

        // Fallback si la lista estática quedó vacía tras cambio de escena o recarga
        if (InspectableObject.All.Count == 0)
        {
            var found = FindObjectsByType<InspectableObject>(FindObjectsSortMode.None);
            foreach (var o in found)
            {
                if (o != null && o.isActiveAndEnabled && !InspectableObject.All.Contains(o))
                    InspectableObject.All.Add(o);
            }
        }

        InspectableObject best = null;
        float bestSqr = float.MaxValue;
        Vector3 p = pTransform.position;

        for (int i = InspectableObject.All.Count - 1; i >= 0; i--)
        {
            var o = InspectableObject.All[i];
            if (o == null)
            {
                InspectableObject.All.RemoveAt(i);
                continue;
            }
            if (!o.isActiveAndEnabled) continue;

            float sqr = (o.transform.position - p).sqrMagnitude;
            if (sqr <= o.range * o.range && sqr < bestSqr)
            {
                best = o;
                bestSqr = sqr;
            }
        }
        return best;
    }

    void OpenInspect(InspectableObject source)
    {
        if (source.inspectPrefab == null)
        {
            Debug.LogWarning($"{source.name}: no tiene inspectPrefab asignado.", source);
            return;
        }

        // RenderTexture del tamaño de la pantalla.
        rt = new RenderTexture(Screen.width, Screen.height, 24) { antiAliasing = 4 };
        inspectCamera.targetTexture = rt;
        inspectCamera.enabled = true;
        inspectImage.texture = rt;

        SpawnObject(source);

        promptRoot.SetActive(false);
        SetViewVisible(true);
        SetBlocked(true);
        isOpen = true;
        IsOpen = true;

        if (pauseTimeWhileOpen) Time.timeScale = 0f;
        onOpened.Invoke();
    }

    void SpawnObject(InspectableObject source)
    {
        pivot = new GameObject("InspectPivot").transform;
        pivot.SetParent(stage, false);

        var instance = Instantiate(source.inspectPrefab, pivot);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        // Centrar visualmente el modelo en el pivote y normalizar su tamaño,
        // así cualquier modelo gira sobre su centro y cabe en pantalla.
        float scale = source.inspectScale;

        if (source.autoFit && CalculateBounds(instance, out Bounds b))
        {
            instance.transform.position += pivot.position - b.center;

            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim > 0.0001f)
                scale *= targetSize / maxDim;
        }

        pivot.localScale = Vector3.one * scale;
        pivot.localRotation = Quaternion.Euler(source.startRotation);
    }

    // Mide el modelo real. En personajes animados, renderer.bounds suele venir inflado
    // (o desfasado), y eso hacía que el modelo se escalara de menos y se viera diminuto.
    static bool CalculateBounds(GameObject root, out Bounds total)
    {
        total = default;
        bool has = false;

        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            Bounds b;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                b = TransformBounds(baked.bounds, m);
                Destroy(baked);
            }
            else
            {
                b = r.bounds;
            }

            if (!has) { total = b; has = true; }
            else total.Encapsulate(b);
        }
        return has;
    }

    static Bounds TransformBounds(Bounds local, Matrix4x4 m)
    {
        Vector3 c = local.center;
        Vector3 e = local.extents;
        var result = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            result.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return result;
    }

    void HandleRotation()
    {
        if (pivot == null) return;

        // Stick derecho / flechas (Time.unscaledDeltaTime para que funcione con el tiempo pausado).
        Vector2 input = rotate.ReadValue<Vector2>();
        float yaw   = input.x * stickSpeed * Time.unscaledDeltaTime;
        float pitch = input.y * stickSpeed * Time.unscaledDeltaTime;

        // Mouse: arrastrar con clic izquierdo.
        var mouse = Mouse.current;
        if (mouse != null && (!mouseNeedsClick || mouse.leftButton.isPressed))
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw   += d.x * mouseSensitivity;
            pitch += d.y * mouseSensitivity;
        }

        // Si algún eje te queda invertido, cambia el signo aquí.
        pivot.Rotate(Vector3.up, -yaw, Space.World);
        pivot.Rotate(inspectCamera.transform.right, pitch, Space.World);
    }

    // Activa o desactiva el panel oscuro y el RawImage. El RawImage se activa por separado
    // para que funcione aunque lo hayas dejado desactivado en el editor.
    void SetViewVisible(bool visible)
    {
        if (inspectRoot != null) inspectRoot.SetActive(visible);
        inspectImage.gameObject.SetActive(visible);
    }

    // Apaga (o devuelve) los componentes de la lista disableWhileOpen: cámara, movimiento, etc.
    // Solo reactiva los que estaban activos antes de abrir.
    void SetBlocked(bool blocked)
    {
        IsOpen = blocked;

        if (blocked)
        {
            disabledByUs.Clear();
            if (disableWhileOpen != null)
            {
                foreach (var b in disableWhileOpen)
                {
                    if (b != null && b.enabled)
                    {
                        b.enabled = false;
                        disabledByUs.Add(b);
                    }
                }
            }

            // Desactivar controles de Nami (movimiento, cabezazo, manager)
            Transform p = ResolvePlayer();
            if (p != null)
            {
                var rb = p.GetComponent<Rigidbody>() ?? p.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                var root = p.root;
                foreach (var b in root.GetComponentsInChildren<Behaviour>())
                {
                    if (b != null && b.enabled && !disabledByUs.Contains(b))
                    {
                        string n = b.GetType().Name;
                        if (n == "PlayerControls" || n == "HeadbuttController" || n == "PlayerManager")
                        {
                            b.enabled = false;
                            disabledByUs.Add(b);
                        }
                    }
                }
            }

            // Desactivar control de rotación de la cámara Cinemachine
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (b != null && b.enabled && b.GetType().Name == "CinemachineInputAxisController")
                {
                    if (!disabledByUs.Contains(b))
                    {
                        b.enabled = false;
                        disabledByUs.Add(b);
                    }
                }
            }
        }
        else
        {
            foreach (var b in disabledByUs)
                if (b != null) b.enabled = true;
            disabledByUs.Clear();
        }
    }

    void CloseInspect()
    {
        if (pivot != null) Destroy(pivot.gameObject);

        inspectCamera.enabled = false;
        inspectCamera.targetTexture = null;
        inspectImage.texture = null;
        SetViewVisible(false);

        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        SetBlocked(false);
        isOpen = false;
        IsOpen = false;
        if (pauseTimeWhileOpen) Time.timeScale = 1f;
        onClosed.Invoke();
    }
}
