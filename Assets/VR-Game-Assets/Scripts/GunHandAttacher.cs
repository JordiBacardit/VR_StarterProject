using System.Collections;
using Autohand;
using UnityEngine;

public enum HandSide { Left, Right }

// Fa que la pistola comenci agafada a la mà escollida i hi torni sola si es deixa anar.
// Es pot canviar de mà en temps d'execució amb SetHand() / SwapHand().
[RequireComponent(typeof(Grabbable))]
public class GunHandAttacher : MonoBehaviour
{
    [SerializeField] private HandSide hand = HandSide.Right;
    [Tooltip("Si està desactivat, la pistola queda a l'escena i el jugador l'ha d'agafar")]
    [SerializeField] private bool grabOnStart = true;

    [Header("Tornar a la mà")]
    [Tooltip("Si es deixa anar la pistola, torna a la mà que la tenia")]
    [SerializeField] private bool returnOnRelease = true;
    [SerializeField] private float returnDelay = 1f;

    public HandSide CurrentHand => hand;

    private Grabbable grabbable;
    private Coroutine returnRoutine;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
    }

    void OnEnable()
    {
        grabbable.OnGrabEvent += OnGrabbed;
        grabbable.OnReleaseEvent += OnReleased;
    }

    void OnDisable()
    {
        grabbable.OnGrabEvent -= OnGrabbed;
        grabbable.OnReleaseEvent -= OnReleased;
    }

    IEnumerator Start()
    {
        // Esperem un frame perquè les mans d'AutoHand estiguin inicialitzades
        yield return null;
        if (grabOnStart)
            SetHand(hand);
    }

    private void OnGrabbed(Hand h, Grabbable g)
    {
        // Recorda quina mà la té (també si el jugador la canvia de mà ell mateix)
        hand = h.left ? HandSide.Left : HandSide.Right;
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }
    }

    private void OnReleased(Hand h, Grabbable g)
    {
        if (returnOnRelease && isActiveAndEnabled)
            returnRoutine = StartCoroutine(ReturnToHand());
    }

    private IEnumerator ReturnToHand()
    {
        yield return new WaitForSeconds(returnDelay);
        returnRoutine = null;
        if (!grabbable.IsHeld())
            SetHand(hand);
    }

    public void SetHand(HandSide side)
    {
        var target = FindHand(side);
        if (target == null)
        {
            Debug.LogWarning($"{name}: no s'ha trobat la mà {side} d'AutoHand.", this);
            return;
        }

        // Deixa anar la pistola de la mà que la tingui ara (forçat, per si té GrabLock)
        if (grabbable.IsHeld())
            grabbable.ForceHandsRelease();

        // Si la mà destí té una altra cosa agafada, també la deixa anar
        if (target.holdingObj != null)
            target.holdingObj.ForceHandRelease(target);

        hand = side;
        target.ForceGrab(grabbable);
    }

    public void SwapHand()
    {
        SetHand(hand == HandSide.Right ? HandSide.Left : HandSide.Right);
    }

    // Les mans reals del jugador (n'hi ha d'altres amb component Hand, com les "Grip Projection", que no serveixen)
    private static Hand FindHand(HandSide side)
    {
        var player = AutoHandPlayer.Instance;
        if (player == null)
            return null;
        return side == HandSide.Left ? player.handLeft : player.handRight;
    }
}
