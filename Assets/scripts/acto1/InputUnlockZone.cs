using UnityEngine;

// Pon uno de estos en cada punto del mapa donde quieras "enseñar" una habilidad
// nueva durante el tutorial. Al entrar, desbloquea esa habilidad para siempre
// y se autodestruye (ya cumplió su función).
[RequireComponent(typeof(Collider))]
public class InputUnlockZone : MonoBehaviour
{
    public enum UnlockType { MovimientoVertical, Sprint, Cabezazo, ModoEscucha }

    [SerializeField] private UnlockType unlockType;
    [SerializeField] private string playerTag = "Player";

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag) && (other.transform.root == null || !other.transform.root.CompareTag(playerTag)))
            return;

        InputManagerBueno inputManager = other.GetComponent<InputManagerBueno>()
            ?? other.GetComponentInParent<InputManagerBueno>()
            ?? other.GetComponentInChildren<InputManagerBueno>();

        if (inputManager == null)
            return;

        switch (unlockType)
        {
            case UnlockType.MovimientoVertical:
                inputManager.canMoveVertical = true;
                break;
            case UnlockType.Sprint:
                inputManager.canSprint = true;
                break;
            case UnlockType.Cabezazo:
                inputManager.canHeadbutt = true;
                break;
            case UnlockType.ModoEscucha:
                inputManager.canListen = true;
                break;
        }

        if (CheckpointManager.Instance != null)
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            CheckpointUnlocks cur = CheckpointManager.Instance.GetCheckpointUnlocks();
            if (unlockType == UnlockType.MovimientoVertical) cur.canMoveVertical = true;
            if (unlockType == UnlockType.Sprint) cur.canSprint = true;
            if (unlockType == UnlockType.Cabezazo) cur.canHeadbutt = true;
            if (unlockType == UnlockType.ModoEscucha) cur.canListen = true;

            CheckpointManager.Instance.SaveCheckpoint(scene, other.transform.position, other.transform.position, cur);
        }

        // Destruye únicamente este componente para no interrumpir otros scripts en el mismo GameObject (ej. ScriptedEvent o CheckpointTrigger)
        Destroy(this);
    }
}
