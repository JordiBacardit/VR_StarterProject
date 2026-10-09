using UnityEngine;

// Ajusta la posició, rotació i escala del model de la pistola respecte la mà, sense tocar la pose d'AutoHand.
// Es pot modificar en Play des de l'Inspector i es veu en directe; després copia els valors (menú ⋮ → Copy Component)
// i enganxa'ls fora de Play (Paste Component Values) o aplica'ls al prefab.
[ExecuteAlways]
public class GunModelOffset : MonoBehaviour
{
    [SerializeField] private Transform model;
    [SerializeField] private Transform aim;

    [Header("Offset del model (espai local de la pistola)")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;
    [Min(0.01f)] public float scale = 1f;

    // Posició original de l'Aim respecte la pistola, per moure'l igual que el model
    [SerializeField, HideInInspector] private bool aimCached;
    [SerializeField, HideInInspector] private Vector3 aimBasePos;
    [SerializeField, HideInInspector] private Quaternion aimBaseRot;

    void OnEnable()
    {
        if (model == null) model = transform.Find("Model");
        if (aim == null) aim = transform.Find("Aim");

        if (aim != null && !aimCached)
        {
            aimBasePos = aim.localPosition;
            aimBaseRot = aim.localRotation;
            aimCached = true;
        }
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        var rot = Quaternion.Euler(rotationOffset);

        if (model != null)
        {
            model.localPosition = positionOffset;
            model.localRotation = rot;
            model.localScale = Vector3.one * scale;
        }

        // L'Aim no és fill del Model (per no moure's amb el retrocés), però ha de seguir el seu offset
        if (aim != null && aimCached)
        {
            aim.localPosition = positionOffset + rot * (aimBasePos * scale);
            aim.localRotation = rot * aimBaseRot;
        }
    }
}
