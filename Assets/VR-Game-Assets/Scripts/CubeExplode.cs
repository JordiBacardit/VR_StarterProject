using UnityEngine;

// Al GunCube: quan rep una bala amaga el cub sencer i reprodueix l'animació de trencament.
public class CubeExplode : MonoBehaviour
{
    [SerializeField] private GameObject mainCube;
    [SerializeField] private GameObject shatterObject;
    [SerializeField] private string shatterClip = "shatter";
    [Tooltip("Temps extra abans de destruir el cub un cop acabada l'animació")]
    [SerializeField] private float destroyDelay = 0.2f;

    private bool exploded = false;

    void Awake()
    {
        if (mainCube == null)
            mainCube = transform.Find("mainCube")?.gameObject;
        if (shatterObject == null)
            shatterObject = transform.Find("CyberpunkCube_Shatter_Animation")?.gameObject;

        // L'animació de trencament només es veu quan explota
        if (shatterObject != null)
            shatterObject.SetActive(false);
    }

    // Crida-ho des de la bala o des del raycast de la pistola
    public void Explode()
    {
        if (exploded)
            return;
        exploded = true;

        var physics = GetComponent<CubePhysics>();
        if (physics != null)
            physics.Stop();

        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        if (mainCube != null)
            mainCube.SetActive(false);

        float duration = 1f;
        if (shatterObject != null)
        {
            shatterObject.SetActive(true);
            var anim = shatterObject.GetComponentInChildren<Animation>();
            if (anim != null && anim[shatterClip] != null)
            {
                anim.Play(shatterClip);
                duration = anim[shatterClip].length;
            }
        }

        Destroy(gameObject, duration + destroyDelay);
    }
}
