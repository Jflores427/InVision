using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlacementReticle : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    public GameObject reticlePrefab;

    private GameObject spawnedReticle;
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Start()
    {
        spawnedReticle = Instantiate(reticlePrefab);
        spawnedReticle.SetActive(false); // Hide by default
    }

    void Update()
    {
        // Shoot a ray from the center of the screen
        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);

        if (raycastManager.Raycast(screenCenter, hits, TrackableType.AllTypes))
        {
            // A valid plane has been hit
            Pose hitPose = hits[0].pose;

            spawnedReticle.SetActive(true);
            spawnedReticle.transform.position = hitPose.position;
            spawnedReticle.transform.rotation = hitPose.rotation;
        }
        else
        {
            // We lost the surface, so hide the reticle.
            spawnedReticle.SetActive(false);
        }
    }
}