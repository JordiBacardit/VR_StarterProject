using Autohand;
using UnityEngine;

// Configura la càmera de tots els Canvas World Space de l'escena (també els desactivats, com UI_ScoreFinal).
// Fa servir la càmera de UI d'AutoHand, que és la que fan servir els punters de les mans per clicar botons;
// si no hi ha punters, fa servir la càmera del jugador (Camera.main).
public class CanvasCameraSetup : MonoBehaviour
{
    void Start()
    {
        Camera cam = FindFirstObjectByType<HandCanvasPointer>(FindObjectsInactive.Include) != null
            ? HandCanvasPointer.UICamera
            : Camera.main;

        if (cam == null)
        {
            Debug.LogWarning($"{name}: no s'ha trobat cap càmera per als Canvas.", this);
            return;
        }

        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (canvas.renderMode == RenderMode.WorldSpace)
                canvas.worldCamera = cam;
    }
}
