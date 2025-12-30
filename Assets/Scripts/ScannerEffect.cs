using UnityEngine;
using UnityEngine.UI;

public class ScannerEffect : MonoBehaviour
{
    [Header("References")]
    public RectTransform scanLine;
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public float speed = 500f;      // Pixels per second
    public float fadeSpeed = 5f;    // How fast it appears/disappears

    private float screenHeight;
    private bool isScanning = false;

    void Start()
    {
        screenHeight = Screen.height;
        if (canvasGroup != null) canvasGroup.alpha = 0;
    }

    public void StartScanning()
    {
        isScanning = true;
    }

    public void StopScanning()
    {
        isScanning = false;
    }

    void Update()
    {
        // Handle Fading
        float targetAlpha = isScanning ? 1.0f : 0.0f;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
        }

        // Handle Movement, when it is visible
        if (canvasGroup.alpha > 0.01f)
        {
            // Use PingPong, which creates a value that bounces back and forth between 0 and Length
            // Subtract half height to make it go from Bottom (-H/2) to Top (+H/2)
            float yPos = Mathf.PingPong(Time.time * speed, screenHeight) - (screenHeight / 2.0f);

            scanLine.anchoredPosition = new Vector2(0, yPos);
        }
    }
}