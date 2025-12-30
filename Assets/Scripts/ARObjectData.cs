using UnityEngine;

public class ARObjectData : MonoBehaviour
{
    public string objectClass;      
    public float confidence;         
    public Texture2D snapshot;       // The image taken when this object was found
}