using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HideZone : MonoBehaviour
{
    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        var h = other.GetComponentInParent<PlayerHiding>();
        if (h != null) h.Enter();
    }

    void OnTriggerExit(Collider other)
    {
        var h = other.GetComponentInParent<PlayerHiding>();
        if (h != null) h.Exit();
    }
}