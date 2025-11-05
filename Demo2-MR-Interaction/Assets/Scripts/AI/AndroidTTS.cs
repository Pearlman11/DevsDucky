using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Implements ITTS using the native Android TextToSpeech engine.
/// This is complex because we must synthesize to a file to get an AudioClip,
/// rather than just playing directly.
/// </summary>
public class AndroidTTS : ITTS, System.IDisposable
{
    private AndroidJavaObject tts;
    private AndroidJavaObject mainActivity;
    private bool isInitialized = false;
    private string tempFilePath;
    private string logFilePath;

    private TaskCompletionSource<bool> initTcs;
    private TaskCompletionSource<bool> synthTcs;
    
    private void Log(string message)
    {
        Debug.Log(message);
        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                File.AppendAllText(logFilePath, $"{System.DateTime.Now:HH:mm:ss.fff} - {message}\n");
            }
        }
        catch { }
    }

    public AndroidTTS(float speechRate)
    {
        Debug.Log("[AndroidTTS] Initializing...");
        
        // FIRST: Check if TTS is available
        if (!CheckTTSAvailability())
        {
            Debug.LogError("[AndroidTTS] TTS is NOT available on this device!");
            initTcs = new TaskCompletionSource<bool>();
            initTcs.TrySetResult(false);
            return;
        }
        
        Debug.Log("[AndroidTTS] TTS availability check passed!");
        
        try
        {
            // get unity players activity
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            mainActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            Debug.Log("[AndroidTTS] Got main activity");

            // get cache dir path
            var cacheDir = mainActivity.Call<AndroidJavaObject>("getCacheDir");
            var cachePath = cacheDir.Call<string>("getAbsolutePath");
            tempFilePath = Path.Combine(cachePath, "tts_temp.wav");
            logFilePath = Path.Combine(cachePath, "tts_debug.log");
            
            // Clear old log file
            try { File.Delete(logFilePath); } catch { }
            
            Log($"[AndroidTTS] Temp file path: {tempFilePath}");
            Log($"[AndroidTTS] Log file path: {logFilePath}");
            
            initTcs = new TaskCompletionSource<bool>();

            // run TTS initialization on the Android UI thread
            mainActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                Log("[AndroidTTS] Creating TTS on Android UI thread...");
                
                try
                {
                    // creating an oninitListener
                    var listener = new TTSInitListener();
                    listener.OnInit += (status) =>
                    {
                        Log($"[AndroidTTS] OnInit callback received with status: {status}");
                        
                        if (status == 0) // SUCCESS
                        {
                            try
                            {
                                var result = tts.Call<int>("setLanguage", new AndroidJavaClass("java.util.Locale").GetStatic<AndroidJavaObject>("US"));
                                if (result < 0)
                                {
                                    Log($"AndroidTTS: Language (US) not supported, result: {result}");
                                }
                                else
                                {
                                    Log("[AndroidTTS] Language set to US");
                                }
                                
                                tts.Call<int>("setSpeechRate", speechRate);
                                Log($"[AndroidTTS] Speech rate set to: {speechRate}");
                                
                                isInitialized = true;
                                initTcs.TrySetResult(true);
                                Log("[AndroidTTS] Initialization complete!");
                            }
                            catch (System.Exception e)
                            {
                                Log($"[AndroidTTS] Exception during TTS configuration: {e.Message}\n{e.StackTrace}");
                                initTcs.TrySetResult(false);
                            }
                        }
                        else
                        {
                            Log($"AndroidTTS: Initialization failed with status: {status} (0=SUCCESS, -1=ERROR, -2=ERROR_NOT_INSTALLED_YET)");
                            initTcs.TrySetResult(false);
                        }
                    };
                    
                    tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", mainActivity, listener);
                    Log("[AndroidTTS] TextToSpeech object created, waiting for onInit callback...");
                }
                catch (System.Exception e)
                {
                    Log($"[AndroidTTS] Exception creating TextToSpeech: {e.Message}\n{e.StackTrace}");
                    MainThread.Enqueue(() => initTcs.TrySetResult(false));
                }
            }));
        }
        catch (System.Exception e)
        {
            Log($"[AndroidTTS] Exception in constructor: {e.Message}\n{e.StackTrace}");
            initTcs?.TrySetResult(false);
        }
        
        Log($"[AndroidTTS] Constructor complete. Check log file at: {logFilePath}");
    }
    
    private bool CheckTTSAvailability()
    {
        try
        {
            Debug.Log("[AndroidTTS] Checking TTS availability...");
            
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            
            Debug.Log("[AndroidTTS] Creating Intent for TTS engine check...");
            
            // Check if TTS data is available
            var intent = new AndroidJavaObject("android.content.Intent");
            var ttsClass = new AndroidJavaClass("android.speech.tts.TextToSpeech");
            var engineClass = ttsClass.GetStatic<AndroidJavaClass>("Engine");
            var actionCheckTtsData = engineClass.GetStatic<string>("ACTION_CHECK_TTS_DATA");
            
            intent.Call<AndroidJavaObject>("setAction", actionCheckTtsData);
            
            Debug.Log("[AndroidTTS] Checking package manager for TTS engines...");
            
            var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
            var resolveInfoList = packageManager.Call<AndroidJavaObject>("queryIntentActivities", intent, 0);
            int listSize = resolveInfoList.Call<int>("size");
            
            Debug.Log($"[AndroidTTS] Found {listSize} TTS engines");
            
            if (listSize > 0)
            {
                // List available engines
                for (int i = 0; i < listSize; i++)
                {
                    var resolveInfo = resolveInfoList.Call<AndroidJavaObject>("get", i);
                    var activityInfo = resolveInfo.Get<AndroidJavaObject>("activityInfo");
                    var packageName = activityInfo.Get<string>("packageName");
                    Debug.Log($"[AndroidTTS] Available TTS engine {i}: {packageName}");
                }
                return true;
            }
            else
            {
                Debug.LogError("[AndroidTTS] No TTS engines found on device!");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AndroidTTS] Exception checking TTS availability: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    public async Task<AudioClip> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        Debug.Log($"[AndroidTTS] SynthesizeAsync called with text: {text.Substring(0, Math.Min(50, text.Length))}...");
        
        if (!isInitialized)
        {
            Debug.Log("[AndroidTTS] Waiting for initialization...");
            
            // Wait with timeout
            var timeoutTask = Task.Delay(10000); // 10 second timeout
            var completedTask = await Task.WhenAny(initTcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Debug.LogError("[AndroidTTS] Initialization timed out after 10 seconds. TTS engine may not be available on this device.");
                return null;
            }
            
            bool success = await initTcs.Task;
            if (!success)
            {
                Debug.LogError("[AndroidTTS] Init failed, cannot synthesize. Check if TTS engine is installed on device.");
                return null;
            }
        }

        synthTcs = new TaskCompletionSource<bool>();

        // Create an OnUtteranceProgressListener
        var listener = new TTSUtteranceListener();
        listener.OnDone += (utteranceId) =>
        {
            Debug.Log($"[AndroidTTS] Synthesis complete for utterance: {utteranceId}");
            synthTcs.TrySetResult(true);
        };
        listener.OnError += (utteranceId, error) =>
        {
            Debug.LogError($"AndroidTTS: Synthesis failed with error: {error}");
            synthTcs.TrySetResult(false);
        };

        // Use a timestamp-based utterance ID (thread-safe)
        string utteranceId = "DuckyTTS_" + System.DateTime.UtcNow.Ticks;
        var parameters = new AndroidJavaObject("android.os.Bundle"); // Empty bundle

        Debug.Log($"[AndroidTTS] Starting synthesis with utterance ID: {utteranceId}");

        // Dispatch synthesis to Android UI thread
        mainActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            try
            {
                tts.Call<int>("setOnUtteranceProgressListener", listener);
                int synthResult = tts.Call<int>("synthesizeToFile", text, parameters, new AndroidJavaObject("java.io.File", tempFilePath), utteranceId);
                Debug.Log($"[AndroidTTS] synthesizeToFile returned: {synthResult}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AndroidTTS] Exception during synthesizeToFile: {e.Message}");
                MainThread.Enqueue(() => synthTcs.TrySetResult(false));
            }
        }));

        Debug.Log("[AndroidTTS] Waiting for synthesis to complete...");
        
        // Wait for synthesis to complete
        bool synthSuccess = await synthTcs.Task;
        
        if (!synthSuccess)
        {
            Debug.LogError("[AndroidTTS] Synthesis failed");
            return null;
        }
        
        if (ct.IsCancellationRequested)
        {
            Debug.Log("[AndroidTTS] Synthesis cancelled");
            return null;
        }

        Debug.Log($"[AndroidTTS] Loading audio from file: {tempFilePath}");
        
        // Load the synthesized WAV file
        return await LoadAudioClipFromFileAsync(tempFilePath, ct);
    }

    private async Task<AudioClip> LoadAudioClipFromFileAsync(string path, CancellationToken ct)
    {
        Debug.Log($"[AndroidTTS] LoadAudioClipFromFileAsync: {path}");
        
        // Check if file exists
        if (!File.Exists(path))
        {
            Debug.LogError($"[AndroidTTS] File does not exist: {path}");
            return null;
        }
        
        FileInfo fileInfo = new FileInfo(path);
        Debug.Log($"[AndroidTTS] File size: {fileInfo.Length} bytes");
        
        string url = "file://" + path;
        using var uwr = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
        var op = uwr.SendWebRequest();

        while (!op.isDone)
        {
            if (ct.IsCancellationRequested)
            {
                Debug.Log("[AndroidTTS] LoadAudioClip cancelled");
                uwr.Abort();
                return null;
            }
            await Task.Yield();
        }

        if (uwr.result == UnityWebRequest.Result.Success)
        {
            var clip = DownloadHandlerAudioClip.GetContent(uwr);
            Debug.Log($"[AndroidTTS] AudioClip loaded successfully! Length: {clip.length}s");
            return clip;
        }
        else
        {
            Debug.LogError($"[AndroidTTS] Failed to load TTS AudioClip: {uwr.error}");
            return null;
        }
    }

    public void Dispose()
    {
        Debug.Log("[AndroidTTS] Disposing...");
        mainActivity?.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            tts?.Call("stop");
            tts?.Call("shutdown");
            tts?.Dispose();
        }));
    }
    
    // --- Internal Android Proxy Classes ---
    private class TTSInitListener : AndroidJavaProxy
    {
        public event System.Action<int> OnInit;
        public TTSInitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") {}
        public void onInit(int status) => OnInit?.Invoke(status);
    }

    private class TTSUtteranceListener : AndroidJavaProxy
    {
        public event System.Action<string> OnDone;
        public event System.Action<string, int> OnError;
        public TTSUtteranceListener() : base("android.speech.tts.UtteranceProgressListener") {}
        public void onDone(string utteranceId) => MainThread.Enqueue(() => OnDone?.Invoke(utteranceId));
        public void onError(string utteranceId, int error) => MainThread.Enqueue(() => OnError?.Invoke(utteranceId, error));
        public void onError(string utteranceId) => MainThread.Enqueue(() => OnError?.Invoke(utteranceId, -1)); // Deprecated version
        public void onStart(string utteranceId) {}
    }
}