using System.Collections;
using Autohand;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Connecta StartCountdown() a l'event OnPressed del PhysicsGadgetButton (AutoHand).
public class PressButtonChangeScene : MonoBehaviour
{
    [Header("Escena")]
    [SerializeField] private string sceneToLoad = "GameScene";
    [SerializeField, Min(0f)] private float countdownSeconds = 3f;

    [Header("So")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickSound;
    [Tooltip("So a cada segon del compte enrere")]
    [SerializeField] private AudioClip tickSound;

    [Header("UI (opcional)")]
    [SerializeField] private TMP_Text counterText;

    private bool countdownStarted = false;

    void Start()
    {
        // La UI és visible des del principi; el comptador mostra els segons inicials
        if (counterText != null)
            counterText.text = Mathf.CeilToInt(countdownSeconds).ToString();

        // Si hi ha un PhysicsGadgetButton en aquest objecte (o un fill), s'hi subscriu automàticament
        var button = GetComponentInChildren<PhysicsGadgetButton>();
        if (button != null)
            button.OnPressed.AddListener(StartCountdown);
        else
            Debug.LogWarning($"{name}: no s'ha trobat cap PhysicsGadgetButton, StartCountdown() s'ha de cridar manualment.", this);
    }

    public void StartCountdown()
    {
        // Evita reiniciar el compte enrere si el botó es prem més d'un cop
        if (countdownStarted)
            return;
        countdownStarted = true;

        if (audioSource != null)
        {
            if (clickSound != null)
                audioSource.PlayOneShot(clickSound);
            else
                audioSource.Play();
        }

        StartCoroutine(CountdownAndLoad());
    }

    private IEnumerator CountdownAndLoad()
    {
        // La música del lobby s'apaga durant el compte enrere
        if (AudioManager.Instance != null)
            AudioManager.Instance.FadeOutMusic(countdownSeconds);

        for (int i = Mathf.CeilToInt(countdownSeconds); i > 0; i--)
        {
            if (counterText != null)
                counterText.text = i.ToString();
            if (tickSound != null && audioSource != null)
                audioSource.PlayOneShot(tickSound);
            yield return new WaitForSeconds(1f);
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
