using UnityEngine;
using Meta.XR.MRUtilityKit;
using Meta.XR;

/// <summary>
/// Handles placing and moving the duck prefab using OVRInput and raycasting against OVRScene planes
/// </summary>
public class DuckSpawner : MonoBehaviour
{
    [SerializeField] private GameObject duckPrefab;
    [Tooltip("The OVRCameraRigs center eye anchor.")]
    [SerializeField] private Transform centerEyeAnchor;

    private GameObject duckInstance;
    private OVRSpatialAnchor duckAnchor;
    private bool isDuckSpawned = false;

    private EnvironmentRaycastManager raycastManager;

    void Start()
    {
        raycastManager = FindAnyObjectByType<EnvironmentRaycastManager>();
        if (raycastManager == null)
        {
            Debug.LogError("DuckSpawner: Could not find EnvironmentRaycastManager in the scene!");
        }
    }
    private void Update()
    {
        // check for A button press or Right hand punch
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
            OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            PlaceDuck();

        }
    }

    public async void PlaceDuck()
    {
        if (raycastManager == null) return;

        Vector3 spawnPos;
        Quaternion spawnRot;

        // construct ray from center eye
        var ray = new Ray(centerEyeAnchor.position, centerEyeAnchor.forward);

        // use MRUK Environement Raycast manager
        if (raycastManager.Raycast(ray, out EnvironmentRaycastHit hit, 10.0f))
        {
            spawnPos = hit.point;

            var toHead = Vector3.ProjectOnPlane(centerEyeAnchor.position - spawnPos, Vector3.up);
            if (toHead.sqrMagnitude < 1e-6f)
            {
                // fallback if you're exactly above the spawn point
                toHead = Vector3.ProjectOnPlane(centerEyeAnchor.forward, Vector3.up);
            }

            spawnRot = Quaternion.LookRotation(toHead, Vector3.up);

            Debug.LogWarning($"DuckSpawner: spawnRot = Quaternion.LookRotation() used");
        }
        else
        {
            spawnPos = centerEyeAnchor.position + centerEyeAnchor.forward * 1.5f;
            spawnRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(centerEyeAnchor.forward, Vector3.up), Vector3.up);

            Debug.LogWarning($"DuckSpawner: spawnRot = Quaternion.LookRotation() was not used used");
        }
        if (!isDuckSpawned)
        {
            duckInstance = Instantiate(duckPrefab, spawnPos, spawnRot);
            duckAnchor = duckInstance.GetComponent<OVRSpatialAnchor>();
            if (duckAnchor == null)
            {
                Debug.LogError("Duck prefab is missing OVRSpatialAnchor!");
                return;
            }
            isDuckSpawned = true;
        }
        else
        {
            // Move existing duck
            duckInstance.transform.position = spawnPos;
            duckInstance.transform.rotation = spawnRot;
        }

        // Re-save the anchor at the new location
        if (duckAnchor.enabled)
        {
            await duckAnchor.SaveAnchorAsync();
        }
    }

    // Public getter for VoiceLoop to find ducks components
    public GameObject GetDuckInstance() => duckInstance;
    

}
