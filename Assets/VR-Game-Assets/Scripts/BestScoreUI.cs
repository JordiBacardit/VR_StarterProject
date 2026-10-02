using TMPro;
using UnityEngine;

// Mostra el best score guardat en un text TMP (per a la LobbyScene).
public class BestScoreUI : MonoBehaviour
{
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private string format = "BEST SCORE: {0}";

    void Awake()
    {
        if (bestScoreText == null)
            bestScoreText = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        if (bestScoreText != null)
            bestScoreText.text = string.Format(format, BestScore.Get());
    }
}
