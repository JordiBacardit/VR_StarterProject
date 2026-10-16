using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A [MANAGERS]/ScoreManager de la GameScene. Centralitza la partida i la UI:
// - UI_Timer: compte enrere del temps de partida (text + barra).
// - UI_ScoreCurrent: puntuació actual (suma amb encerts, resta amb errors).
// - UI_ScoreFinal: apareix quan s'acaba el temps, amb la puntuació i el high score (desat amb BestScore).
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Partida")]
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private CubeSpawner cubeSpawner;
    [SerializeField] private string lobbyScene = "LobbyScene";

    [Header("Punts")]
    [Tooltip("Cub destruït amb l'arma correcta")]
    [SerializeField] private int correctPoints = 10;
    [Tooltip("Cub que arriba al jugador sense destruir")]
    [SerializeField] private int missPenalty = 5;
    [Tooltip("Cub tocat amb l'arma equivocada")]
    [SerializeField] private int wrongPenalty = 5;

    [Header("UI_Timer")]
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Image timeBar;

    [Header("UI_ScoreCurrent")]
    [SerializeField] private TMP_Text currentScoreText;

    [Header("UI_ScoreFinal")]
    [SerializeField] private GameObject finalPanel;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private Button backButton;

    public int Score { get; private set; }
    public float TimeLeft { get; private set; }
    public bool IsPlaying { get; private set; }

    void Awake()
    {
        Instance = this;

        // Si falta algun enllaç a l'Inspector, el busca pel nom de l'objecte
        if (timeText == null) timeText = FindByName<TMP_Text>("Text_Time");
        if (timeBar == null) timeBar = FindByName<Image>("PorgressBar_Fill");
        if (currentScoreText == null) currentScoreText = FindByName<TMP_Text>("Text_CurrentScore");
        if (finalPanel == null) finalPanel = FindByName<Transform>("UI_ScoreFinal")?.gameObject;
        if (finalScoreText == null) finalScoreText = FindByName<TMP_Text>("Text_FinalScore");
        if (highScoreText == null) highScoreText = FindByName<TMP_Text>("Text_HighScore");
        if (backButton == null) backButton = FindByName<Button>("BackButton");
        if (cubeSpawner == null) cubeSpawner = FindFirstObjectByType<CubeSpawner>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(BackToLobby);

        StartGame();
    }

    public void StartGame()
    {
        Score = 0;
        TimeLeft = gameDuration;
        IsPlaying = true;

        if (finalPanel != null)
            finalPanel.SetActive(false);
        if (cubeSpawner != null)
            cubeSpawner.StartSpawning();

        UpdateScoreUI();
        UpdateTimerUI();
    }

    void Update()
    {
        if (!IsPlaying)
            return;

        TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
        UpdateTimerUI();

        if (TimeLeft <= 0f)
            EndGame();
    }

    // ---------- Accions del joc ----------

    // Cub destruït amb l'arma correcta (bala → GunCube, espasa → SwordCube)
    public void CubeDestroyed() => AddScore(correctPoints);

    // Cub que ha arribat al CubeKiller
    public void CubeMissed() => AddScore(-missPenalty);

    // Cub tocat amb l'arma equivocada
    public void WrongWeapon() => AddScore(-wrongPenalty);

    public void AddScore(int points)
    {
        if (!IsPlaying)
            return;
        Score = Mathf.Max(0, Score + points);
        UpdateScoreUI();
    }

    // ---------- Final de partida ----------

    private void EndGame()
    {
        IsPlaying = false;

        if (cubeSpawner != null)
            cubeSpawner.StopSpawning();

        // Treu els cubs que encara volen
        foreach (var cube in FindObjectsByType<CubePhysics>(FindObjectsSortMode.None))
            Destroy(cube.gameObject);

        bool newRecord = BestScore.TrySet(Score);

        if (finalScoreText != null)
            finalScoreText.text = Score.ToString();
        if (highScoreText != null)
            highScoreText.text = newRecord ? $"{BestScore.Get()} NEW!" : BestScore.Get().ToString();
        if (finalPanel != null)
            finalPanel.SetActive(true);

        // El botó de tornar al lobby també es pot "clicar" disparant-hi
        MakeShootable(backButton);

        // La música baixa a un volum suau mentre es mostra la puntuació final
        if (AudioManager.Instance != null)
            AudioManager.Instance.FadeMusicTo(0.35f, 2f);
        AudioManager.Play(SfxEvent.GameOver);
    }

    public void BackToLobby()
    {
        if (leavingScene)
            return;
        leavingScene = true;
        AudioManager.Play(SfxEvent.Button);
        StartCoroutine(LoadLobby());
    }

    // Petita fosa de la música abans de canviar d'escena
    private System.Collections.IEnumerator LoadLobby()
    {
        const float fade = 0.5f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.FadeOutMusic(fade);
        yield return new WaitForSeconds(fade);
        SceneManager.LoadScene(lobbyScene);
    }

    private bool leavingScene = false;

    // Afegeix un collider trigger de la mida del botó perquè les bales el puguin tocar
    private static void MakeShootable(Button button)
    {
        if (button == null || button.GetComponent<Collider>() != null)
            return;

        var rect = button.GetComponent<RectTransform>();
        var box = button.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        if (rect != null)
        {
            box.center = rect.rect.center;
            box.size = new Vector3(rect.rect.width, rect.rect.height, Mathf.Max(rect.rect.width, rect.rect.height) * 0.2f);
        }
    }

    // ---------- UI ----------

    private void UpdateScoreUI()
    {
        if (currentScoreText != null)
            currentScoreText.text = Score.ToString();
    }

    private void UpdateTimerUI()
    {
        if (timeText != null)
        {
            int seconds = Mathf.CeilToInt(TimeLeft);
            timeText.text = $"{seconds / 60}:{seconds % 60:00}";
        }
        if (timeBar != null)
            timeBar.fillAmount = gameDuration > 0f ? TimeLeft / gameDuration : 0f;
    }

    private static T FindByName<T>(string objectName) where T : Component
    {
        foreach (var c in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.name == objectName)
                return c;
        return null;
    }
}
