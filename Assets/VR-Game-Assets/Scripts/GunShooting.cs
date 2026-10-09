using System.Collections;
using Autohand;
using UnityEngine;

// A la pistola (amb un Grabbable d'AutoHand): cada cop que es prem el gatell (squeeze) dispara una bala,
// amb animació de retrocés del model i vibració del controlador.
public class GunShooting : MonoBehaviour
{
    [Header("Dispar")]
    [Tooltip("Temps mínim entre dispars")]
    [SerializeField] private float fireRate = 0.2f;
    [Tooltip("Si es deixa buit es crea una bala simple per codi")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform nozzleTransform;

    [Header("Feedback")]
    [SerializeField] private AudioSource sound_shoot;
    [Tooltip("Es fa servir si no hi ha AudioSource")]
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private Animator gunAnimator;

    [Header("Retrocés")]
    [Tooltip("Part visual que s'anima (fill 'Model')")]
    [SerializeField] private Transform recoilModel;
    [Tooltip("Distància que el model va cap enrere (metres)")]
    [SerializeField] private float recoilKickBack = 0.04f;
    [Tooltip("Graus que puja el canó")]
    [SerializeField] private float recoilKickAngle = 15f;
    [SerializeField] private float recoilDuration = 0.18f;
    [Tooltip("0 = posició normal, 1 = retrocés màxim")]
    [SerializeField] private AnimationCurve recoilCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 20f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));

    [Header("Retrocés de la mà")]
    [Tooltip("Distància que la mà va cap enrere (metres)")]
    [SerializeField] private float handKickBack = 0.06f;
    [Tooltip("Graus que la mà aixeca el canó")]
    [SerializeField] private float handKickAngle = 20f;
    [SerializeField] private float handRecoilDuration = 0.25f;

    [Header("Vibració")]
    [SerializeField, Range(0f, 1f)] private float hapticStrength = 0.8f;
    [SerializeField] private float hapticDuration = 0.08f;

    private Grabbable grabbable;
    private Hand holdingHand;
    private float nextShotTime = 0f;

    private Vector3 modelStartPos;
    private Quaternion modelStartRot;
    private Coroutine recoilRoutine;

    private Vector3 heldBasePos;
    private Vector3 heldBaseRot;
    private Coroutine handRecoilRoutine;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        if (nozzleTransform == null)
            nozzleTransform = transform.Find("Nozzle");
        if (recoilModel == null)
            recoilModel = transform.Find("Model");
        if (sound_shoot == null)
            sound_shoot = GetComponentInChildren<AudioSource>();
        if (gunAnimator == null)
            gunAnimator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        if (grabbable == null)
        {
            Debug.LogWarning($"{name}: falta el component Grabbable d'AutoHand.", this);
            return;
        }
        grabbable.OnGrabEvent += OnGrab;
        grabbable.OnReleaseEvent += OnRelease;
        grabbable.OnSqueezeEvent += OnSqueeze;
    }

    void OnDisable()
    {
        if (grabbable == null)
            return;

        // Si es desactiva a mig retrocés, deixa els offsets com estaven
        if (handRecoilRoutine != null)
        {
            grabbable.heldPositionOffset = heldBasePos;
            grabbable.heldRotationOffset = heldBaseRot;
            handRecoilRoutine = null;
        }

        grabbable.OnGrabEvent -= OnGrab;
        grabbable.OnReleaseEvent -= OnRelease;
        grabbable.OnSqueezeEvent -= OnSqueeze;
    }

    private void OnGrab(Hand hand, Grabbable grab) => holdingHand = hand;
    private void OnRelease(Hand hand, Grabbable grab) => holdingHand = null;

    // Una bala per cada pressió del gatell
    private void OnSqueeze(Hand hand, Grabbable grab)
    {
        if (Time.time < nextShotTime)
            return;
        nextShotTime = Time.time + fireRate;
        Shoot();
    }

    public void Shoot()
    {
        var origin = nozzleTransform != null ? nozzleTransform : transform;

        Bullet bullet;
        if (bulletPrefab != null)
            bullet = Instantiate(bulletPrefab, origin.position, origin.rotation).GetComponent<Bullet>();
        else
            bullet = Bullet.CreateDefault(origin.position, origin.rotation);

        if (bullet != null)
            bullet.owner = transform;

        if (sound_shoot != null)
            sound_shoot.PlayOneShot(shootClip != null ? shootClip : sound_shoot.clip);
        else if (shootClip != null)
            AudioSource.PlayClipAtPoint(shootClip, origin.position);

        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (gunAnimator != null)
            gunAnimator.SetTrigger("Fire");

        PlayRecoil();
        ApplyHandRecoil();
        PlayHaptics();
    }

    private void PlayRecoil()
    {
        if (recoilModel == null || nozzleTransform == null)
            return;

        if (recoilRoutine != null)
            StopCoroutine(recoilRoutine);
        else
        {
            // La posició de repòs es llegeix ara (per si GunModelOffset l'ha canviat)
            modelStartPos = recoilModel.localPosition;
            modelStartRot = recoilModel.localRotation;
        }
        recoilRoutine = StartCoroutine(RecoilAnimation());
    }

    // Mou la mà que aguanta la pistola (enrere i canó amunt) animant els offsets del Grabbable,
    // que AutoHand suma a la posició objectiu de la mà cada frame. Un impuls físic no serviria:
    // HandFollow sobreescriu la velocitat de la mà a cada FixedUpdate.
    private void ApplyHandRecoil()
    {
        if (holdingHand == null || grabbable == null || nozzleTransform == null)
            return;

        if (handRecoilRoutine != null)
            StopCoroutine(handRecoilRoutine);
        else
        {
            heldBasePos = grabbable.heldPositionOffset;
            heldBaseRot = grabbable.heldRotationOffset;
        }
        handRecoilRoutine = StartCoroutine(HandRecoilAnimation(holdingHand));
    }

    private IEnumerator HandRecoilAnimation(Hand hand)
    {
        // Direccions en l'espai local de la mà
        Vector3 backLocal = hand.transform.InverseTransformDirection(-nozzleTransform.forward).normalized;
        Vector3 pitchAxis = hand.transform.InverseTransformDirection(nozzleTransform.right).normalized;
        Quaternion baseRot = Quaternion.Euler(heldBaseRot);

        float t = 0f;
        while (t < handRecoilDuration && holdingHand == hand)
        {
            float k = recoilCurve.Evaluate(t / handRecoilDuration);
            Vector3 p = backLocal * (handKickBack * k);
            Quaternion r = Quaternion.AngleAxis(-handKickAngle * k, pitchAxis);

            // AutoHand fa un mirall dels offsets per a la mà esquerra: ho compensem
            if (hand.left)
            {
                p.x = -p.x;
                r = new Quaternion(r.x, -r.y, -r.z, r.w);
            }

            grabbable.heldPositionOffset = heldBasePos + p;
            grabbable.heldRotationOffset = (baseRot * r).eulerAngles;

            t += Time.deltaTime;
            yield return null;
        }

        grabbable.heldPositionOffset = heldBasePos;
        grabbable.heldRotationOffset = heldBaseRot;
        handRecoilRoutine = null;
    }

    private IEnumerator RecoilAnimation()
    {
        var parent = recoilModel.parent;
        // Direccions en l'espai local del pare: enrere al llarg del canó i eix per aixecar-lo
        Vector3 backDir = parent.InverseTransformDirection(-nozzleTransform.forward).normalized;
        Vector3 pitchAxis = parent.InverseTransformDirection(nozzleTransform.right).normalized;
        // localPosition està en unitats escalades del pare
        float kickLocal = recoilKickBack / Mathf.Max(parent.lossyScale.x, 0.0001f);

        float t = 0f;
        while (t < recoilDuration)
        {
            float k = recoilCurve.Evaluate(t / recoilDuration);
            recoilModel.localPosition = modelStartPos + backDir * (kickLocal * k);
            recoilModel.localRotation = Quaternion.AngleAxis(-recoilKickAngle * k, pitchAxis) * modelStartRot;
            t += Time.deltaTime;
            yield return null;
        }

        recoilModel.localPosition = modelStartPos;
        recoilModel.localRotation = modelStartRot;
        recoilRoutine = null;
    }

    private void PlayHaptics()
    {
        if (holdingHand == null || hapticStrength <= 0f)
            return;
        StartCoroutine(Haptics(holdingHand.left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch));
    }

    private IEnumerator Haptics(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(1f, hapticStrength, controller);
        yield return new WaitForSeconds(hapticDuration);
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }
}
