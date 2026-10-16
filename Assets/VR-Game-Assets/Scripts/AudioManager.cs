using System.Collections;
using UnityEngine;

public enum SfxEvent { WrongWeapon, CubeMissed, Grab, GameOver, Button }

// Un per escena (a l'objecte AudioManager / [MANAGERS]):
// - Música de fons en bucle amb fosa d'entrada i de sortida.
// - Efectes de so 3D amb un grup d'AudioSources propis (configurats per a VR).
// - Sons dels esdeveniments del joc (arma equivocada, cub perdut, agafar arma, final de partida, botó).
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Música")]
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.4f;
    [SerializeField] private float fadeInTime = 2f;

    [Header("Efectes")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [Tooltip("Fins a aquesta distància (m) els efectes se senten a tot volum")]
    [SerializeField] private float sfxMinDistance = 2f;
    [SerializeField] private float sfxMaxDistance = 40f;
    [SerializeField] private int sfxVoices = 16;

    [Header("Sons d'esdeveniments")]
    [SerializeField] private AudioClip wrongWeaponSound;
    [SerializeField] private AudioClip cubeMissedSound;
    [SerializeField] private AudioClip grabSound;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip buttonSound;

    private AudioSource musicSource;
    private AudioSource[] voices;
    private int nextVoice;
    private Coroutine fadeRoutine;

    void Awake()
    {
        Instance = this;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = music;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f; // 2D: la música no depèn d'on mira el jugador
        musicSource.volume = 0f;

        // Veus per als efectes: cadascuna és un objecte fill que es mou on sona l'efecte
        voices = new AudioSource[Mathf.Max(1, sfxVoices)];
        for (int i = 0; i < voices.Length; i++)
        {
            var go = new GameObject("SFX Voice " + i);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = sfxMinDistance;
            source.maxDistance = sfxMaxDistance;
            source.dopplerLevel = 0f;
            voices[i] = source;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (music != null)
        {
            musicSource.Play();
            FadeMusic(musicVolume, fadeInTime);
        }
    }

    // ---------- Música ----------

    public void FadeOutMusic(float duration)
    {
        FadeMusic(0f, duration);
    }

    // Baixa (o puja) la música a una fracció del volum configurat, p. ex. a la pantalla final
    public void FadeMusicTo(float fractionOfVolume, float duration)
    {
        FadeMusic(musicVolume * Mathf.Clamp01(fractionOfVolume), duration);
    }

    private void FadeMusic(float target, float duration)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(target, duration));
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = musicSource.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        musicSource.volume = target;
        fadeRoutine = null;
    }

    // ---------- Efectes ----------

    // Efecte de so en 3D en una posició (funciona encara que no hi hagi AudioManager a l'escena)
    public static void PlaySfx(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null)
            return;
        if (Instance == null)
        {
            AudioSource.PlayClipAtPoint(clip, position, volume);
            return;
        }
        Instance.PlayOnVoice(clip, position, volume, 1f);
    }

    // Efecte de so 2D (se sent igual miri on miri el jugador)
    public static void PlaySfx2D(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;
        if (Instance == null)
        {
            var cam = Camera.main;
            AudioSource.PlayClipAtPoint(clip, cam != null ? cam.transform.position : Vector3.zero, volume);
            return;
        }
        Instance.PlayOnVoice(clip, Instance.transform.position, volume, 0f);
    }

    // Sons dels esdeveniments del joc (els clips es configuren a l'AudioManager de l'escena)
    public static void Play(SfxEvent sfx, Vector3? position = null)
    {
        if (Instance == null)
            return;

        AudioClip clip = sfx switch
        {
            SfxEvent.WrongWeapon => Instance.wrongWeaponSound,
            SfxEvent.CubeMissed => Instance.cubeMissedSound,
            SfxEvent.Grab => Instance.grabSound,
            SfxEvent.GameOver => Instance.gameOverSound,
            SfxEvent.Button => Instance.buttonSound,
            _ => null
        };

        if (position.HasValue)
            PlaySfx(clip, position.Value);
        else
            PlaySfx2D(clip);
    }

    private void PlayOnVoice(AudioClip clip, Vector3 position, float volume, float spatialBlend)
    {
        // Agafa la propera veu lliure (o la més antiga si totes sonen)
        AudioSource voice = null;
        for (int i = 0; i < voices.Length; i++)
        {
            var candidate = voices[(nextVoice + i) % voices.Length];
            if (!candidate.isPlaying)
            {
                voice = candidate;
                nextVoice = (nextVoice + i + 1) % voices.Length;
                break;
            }
        }
        if (voice == null)
        {
            voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
        }

        voice.transform.position = position;
        voice.spatialBlend = spatialBlend;
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(volume * sfxVolume);
        voice.Play();
    }
}
