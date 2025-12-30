using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class InteractionUI : MonoBehaviour
{
    [Header("UI Components")]
    public CanvasGroup menuCanvasGroup; // This is for the MenuPanel
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public RectTransform spinnerRect; 

    [Header("Settings")]
    public float fadeSpeed = 10f;
    public float spinSpeed = 200f;

    private ARObjectData currentSelectedObject;
    private bool isLoading = false;

    public bool IsMenuOpen { get; private set; } = false;

    void Start()
    {
        // Makes sure the menu is hidden at start
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0;
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        if (spinnerRect != null) spinnerRect.gameObject.SetActive(false);
        if (bodyText) bodyText.text = "";
    }

    void Update()
    {
        // Rotation Animation (Rotate around Z axis)
        if (isLoading && spinnerRect != null)
        {
            spinnerRect.Rotate(0, 0, -spinSpeed * Time.deltaTime);
        }
    }

    public void ShowMenu(ARObjectData data)
    {
        IsMenuOpen = true;

        currentSelectedObject = data;
        titleText.text = $"Selected: {data.objectClass}";
        if (bodyText) bodyText.text = "Select an option below...";

        // Menu Fade-in routine
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(1.0f));
    }

    public void OnRandomFactClicked()
    {
        if (currentSelectedObject == null) return;

        SetLoadingState(true);

        CloudQueryManager.Instance.GetRandomFact(currentSelectedObject.objectClass, (response) =>
        {
            SetLoadingState(false);
            if (bodyText) bodyText.text = response;
        });
    }

    public void OnSpecificsClicked()
    {
        if (currentSelectedObject == null) return;

        SetLoadingState(true);
        CloudQueryManager.Instance.GetSpecifics(currentSelectedObject.snapshot, currentSelectedObject.objectClass, (response) =>
        {
            SetLoadingState(false);
            if (bodyText) bodyText.text = response;
        });
    }

    public void OnCloseClicked()
    {
        IsMenuOpen = false;

        // Fade-out routine
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(0.0f));
    }

    private void SetLoadingState(bool loading)
    {
        isLoading = loading;

        if (spinnerRect != null)
            spinnerRect.gameObject.SetActive(loading);

        if (loading && bodyText)
            bodyText.text = "Thinking...";
    }

    // Smooth Fader
    IEnumerator FadeRoutine(float targetAlpha)
    {
        while (!Mathf.Approximately(menuCanvasGroup.alpha, targetAlpha))
        {
            menuCanvasGroup.alpha = Mathf.MoveTowards(menuCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
            yield return null;
        }

        // Toggle interactivity based on visibility
        bool isVisible = targetAlpha > 0.5f;
        menuCanvasGroup.interactable = isVisible;
        menuCanvasGroup.blocksRaycasts = isVisible;
    }
}