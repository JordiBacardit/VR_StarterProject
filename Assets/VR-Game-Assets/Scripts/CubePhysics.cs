using UnityEngine;

// Mou el cub cap endavant (cap al jugador) i el destrueix quan entra en un trigger de l'escena (CubeKiller).
[RequireComponent(typeof(Rigidbody))]
public class CubePhysics : MonoBehaviour
{
    [Header("Moviment")]
    [Tooltip("Nivell de moviment: com més alt, més ràpid va el cub")]
    [Min(1)] public int movementLevel = 1;
    [SerializeField] private float baseSpeed = 2f;
    [SerializeField] private float speedPerLevel = 0.75f;

    [Header("Seguretat")]
    [Tooltip("Es destrueix igualment passat aquest temps, per si no toca cap trigger")]
    [SerializeField] private float maxLifetime = 30f;

    private Rigidbody rb;

    public float Speed => baseSpeed + (movementLevel - 1) * speedPerLevel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    void Start()
    {
        Destroy(gameObject, maxLifetime);
    }

    void FixedUpdate()
    {
        rb.linearVelocity = transform.forward * Speed;
    }

    public void Stop()
    {
        rb.linearVelocity = Vector3.zero;
        enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        // Només triggers estàtics de l'escena (CubeKiller), no altres cubs ni objectes amb Rigidbody (mans, armes...)
        if (other.isTrigger && other.attachedRigidbody == null)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.CubeMissed();
            AudioManager.Play(SfxEvent.CubeMissed);
            Destroy(gameObject);
            return;
        }

        // L'espasa tocant un cub que no es pot tallar (GunCube): arma equivocada, un sol cop per cub
        if (GetComponent<CubeSlice>() == null && other.GetComponentInParent<SwordSlicer>() != null)
            HitByWrongWeapon(other.ClosestPoint(transform.position));
    }

    private bool wrongWeaponHit = false;

    // Arma equivocada (bala contra un SwordCube o espasa contra un GunCube): so de cop metàl·lic i penalització
    public void HitByWrongWeapon(Vector3 hitPoint)
    {
        if (wrongWeaponHit)
            return;
        wrongWeaponHit = true;
        AudioManager.Play(SfxEvent.WrongWeapon, hitPoint);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.WrongWeapon();
    }
}
