using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;


public class ARAutomation : MonoBehaviour
{

    [Header("Prefabs")]
    public GameObject arLabelPrefab;
    public GameObject holocubePrefab;

    [Header("Dependencies")]
    public YoloDetector detector;
    public ARRaycastManager raycastManager;
    public ARAnchorManager anchorManager;
    public BoundingBoxManager boxManager;
    public InteractionUI interactionUI;

    [Header("Settings")]
    public float scanDuration = 3.0f;
    [Range(0.1f, 1.0f)] public float focusRadius = 0.2f;
    [Range(0.1f, 1.0f)] public float boxSizeMultiplier = 0.1f; // Adjusts the size of the Holocube

    [Header("UI Positioning")]
    public Vector3 labelOffset = new Vector3(0, 0.2f, 0);

    [Header("UI References")]
    public ScannerEffect scannerEffect; 

    private GameObject currentARLabel;
    private GameObject currentCube;
    private List<GameObject> allAnchoredObjects = new List<GameObject>();

    private bool isScanning = false;
    private bool isProcessingFrame = false;
    private bool hasPlacedOnSurface = false;
    private RenderTexture gpuScreenCopy;

    // COCO Class Names 
    private readonly string[] classNames = new string[] { "Person", "Bicycle", "Car", "Motorcycle", "Airplane", "Bus", "Train", "Truck", "Boat", 
        "Traffic Light", "Fire Hydrant", "Stop Sign", "Parking Meter", "Bench", "Bird", "Cat", "Dog", "Horse", "Sheep", "Cow", "Elephant", "Bear", 
        "Zebra", "Giraffe", "Backpack", "Umbrella", "Handbag", "Tie", "Suitcase", "Frisbee", "Skis", "Snowboard", "Sports Ball", "Kite", "Baseball Bat", 
        "Baseball Glove", "Skateboard", "Surfboard", "Tennis Racket", "Bottle", "Wine Glass", "Cup", "Fork", "Knife", "Spoon", "Bowl", "Banana", "Apple", 
        "Sandwich", "Orange", "Broccoli", "Carrot", "Hot Dog", "Pizza", "Donut", "Cake", "Chair", "Couch", "Potted Plant", "Bed", "Dining Table", "Toilet", 
        "TV", "Laptop", "Mouse", "Remote", "Keyboard", "Cell Phone", "Microwave", "Oven", "Toaster", "Sink", "Refrigerator", "Book", "Clock", "Vase", "Scissors", 
        "Teddy Bear", "Hair Drier", "Toothbrush" 
    };

    void Start()
    {
        gpuScreenCopy = new RenderTexture(Screen.width, Screen.height, 0);
    }

    void Update()
    {
        if (interactionUI.IsMenuOpen || IsPointerOverUI()) return;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                if (touch.tapCount == 2 && !isScanning) StartCoroutine(ScanRoutine());
                else if (touch.tapCount == 1 && !isScanning) TrySelectObject(touch.position);
            }
        }
    }

    // Checks if the finger/pointer is over a UI element via Raycasting
    private bool IsPointerOverUI()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    // Clears all existing anchors from the world space
    // Cleans up other components
    public void ClearAllAnchors()
    {
        foreach (GameObject obj in allAnchoredObjects) if (obj != null) Destroy(obj);
        allAnchoredObjects.Clear();
        if (currentARLabel != null) Destroy(currentARLabel);
        if (currentCube != null) Destroy(currentCube);
        boxManager.ClearBoxes(); 
    }

    // Opens InteractionUI menu from selecting an object via Raycasting
    void TrySelectObject(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            ARObjectData data = hit.collider.GetComponent<ARObjectData>();
            if (data != null) interactionUI.ShowMenu(data);
        }
    }

    // This is the main scanning routine that provides information on the object being detected
    IEnumerator ScanRoutine()
    {
        isScanning = true;
        hasPlacedOnSurface = false;
        currentARLabel = null;
        currentCube = null;

        if (scannerEffect != null) scannerEffect.StartScanning();

        float timer = scanDuration;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            if (!isProcessingFrame)
            {
                yield return StartCoroutine(ProcessSingleFrame());
            }
            else yield return null;
        }

        isScanning = false;
        boxManager.ClearBoxes();

        if (scannerEffect != null) scannerEffect.StopScanning();

        AttachAnchorsToCurrent();
    }

    // For each frame, we grab the best central detection, draw its corresponding bounding box and insert/adjusts its Holo Cube into the world
    IEnumerator ProcessSingleFrame()
    {
        isProcessingFrame = true;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(gpuScreenCopy);

        StartCoroutine(detector.RunInferenceAsync(gpuScreenCopy, (results) =>
        {
            // If the scan timer finished while YOLO is detecting, discard this result immediately.
            // This prevents boxes from appearing after ClearBoxes is called.
            if (!isScanning)
            {
                isProcessingFrame = false;
                return;
            }

            YoloDetector.Detection best = GetBestCentralDetection(results);

            List<YoloDetector.Detection> focusedList = new List<YoloDetector.Detection>();
            
            if (best.classId != -1) focusedList.Add(best);
            
            boxManager.DrawBoxes(focusedList, classNames);

            if (best.classId != -1)
            {
                string objectName = (best.classId >= 0 && best.classId < classNames.Length) ? classNames[best.classId] : "Unknown";

                Vector2 screenCenter = new Vector2(
                    (best.box.x + best.box.width / 2) * Screen.width,
                    (best.box.y + best.box.height / 2) * Screen.height
                );

                Place3DContent(screenCenter, best.box, objectName, best.confidence);
            }
            isProcessingFrame = false;
        }));
    }

    private YoloDetector.Detection GetBestCentralDetection(List<YoloDetector.Detection> detections)
    {
        YoloDetector.Detection best = new YoloDetector.Detection { classId = -1 };
        float closestDistance = float.MaxValue;
        Vector2 screenCenter = new Vector2(0.5f, 0.5f);
        foreach (var det in detections)
        {
            Vector2 boxCenter = new Vector2(det.box.x + (det.box.width / 2), det.box.y + (det.box.height / 2));
            float dist = Vector2.Distance(boxCenter, screenCenter);
            if (dist < focusRadius && dist < closestDistance) { closestDistance = dist; best = det; }
        }
        return best;
    }

    // Insert the Holocube with its label into the world space with respect to a found surface/plane
    void Place3DContent(Vector2 screenCenter, Rect pixelBox, string text, float confidence)
    {
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        if (raycastManager.Raycast(screenCenter, hits, TrackableType.AllTypes))
        {
            Pose hitPose = hits[0].pose;
            Vector3 worldSize = CalculateWorldSize(pixelBox, Vector3.Distance(Camera.main.transform.position, hitPose.position));
            Vector3 objectCenter = hitPose.position + (Vector3.up * (worldSize.y / 2.0f));
            Vector3 labelPos = objectCenter + (Vector3.up * (worldSize.y / 2.0f)) + labelOffset;

            UpdateCurrentObjects(objectCenter, labelPos, text, confidence, worldSize);
            hasPlacedOnSurface = true;
        }
        else if (!hasPlacedOnSurface)
        {
            float dist = 0.8f;
            Vector3 pos = Camera.main.transform.position + Camera.main.transform.forward * dist;
            Vector3 worldSize = CalculateWorldSize(pixelBox, dist);
            UpdateCurrentObjects(pos, pos + labelOffset, text, confidence, worldSize);
        }
    }

    void UpdateCurrentObjects(Vector3 objPos, Vector3 labelPos, string text, float confidence, Vector3 size)
    {
        float lerpInterpolation = 0.2f;

        // Update Label Visuals
        string labelText = $"{text}\n<size=70%>{confidence:P0}</size>";
        if (currentARLabel == null) currentARLabel = Instantiate(arLabelPrefab, labelPos, Quaternion.identity);
        else currentARLabel.transform.position = Vector3.Lerp(currentARLabel.transform.position, labelPos, lerpInterpolation);

        ARLabel labelScript = currentARLabel.GetComponent<ARLabel>();
        if (labelScript != null) labelScript.SetText(labelText);

        // Update Holocube Visuals
        if (currentCube == null) currentCube = Instantiate(holocubePrefab, objPos, Quaternion.identity);
        else currentCube.transform.position = Vector3.Lerp(currentCube.transform.position, objPos, lerpInterpolation);

        currentCube.transform.localScale = Vector3.Lerp(currentCube.transform.localScale, size, lerpInterpolation);
        currentCube.transform.rotation = Quaternion.identity;

        // Ensures the data inside the box (ARObjectData) matches the visual label continuously.
        // This prevents the mismatch bug where the box says one label but the data says a different one.
        ARObjectData data = currentCube.GetComponent<ARObjectData>();
        if (data != null)
        {
            data.objectClass = text;
            data.confidence = confidence;
        }
    }

    // Attach the last Holocube to the list of all anchored objects and update the data of that same Holocube
    void AttachAnchorsToCurrent()
    {
        if (currentCube != null)
        {
            allAnchoredObjects.Add(currentCube);
            if (currentCube.GetComponent<ARAnchor>() == null) currentCube.AddComponent<ARAnchor>();

            // Snapshot logic for Cloud API
            ARObjectData data = currentCube.GetComponent<ARObjectData>();
            if (data != null)
            {
                // Save Snapshot (High cost)
                Texture2D snapshot = new Texture2D(gpuScreenCopy.width, gpuScreenCopy.height, TextureFormat.RGB24, false);
                RenderTexture.active = gpuScreenCopy;
                snapshot.ReadPixels(new Rect(0, 0, gpuScreenCopy.width, gpuScreenCopy.height), 0, 0);
                snapshot.Apply();
                RenderTexture.active = null;
                data.snapshot = snapshot;
            }
        }

        if (currentARLabel != null)
        {
            allAnchoredObjects.Add(currentARLabel);
            if (currentARLabel.GetComponent<ARAnchor>() == null) currentARLabel.AddComponent<ARAnchor>();
        }
    }

    // Compute the world size to adjust the size of the Holocube
    Vector3 CalculateWorldSize(Rect normalizedBox, float distance)
    {
        float frustumHeight = 2.0f * distance * Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * Camera.main.aspect;
        float w = frustumWidth * normalizedBox.width * boxSizeMultiplier;
        float h = frustumHeight * normalizedBox.height * boxSizeMultiplier;
        float d = Mathf.Min(w, h);
        return new Vector3(w, h, d);
    }

    void OnDestroy()
    {
        if (gpuScreenCopy != null) gpuScreenCopy.Release();
    }
}