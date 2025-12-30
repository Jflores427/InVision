using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoundingBoxManager : MonoBehaviour
{
    [Header("Settings")]
    public GameObject boxPrefab; 
    public Transform canvasParent; 

    private List<GameObject> boxPool = new List<GameObject>();

    public void DrawBoxes(List<YoloDetector.Detection> detections, string[] classNames)
    {
        // Ensure we have enough boxes in the pool
        while (boxPool.Count < detections.Count)
        {
            GameObject newBox = Instantiate(boxPrefab, canvasParent);
            boxPool.Add(newBox);
        }

        // Loop through pool
        for (int i = 0; i < boxPool.Count; i++)
        {
            if (i < detections.Count)
            {
                // Active box
                GameObject box = boxPool[i];
                box.SetActive(true);

                // Update position & size
                Rect rect = detections[i].box;
                RectTransform rt = box.GetComponent<RectTransform>();

                // Convert normalized to screen pixels
                float screenW = Screen.width;
                float screenH = Screen.height;

                rt.sizeDelta = new Vector2(rect.width * screenW, rect.height * screenH);
                rt.anchoredPosition = new Vector2(rect.x * screenW, rect.y * screenH);
            }
            else
            {
                // Inactive box
                boxPool[i].SetActive(false);
            }
        }
    }

    public void ClearBoxes()
    {
        foreach (var box in boxPool) box.SetActive(false);
    }
}