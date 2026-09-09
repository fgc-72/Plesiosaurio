using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HeadbuttHitbox : MonoBehaviour
{
    private bool isActive;
    private float activeTimer;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Update()
    {
        if (!isActive)
            return;

        activeTimer -= Time.deltaTime;

        if (activeTimer <= 0f)
            isActive = false;
    }

    // Llamado por HeadbuttController cuando se presiona el botón.
    public void Activate(float duration)
    {
        isActive = true;
        activeTimer = duration;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive)
            return;

        // GetComponentInParent por si el collider de la roca vive en un hijo
        // distinto al que tiene el script DestructibleRock.
        DestructibleRock rock = other.GetComponentInParent<DestructibleRock>();

        if (rock != null)
        {
            Debug.Log("💥 Hitbox impactó: " + other.name); // TEMPORAL: borrar cuando tengas la animación
            rock.TakeHit();
        }
    }

    // Dibuja la hitbox en el Scene View: rojo mientras está activa, gris cuando no.
    // Solo para debug visual, no afecta el juego compilado.
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = isActive ? Color.red : new Color(1f, 1f, 1f, 0.3f);

        if (col is SphereCollider sphere)
            Gizmos.DrawWireSphere(transform.position, sphere.radius * transform.lossyScale.x);
    }
}