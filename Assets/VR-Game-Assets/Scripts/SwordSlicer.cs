using Autohand;
using UnityEngine;

// A l'espasa (amb un Grabbable d'AutoHand).
// - Prepara els colliders: la fulla és trigger (talla però no s'agafa) i el mànec té un collider en forma de càpsula.
// - Abans que la mà l'agafi, la col·loca al palmell amb la fulla sortint pel costat del polze (HandAttacher crida AlignToHand).
// - Calcula la velocitat de la punta perquè els cubs només es tallin amb un cop prou ràpid.
// S'executa abans que el Grabbable (-100) perquè aquest ja trobi els colliders correctes.
[DefaultExecutionOrder(-200)]
public class SwordSlicer : MonoBehaviour, IHandAligner
{
    [SerializeField] private Collider blade;

    [Header("Agafada")]
    [Tooltip("On s'agafa el mànec: 0 = pom (final), 1 = guarda (tocant la fulla)")]
    [SerializeField, Range(0f, 1f)] private float gripPosition = 0.5f;
    [Tooltip("Inclinació de la fulla cap endavant (graus). El mànec creua el palmell en diagonal, com en una agafada real")]
    [SerializeField, Range(-90f, 90f)] private float gripTilt = 45f;
    [Tooltip("Gir extra de la fulla al voltant del mànec (graus)")]
    [SerializeField] private float gripRoll = 0f;
    [Tooltip("Inverteix la direcció de la fulla si surt pel costat del dit petit")]
    [SerializeField] private bool flipBlade = false;
    [Tooltip("Radi del mànec en metres (0 = automàtic a partir de la malla). Com més petit, més tancat queda el puny")]
    [SerializeField] private float handleRadiusOverride = 0f;

    [Header("Puny (es pot ajustar en Play i es veu en directe)")]
    [Tooltip("Quant es dobleguen els dits al voltant del mànec (0 = oberts, 1 = puny tancat)")]
    [SerializeField, Range(0f, 1f)] private float fingerBend = 0.8f;
    [SerializeField, Range(0f, 1f)] private float thumbBend = 0.65f;

    [Header("Vibració en tallar")]
    [SerializeField, Range(0f, 1f)] private float hapticStrength = 1f;
    [SerializeField] private float hapticDuration = 0.1f;

    // Velocitat (m/s) de la punta de la fulla
    public float Speed { get; private set; }

    private Grabbable grabbable;
    private Hand holdingHand;
    private Vector3 lastTipPos;

    // Geometria en l'espai local de l'espasa
    private Vector3 bladeAxisLocal;   // del mànec cap a la punta
    private Vector3 bladeWidthLocal;  // direcció del tall (amplada de la fulla)
    private Vector3 bladeThicknessLocal = Vector3.forward; // normal de la cara plana de la fulla
    private Vector3 gripLocal;        // punt on va el palmell
    private float handleRadius = 0.02f;
    private bool geometryReady = false;

    void Awake()
    {
        if (blade == null)
        {
            var bladeTransform = transform.Find("Blade");
            if (bladeTransform != null)
                blade = bladeTransform.GetComponent<Collider>();
        }

        // La fulla és trigger: no empeny res, detecta els cubs i AutoHand no la fa servir per agafar
        if (blade != null)
            blade.isTrigger = true;

        ComputeGeometry();
        grabbable = GetComponent<Grabbable>();
        lastTipPos = TipPosition();
    }

    private void ComputeGeometry()
    {
        var handleMesh = GetComponent<MeshFilter>();
        var bladeMesh = blade != null ? blade.GetComponent<MeshFilter>() : null;
        if (handleMesh == null || handleMesh.sharedMesh == null || bladeMesh == null || bladeMesh.sharedMesh == null)
            return;

        Bounds handle = handleMesh.sharedMesh.bounds;
        Bounds bladeBounds = LocalBounds(bladeMesh);

        // Eix principal: el de la fulla (de més llargada), orientat del mànec cap a la punta
        int axis = LargestAxis(bladeBounds.size);
        bladeAxisLocal = Vector3.zero;
        bladeAxisLocal[axis] = Mathf.Sign(bladeBounds.center[axis] - handle.center[axis]);
        if (bladeAxisLocal[axis] == 0f)
            bladeAxisLocal[axis] = 1f;

        // Amplada de la fulla: el més gran dels altres dos eixos
        int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
        int widthAxis = bladeBounds.size[a1] >= bladeBounds.size[a2] ? a1 : a2;
        int thickAxis = widthAxis == a1 ? a2 : a1;
        bladeWidthLocal = Vector3.zero;
        bladeWidthLocal[widthAxis] = 1f;
        bladeThicknessLocal = Vector3.zero;
        bladeThicknessLocal[thickAxis] = 1f;

        // Punt d'agafada al llarg del mànec (de pom a guarda)
        float dir = bladeAxisLocal[axis];
        float pommel = dir > 0 ? handle.min[axis] : handle.max[axis];
        float guard = dir > 0 ? handle.max[axis] : handle.min[axis];
        gripLocal = handle.center;
        gripLocal[axis] = Mathf.Lerp(pommel, guard, gripPosition);

        // Collider del mànec: càpsula al llarg de l'eix (el gruix més petit, perquè la guarda no engreixi el mànec)
        float radius = handleRadiusOverride > 0f
            ? handleRadiusOverride / Mathf.Max(transform.lossyScale.x, 0.0001f)
            : Mathf.Clamp(Mathf.Min(handle.size[widthAxis], handle.size[thickAxis]) * 0.5f, 0.01f, 0.03f);
        if (GetComponent<Collider>() == null)
        {
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = handle.center;
            capsule.height = handle.size[axis];
            capsule.radius = radius;
        }
        handleRadius = radius * transform.lossyScale.x;
        geometryReady = true;
    }

    // Bounds de la malla de la fulla expressats en l'espai local de l'espasa
    private Bounds LocalBounds(MeshFilter mf)
    {
        Bounds b = mf.sharedMesh.bounds;
        var result = new Bounds(transform.InverseTransformPoint(mf.transform.TransformPoint(b.center)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            result.Encapsulate(transform.InverseTransformPoint(mf.transform.TransformPoint(corner)));
        }
        return result;
    }

    private static int LargestAxis(Vector3 v)
    {
        if (v.x >= v.y && v.x >= v.z) return 0;
        return v.y >= v.z ? 1 : 2;
    }

    // Col·loca l'espasa al palmell: fulla pel costat del polze, tall en la direcció dels dits
    public void AlignToHand(Hand hand)
    {
        if (!geometryReady || hand == null || hand.palmTransform == null)
            return;

        Transform palm = hand.palmTransform;
        Vector3 palmNormal = palm.forward;

        Vector3 fingersDir = palm.up;
        Vector3 thumbDir = hand.left ? palm.right : -palm.right;
        if (hand.fingers != null)
        {
            foreach (var f in hand.fingers)
            {
                if (f == null) continue;
                Vector3 d = Vector3.ProjectOnPlane(f.transform.position - palm.position, palmNormal);
                if (f.fingerType == FingerEnum.middle && d.sqrMagnitude > 0f) fingersDir = d.normalized;
                if (f.fingerType == FingerEnum.thumb && d.sqrMagnitude > 0f) thumbDir = d.normalized;
            }
        }

        // Eix del mànec dins del puny: perpendicular al palmell i als dits, cap al costat del polze
        Vector3 handleAxis = Vector3.Cross(palmNormal, fingersDir).normalized;
        if (Vector3.Dot(handleAxis, thumbDir) < 0f)
            handleAxis = -handleAxis;
        if (flipBlade)
            handleAxis = -handleAxis;

        // Inclina la fulla cap a la direcció dels dits (endavant), girant dins del pla del palmell
        Quaternion tilt = Quaternion.AngleAxis(gripTilt, Vector3.Cross(handleAxis, fingersDir).normalized);
        handleAxis = tilt * handleAxis;
        Vector3 edgeBase = tilt * fingersDir;

        Vector3 edgeDir = Quaternion.AngleAxis(gripRoll, handleAxis) * edgeBase;

        Quaternion target = Quaternion.LookRotation(handleAxis, edgeDir);
        Quaternion local = Quaternion.LookRotation(bladeAxisLocal, bladeWidthLocal);
        transform.rotation = target * Quaternion.Inverse(local);

        // Que la mà no xoqui amb l'espasa mentre la col·loquem (si no, la física la pot expulsar)
        if (grabbable != null)
            grabbable.IgnoreHand(hand, true);

        // El mànec queda una mica separat del palmell: el raig d'AutoHand ha de sortir de fora del collider
        // per trobar-lo, i després l'auto-pose ja apropa la mà fins a tocar-lo
        Vector3 gripWorldTarget = palm.position + palmNormal * (handleRadius + 0.02f);
        transform.position += gripWorldTarget - transform.TransformPoint(gripLocal);

        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = transform.position;
            body.rotation = transform.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    void OnEnable()
    {
        if (grabbable == null)
            return;
        grabbable.OnGrabEvent += OnGrab;
        grabbable.OnReleaseEvent += OnRelease;
    }

    void OnDisable()
    {
        if (grabbable == null)
            return;
        grabbable.OnGrabEvent -= OnGrab;
        grabbable.OnReleaseEvent -= OnRelease;
    }

    private void OnGrab(Hand hand, Grabbable grab)
    {
        holdingHand = hand;
        StartCoroutine(CloseFistNextFrame(hand));
    }

    private void OnRelease(Hand hand, Grabbable grab) => holdingHand = null;

    // L'auto-pose d'AutoHand atura els dits quan toquen el mànec i amb un mànec prim queden mig oberts.
    // Un cop acabat l'agafament, tanquem el puny i ho guardem com a pose objectiu perquè es mantingui.
    private System.Collections.IEnumerator CloseFistNextFrame(Hand hand)
    {
        yield return null;
        ApplyFist(hand);
    }

    private void ApplyFist(Hand hand)
    {
        if (hand == null || hand.holdingObj != grabbable || hand.fingers == null)
            return;

        foreach (var finger in hand.fingers)
        {
            if (finger == null) continue;
            finger.SetFingerBend(finger.fingerType == FingerEnum.thumb ? thumbBend : fingerBend);
        }

        hand.handAnimator.targetGrabPose.SavePose(hand, transform);
        hand.handAnimator.SetPose(ref hand.handAnimator.targetGrabPose, 0f);
    }

    // Permet ajustar l'agafada des de l'Inspector mentre es juga: torna a col·locar l'espasa a la mà
    void OnValidate()
    {
        if (!Application.isPlaying || holdingHand == null)
            return;

        var attacher = GetComponent<HandAttacher>();
        if (attacher != null)
            attacher.SetHand(holdingHand.left ? HandSide.Left : HandSide.Right);
        else
            ApplyFist(holdingHand);
    }

    void Update()
    {
        Vector3 tip = TipPosition();
        if (Time.deltaTime > 0f)
        {
            TipVelocity = (tip - lastTipPos) / Time.deltaTime;
            Speed = TipVelocity.magnitude;
        }
        lastTipPos = tip;
    }

    // Velocitat (vector) de la punta de la fulla
    public Vector3 TipVelocity { get; private set; }

    // Direcció de la fulla, del mànec cap a la punta (món)
    public Vector3 BladeDirection => transform.TransformDirection(bladeAxisLocal).normalized;

    /// <summary>
    /// Pla de tall: conté la fulla i la direcció del cop, així que és exactament per on passa l'espasa.
    /// Si el cop és massa lent per saber-ne la direcció, fa servir la cara plana de la fulla.
    /// </summary>
    public Plane GetCutPlane(Vector3 nearPoint)
    {
        Vector3 bladeDir = BladeDirection;
        Vector3 normal = Vector3.Cross(bladeDir, TipVelocity);
        if (normal.sqrMagnitude < 1e-4f)
            normal = transform.TransformDirection(bladeThicknessLocal);
        normal.Normalize();

        // Punt de la fulla més proper al cub
        Vector3 point = blade != null ? blade.ClosestPoint(nearPoint) : transform.position;
        return new Plane(normal, point);
    }

    // Punt de la fulla més allunyat del mànec
    private Vector3 TipPosition()
    {
        if (blade == null)
            return transform.position;
        Vector3 center = blade.bounds.center;
        Vector3 dir = center - transform.position;
        return dir.sqrMagnitude > 0f ? blade.ClosestPointOnBounds(center + dir * 10f) : center;
    }

    // El crida CubeSlice quan talla un cub
    public void OnSliced()
    {
        if (holdingHand == null || hapticStrength <= 0f)
            return;
        stopController = holdingHand.left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        OVRInput.SetControllerVibration(1f, hapticStrength, stopController);
        CancelInvoke(nameof(StopHaptics));
        Invoke(nameof(StopHaptics), hapticDuration);
    }

    private OVRInput.Controller stopController;

    private void StopHaptics()
    {
        OVRInput.SetControllerVibration(0f, 0f, stopController);
    }
}
