using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;

public class YoloDetector : MonoBehaviour
{
    [Header("Model Settings")]
    public ModelAsset modelAsset;

    // Yolo Model takes in a 640x640 image size
    const int IMAGE_SIZE = 640;

    [Header("Thresholds")]
    [Range(0f, 1f)] public float confidenceThreshold = 0.2f;
    [Range(0f, 1f)] public float iouThreshold = 0.4f;

    private Worker worker;
    private Tensor<float> inputTensor;
    private RenderTexture targetTexture;

    // Data Struct for Yolo Detection Results
    public struct Detection
    {
        public int classId;
        public float confidence;
        public Rect box;
    }

    // Setup for Yolo Detection Inferencing
    void Start()
    {
        targetTexture = new RenderTexture(IMAGE_SIZE, IMAGE_SIZE, 0);
        Model runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
        inputTensor = new Tensor<float>(new TensorShape(1, 3, IMAGE_SIZE, IMAGE_SIZE));
    }

    public IEnumerator RunInferenceAsync(Texture sourceTexture, Action<List<Detection>> onComplete)
    {
        if (worker == null) yield break;

        // Generate a Blit Image from the sourceTexture
        Graphics.Blit(sourceTexture, targetTexture);

        // Convert to Tensor
        TextureConverter.ToTensor(targetTexture, inputTensor, new TextureTransform());

        // Schedule Execution on GPU
        worker.Schedule(inputTensor);

        // Manually waits for the GPU for 2 frames
        // Instead of a complex/buggy ReadbackRequest, this allows the program loop to run for 2 frames
        // This gives the GPU time to finish the calculation
        yield return null;
        yield return null;

        // Get Output as Tensor
        var outputTensor = worker.PeekOutput() as Tensor<float>;

        // Convert the output tensor into an array, and because we waited 2 frames above,
        // the GPU is likely done
        float[] outputArray = outputTensor.DownloadToArray();

        // Parse the Yolo Output
        List<Detection> results = ParseYoloOutput(outputArray);

        onComplete?.Invoke(results);
    }


    // Parses the Yolo Output by going through all 8400 of its anchors, finding the best class for the detected object,
    // and adding that detection to a list when it exceeds the confidence threshold
    private List<Detection> ParseYoloOutput(float[] data)
    {
        List<Detection> proposals = new List<Detection>();
        int numClasses = 80;
        int numAnchors = 8400;

        for (int i = 0; i < numAnchors; i++)
        {
            float maxScore = 0;
            int bestClass = -1;
            for (int c = 0; c < numClasses; c++)
            {
                float score = data[(4 + c) * numAnchors + i];
                if (score > maxScore)
                {
                    maxScore = score;
                    bestClass = c;
                }
            }

            if (maxScore > confidenceThreshold)
            {
                float x = data[0 * numAnchors + i];
                float y = data[1 * numAnchors + i];
                float w = data[2 * numAnchors + i];
                float h = data[3 * numAnchors + i];

                float xMin = (x - (w / 2)) / IMAGE_SIZE;
                float yMin = (y - (h / 2)) / IMAGE_SIZE;
                float width = w / IMAGE_SIZE;
                float height = h / IMAGE_SIZE;

                yMin = 1f - yMin - height;

                proposals.Add(new Detection
                {
                    classId = bestClass,
                    confidence = maxScore,
                    box = new Rect(xMin, yMin, width, height)
                });
            }
        }

        return NonMaxSuppression(proposals);
    }

    // Performs relevant NonMaxSuppression and CalculateIoU to determine which detections are valid
    private List<Detection> NonMaxSuppression(List<Detection> proposals)
    {
        proposals.Sort((a, b) => b.confidence.CompareTo(a.confidence));

        List<Detection> result = new List<Detection>();

        while (proposals.Count > 0)
        {
            Detection best = proposals[0];
            result.Add(best);
            proposals.RemoveAt(0);
            for (int i = proposals.Count - 1; i >= 0; i--)
            {
                if (CalculateIoU(best.box, proposals[i].box) > iouThreshold)
                    proposals.RemoveAt(i);
            }
        }

        return result;
    }

    private float CalculateIoU(Rect boxA, Rect boxB)
    {
        float intersectionX = Mathf.Max(boxA.x, boxB.x);
        float intersectionY = Mathf.Max(boxA.y, boxB.y);
        float intersectionW = Mathf.Min(boxA.x + boxA.width, boxB.x + boxB.width) - intersectionX;
        float intersectionH = Mathf.Min(boxA.y + boxA.height, boxB.y + boxB.height) - intersectionY;

        if (intersectionW < 0 || intersectionH < 0) return 0;

        float intersectionArea = intersectionW * intersectionH;
        float unionArea = (boxA.width * boxA.height) + (boxB.width * boxB.height) - intersectionArea;

        return intersectionArea / unionArea;
    }

    // Clean-up
    void OnDestroy()
    {
        worker?.Dispose();
        inputTensor?.Dispose();
        if (targetTexture != null) targetTexture.Release();
    }
}