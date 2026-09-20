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
        if (!other.CompareTag(playerTag))
            return;

        InputManagerBueno inputManager = other.GetComponent<InputManagerBueno>();
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

        Destroy(gameObject);
    }
}
