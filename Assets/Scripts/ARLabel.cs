using UnityEngine;
using TMPro;

public class ARLabel : MonoBehaviour
{
    public TMP_Text labelText;
    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    public void SetText(string text)
    {
        labelText.text = text;
    }

    void Update()
    {
        if (cameraTransform != null)
        {
            // Look at the camera and then flip by 180.
            // Otherwise UI text will appear mirrored
            transform.LookAt(transform.position + cameraTransform.rotation * Vector3.forward, cameraTransform.rotation * Vector3.up);
        }
    }
}