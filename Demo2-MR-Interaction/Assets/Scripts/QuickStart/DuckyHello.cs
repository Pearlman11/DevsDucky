using UnityEngine;

/// <summary>
/// One-file quickstart script. Attach this to your OVRCameraRig.
/// This validates spawning, input, and audio without any AI.
/// </summary>

public class DuckyHello : MonoBehaviour
{
    [SerializeField] private DuckSpawner spawner;
    [SerializeField] private AudioClip quackSound;
    private AudioSource duckAudioSource;

    void Update()
    {
        // Check for A button press or Right Hand pinch
        bool isPttDown = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
                         OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch);

        if (isPttDown)
        {
            // tell spawner to place duck
            spawner.PlaceDuck();

            // try to get audio source
            if (duckAudioSource == null && spawner.GetDuckInstance() != null)
            {
                duckAudioSource = spawner.GetDuckInstance().GetComponent<AudioSource>();
            }

            // play the sound
            if (duckAudioSource != null && quackSound != null)
            {
                duckAudioSource.PlayOneShot(quackSound);
            }
            else if (duckAudioSource == null)
            {
                Debug.LogWarning("DuckyHello: AudioSource is still null!");
            }
            else if (quackSound == null)
            {
                Debug.LogWarning("DuckyHello: 'Quack Sound' is not assigned in the inspector!");
            }
        }

    }
}
