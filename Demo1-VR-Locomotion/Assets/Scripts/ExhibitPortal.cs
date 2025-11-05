using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
public class ExhibitPortal : MonoBehaviour
{
    [SerializeField] string sceneToLoad;
    [SerializeField] Transform xrOrigin;
    [SerializeField] Transform spawnPointInNew;
    [SerializeField] CanvasGroup fader;

    bool busy;

    void OnTriggerEnter(Collider other)
    {
        if (busy || !other.transform.IsChildOf(xrOrigin)) return;
        StartCoroutine(EnterDoor());
    }

    IEnumerator EnterDoor()
    {
        busy = true;

        // fade out
        yield return Fade(1f, 0.2f);

        var op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        // get new scene and spawn point
        var newScene = SceneManager.GetSceneByName(sceneToLoad);
        SceneManager.SetActiveScene(newScene);

        if (spawnPointInNew == null)
        {
            foreach (var root in newScene.GetRootGameObjects())
            {
                var t = root.transform.Find("SpawnPoint");
                if (t) { spawnPointInNew = t; break; }
            }
        }

        if (spawnPointInNew != null)
        {
            xrOrigin.position = spawnPointInNew.position;

        }
        // unload previous scene
        var prev = gameObject.scene;
        if (prev.name != "Museum_Hub")
        {
            SceneManager.UnloadSceneAsync(prev);
        }

        yield return Fade(0f, 0.2f);
        busy = false;
    }

    IEnumerator Fade(float target, float time)
    {
        float start = fader.alpha, t = 0f;
        fader.blocksRaycasts = true;
        while (t < time)
        {
            t += Time.deltaTime;
            fader.alpha = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        fader.alpha = target;
        fader.blocksRaycasts = target > 0.99f;
    }
}
