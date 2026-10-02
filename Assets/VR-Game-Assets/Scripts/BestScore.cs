using UnityEngine;

// Guarda el millor resultat amb PlayerPrefs perquè es mantingui entre escenes i partides.
public static class BestScore
{
    private const string Key = "BestScore";

    public static int Get()
    {
        return PlayerPrefs.GetInt(Key, 0);
    }

    // Crida-ho al final de la partida (GameScene). Retorna true si és un nou rècord.
    public static bool TrySet(int score)
    {
        if (score <= Get())
            return false;

        PlayerPrefs.SetInt(Key, score);
        PlayerPrefs.Save();
        return true;
    }
}
