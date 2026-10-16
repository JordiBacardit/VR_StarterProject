using UnityEngine;

// Al SwordCube: quan la fulla de l'espasa el toca prou ràpid, el talla en dues meitats
// exactament pel pla per on passa l'espasa (efecte + so) i el cub original es destrueix.
public class CubeSlice : MonoBehaviour
{
    [Tooltip("Velocitat mínima de la fulla (m/s) per tallar el cub")]
    [SerializeField] private float minSliceSpeed = 1.5f;
    [SerializeField] private GameObject sliceVfx;
    [SerializeField] private AudioClip sliceSound;
    [SerializeField] private float vfxLifetime = 2f;

    [Header("Meitats")]
    [Tooltip("Material de la cara tallada (si és buit, el mateix del cub)")]
    [SerializeField] private Material capMaterial;
    [Tooltip("Velocitat amb què se separen les dues meitats")]
    [SerializeField] private float separationSpeed = 1.5f;
    [Tooltip("Quant de la velocitat de l'espasa reben les meitats")]
    [SerializeField, Range(0f, 1f)] private float swordVelocityTransfer = 0.15f;
    [SerializeField] private float piecesLifetime = 2.5f;

    private bool sliced = false;

    void OnTriggerEnter(Collider other)
    {
        if (sliced)
            return;

        var sword = other.GetComponentInParent<SwordSlicer>();
        if (sword == null || sword.Speed < minSliceSpeed)
            return;

        Slice(sword.GetCutPlane(transform.position), other.ClosestPoint(transform.position), sword);
    }

    public void Slice(Plane worldPlane, Vector3 hitPoint, SwordSlicer sword = null)
    {
        if (sliced)
            return;
        sliced = true;

        CreateHalves(worldPlane, sword);

        if (sliceVfx != null)
        {
            var vfx = Instantiate(sliceVfx, hitPoint, transform.rotation);
            Destroy(vfx, vfxLifetime);
        }

        AudioManager.PlaySfx(sliceSound, hitPoint);

        if (sword != null)
            sword.OnSliced();

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.CubeDestroyed();

        Destroy(gameObject);
    }

    private void CreateHalves(Plane worldPlane, SwordSlicer sword)
    {
        var meshFilter = GetComponent<MeshFilter>();
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshFilter == null || meshRenderer == null || meshFilter.sharedMesh == null)
            return;

        // Pla en espai local del cub (la normal es transforma amb la transposada per si hi ha escala no uniforme)
        Vector3 localPoint = transform.InverseTransformPoint(worldPlane.ClosestPointOnPlane(transform.position));
        Vector3 localNormal = transform.localToWorldMatrix.transpose.MultiplyVector(worldPlane.normal).normalized;
        var localPlane = new Plane(localNormal, localPoint);

        if (!MeshSlicer.Slice(meshFilter.sharedMesh, localPlane, out Mesh positive, out Mesh negative))
            return;

        // Materials: els del cub + el de la tapa
        var source = meshRenderer.sharedMaterials;
        var materials = new Material[source.Length + 1];
        source.CopyTo(materials, 0);
        materials[source.Length] = capMaterial != null ? capMaterial : (source.Length > 0 ? source[0] : null);

        var cubeBody = GetComponent<Rigidbody>();
        Vector3 baseVelocity = cubeBody != null ? cubeBody.linearVelocity * 0.3f : Vector3.zero;
        if (sword != null)
            baseVelocity += sword.TipVelocity * swordVelocityTransfer;

        SpawnHalf(positive, materials, baseVelocity + worldPlane.normal * separationSpeed);
        SpawnHalf(negative, materials, baseVelocity - worldPlane.normal * separationSpeed);
    }

    private void SpawnHalf(Mesh mesh, Material[] materials, Vector3 velocity)
    {
        var half = new GameObject(name + "_Half");
        half.transform.SetPositionAndRotation(transform.position, transform.rotation);
        half.transform.localScale = transform.lossyScale;

        half.AddComponent<MeshFilter>().sharedMesh = mesh;
        half.AddComponent<MeshRenderer>().sharedMaterials = materials;

        var collider = half.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;

        var body = half.AddComponent<Rigidbody>();
        body.mass = 0.3f;
        body.linearVelocity = velocity;
        body.angularVelocity = Random.insideUnitSphere * 4f;

        half.AddComponent<SlicedPiece>().Init(mesh, piecesLifetime);
    }
}

// Meitat d'un cub tallat: s'encongeix al final de la seva vida i es destrueix (amb la malla generada).
public class SlicedPiece : MonoBehaviour
{
    private Mesh generatedMesh;
    private float lifetime;
    private float age;
    private Vector3 startScale;
    private const float ShrinkTime = 0.4f;

    public void Init(Mesh mesh, float life)
    {
        generatedMesh = mesh;
        lifetime = life;
        startScale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        float remaining = lifetime - age;
        if (remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        if (remaining < ShrinkTime)
            transform.localScale = startScale * (remaining / ShrinkTime);
    }

    void OnDestroy()
    {
        if (generatedMesh != null)
            Destroy(generatedMesh);
    }
}
