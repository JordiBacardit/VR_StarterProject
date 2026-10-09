using UnityEngine;

// Bala que avança en línia recta. Fa un raycast a cada pas per no travessar els cubs encara que vagi ràpid.
public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private LayerMask hitLayers = ~0;

    // Qui ha disparat la bala (la pistola): els seus colliders s'ignoren
    [HideInInspector] public Transform owner;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        var hits = Physics.RaycastAll(transform.position, transform.forward, step, hitLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Ignora la pròpia pistola i les mans del jugador
            if ((owner != null && hit.collider.transform.IsChildOf(owner)) || hit.collider.GetComponentInParent<Autohand.Hand>() != null)
                continue;

            // Cub de pistola: explota. Cub d'espasa: la bala s'atura però no el destrueix.
            var explode = hit.collider.GetComponentInParent<CubeExplode>();
            if (explode != null)
            {
                explode.Explode();
                Destroy(gameObject);
                return;
            }
            if (hit.collider.GetComponentInParent<CubePhysics>() != null || !hit.collider.isTrigger)
            {
                Destroy(gameObject);
                return;
            }
            // Altres triggers (p. ex. CubeKiller) s'ignoren
        }

        transform.position += transform.forward * step;
    }

    // Bala per defecte si la pistola no té bulletPrefab assignat
    public static Bullet CreateDefault(Vector3 position, Quaternion rotation)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bullet";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = Vector3.one * 0.02f;

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.08f;
        trail.startWidth = 0.015f;
        trail.endWidth = 0f;
        trail.material = go.GetComponent<MeshRenderer>().sharedMaterial;

        return go.AddComponent<Bullet>();
    }
}
