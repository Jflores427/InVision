using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using CandyCoded.env;

// Data Structures for Responses
[System.Serializable] public class GeminiResponseRoot { public Candidate[] candidates; }
[System.Serializable] public class Candidate { public Content content; }
[System.Serializable] public class Content { public Part[] parts; }
[System.Serializable] public class Part { public string text; }

public class CloudQueryManager : MonoBehaviour
{
    [Header("API Settings")]
    [Tooltip("Gemini API Key goes here")]
    public string apiKey = "";
    private const string API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    public static CloudQueryManager Instance { get; private set; }

    void Start()
    {
        if (env.TryParseEnvironmentVariable("GEMINI_API_KEY", out string apiKeyParsed))    
        {
            Debug.Log("API Key Loaded: " + apiKeyParsed);
            apiKey = apiKeyParsed;
        }
        else
        {
            Debug.LogError("GEMINI_API_KEY not found in .env file!");
        }
    }

    // Network Logic for PostRequest
    IEnumerator PostRequest(string json, Action<string> callback)
    {
        string url = $"{API_URL}?key={apiKey}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Cloud] Error: {request.error}\nResponse: {request.downloadHandler.text}");
                callback?.Invoke("Error: Could not connect to AI.");
            }
            else
            {
                string responseText = ParseGeminiResponse(request.downloadHandler.text);
                callback?.Invoke(responseText);
            }
        }
    }

    // Image helper for resizing and encoding a 2D Texture
    private string ResizeAndEncode(Texture2D source)
    {
        // Resize to max 512px to minimize payload size
        int maxSize = 512;
        float aspect = (float) source.width / source.height;
        int width, height;

        if (source.width > source.height) 
        { 
            width = maxSize; 
            height = (int)(maxSize / aspect); 
        }
        else 
        { 
            height = maxSize; 
            width = (int)(maxSize * aspect); 
        }

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0);
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        // Encodes the result texture data into a JPEG byte array with 90% (Was 70%) quality.
        byte[] bytes = result.EncodeToJPG(90);
        Destroy(result);

        return Convert.ToBase64String(bytes);
    }

    // String Helpers
    private string EscapeJson(string s)
    {
        if (s == null) return "";

        // Need to escape quotes and backslashes for valid JSON
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }

    private string ParseGeminiResponse(string json)
    {
        try
        {
            // Use JsonUtility for parsing the response
            GeminiResponseRoot response = JsonUtility.FromJson<GeminiResponseRoot>(json);
            if (response != null && response.candidates != null && response.candidates.Length > 0)
            {
                return response.candidates[0].content.parts[0].text;
            }
            return "No text found in response.";
        }
        catch (Exception e)
        {
            Debug.LogError($"Parse Error: {e.Message}");
            return "Error parsing AI response.";
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // RANDOM FACT (Text Only)
    public void GetRandomFact(string objectLabel, Action<string> onComplete)
    {
        string prompt = $"I am looking at a {objectLabel} in Augmented Reality. Give me one short, interesting, and random fact about {objectLabel}s. Keep it under 20 words.";

        // Manual JSON construction
        string jsonPayload = $@"{{
            ""contents"": [{{
                ""parts"": [
                    {{ ""text"": ""{EscapeJson(prompt)}"" }}
                ]
            }}]
        }}";

        StartCoroutine(PostRequest(jsonPayload, onComplete));
    }

    // SPECIFICS (Image + Text) 
    public void GetSpecifics(Texture2D image, string objectLabel, Action<string> onComplete)
    {
        string base64Image = ResizeAndEncode(image);
        string prompt = $"I am pointing my camera at this object (detected as {objectLabel}). Identify exactly what specific kind/brand/type this is, and tell me one unique thing about it. Keep it brief and under 45 words.";

        // Manual JSON construction for Multimodal (Text + Image)
        string jsonPayload = $@"{{
            ""contents"": [{{
                ""parts"": [
                    {{ ""text"": ""{EscapeJson(prompt)}"" }},
                    {{ ""inline_data"": {{ ""mime_type"": ""image/jpeg"", ""data"": ""{base64Image}"" }} }}
                ]
            }}]
        }}";

        StartCoroutine(PostRequest(jsonPayload, onComplete));
    }
}

