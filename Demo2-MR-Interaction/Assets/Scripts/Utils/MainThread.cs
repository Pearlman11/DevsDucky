using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Utility to dispach actions from any thread to the Unity main thread
/// </summary>
public class MainThread : MonoBehaviour
{
    private static MainThread _instance;
    private readonly ConcurrentQueue<Action> _dispatchQueue = new ConcurrentQueue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        _instance = new GameObject("MainThreadDispatcher").AddComponent<MainThread>();
        DontDestroyOnLoad(_instance.gameObject);
    }

    private void Update()
    {
        while (_dispatchQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }
    
    // Enqueues an action to be executed on the Unity main thread
    public static void Enqueue(Action action)
    {
        if (_instance == null)
        {
            Debug.LogError("MainThread not initialized! Attempting to initialize now...");
            Initialize();
        }
        
        if (action == null)
        {
            Debug.LogWarning("MainThread.Enqueue: null action provided");
            return;
        }
        
        _instance._dispatchQueue.Enqueue(action);
    }
}
