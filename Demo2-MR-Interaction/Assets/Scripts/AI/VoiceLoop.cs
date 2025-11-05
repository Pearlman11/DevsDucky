using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UI;
using UnityEngine;
using ChatHistory = System.Collections.Generic.List<(string role, string content)>;

/// <summary>
/// The main state machine for the Dev's Ducky.
/// </summary> 

public class VoiceLoop : MonoBehaviour
{
    [SerializeField] private AIConfig config;
    [SerializeField] private DuckSpawner spawner;
    [SerializeField] private string micDeviceName;

    private SpeechBubbleUI speechBubble;
    private AudioSource audioSource;
    private Animator animator;


    // AI Services
    private ITranscriber transcriber;
    private IChatLLM llm;
    private ITTS tts;


    private enum State { Idle, Listening, Transcribing, Thinking, Speaking }
    private State state = State.Idle;

    private AudioClip recordedClip;
    private CancellationTokenSource cts;

    private readonly ChatHistory chatHistory = new ChatHistory();
    
    // For main thread updates
    private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
    private readonly object _lock = new object();

    void Start()
    {
        //Init AI Services
        transcriber = new MetaVoiceSTT(config.WitClientToken);
        llm = new LlamaClient(config.LlamaApiEndpoint, config.LlamaApiKey, config.LlamaModel, config.Temperature);
        tts = new AndroidTTS(config.TtsSpeechRate);

        // adding system prompt
        chatHistory.Add(("system",

            "You are 'Dev's Rubber Ducky,' a calm, curious coding teacher and companion. Keep replies short and sweet and be sure to answer the question directly, in addtion, keep replies under 100 tokens unless asked to expand. "
        ));
    }

    void Update()
    {
        // Execute any queued main thread actions
        lock (_lock)
        {
            while (_mainThreadActions.Count > 0)
            {
                _mainThreadActions.Dequeue()?.Invoke();
            }
        }
        
        // lazy get duck components
        if (speechBubble == null || audioSource == null)
        {
            GameObject duck = spawner.GetDuckInstance();
            if (duck != null)
            {
                Debug.Log($"[VoiceLoop] Found duck: {duck.name}");
                
                speechBubble = duck.GetComponentInChildren<SpeechBubbleUI>(true); // includeInactive = true
                audioSource = duck.GetComponentInChildren<AudioSource>(true);
                animator = duck.GetComponentInChildren<Animator>(true);
                
                Debug.Log($"[VoiceLoop] Components found - SpeechBubble: {speechBubble != null}, AudioSource: {audioSource != null}, Animator: {animator != null}");
                
                if (speechBubble == null)
                {
                    Debug.LogError($"[VoiceLoop] Could not find SpeechBubbleUI component on duck '{duck.name}' or its children!");
                }
            }
            else
            {
                return;
            }
        }
    
        // --- State Machine Input ---
        bool isPttDown = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
                         OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch);


        bool isPttUp = OVRInput.GetUp(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
                       OVRInput.GetUp(OVRInput.Button.One, OVRInput.Controller.LTouch);

        if (state == State.Idle && isPttDown)
        {
            StartListening();
        }
        else if (state == State.Listening && isPttUp)
        {
            StopListeningAndProcess();
        }
        else if (state != State.Idle && OVRInput.GetDown(OVRInput.Button.Two)) // B button cancels
        {
            CancelAll();
        }
    }
    
    private void RunOnMainThread(Action action)
    {
        lock (_lock)
        {
            _mainThreadActions.Enqueue(action);
        }
    }

    private void StartListening()
    {
        if (speechBubble == null)
        {
            Debug.LogWarning("VoiceLoop: speechbubble not ready; aborting StartListening");
            return;
        }
        state = State.Listening;
        speechBubble.SetState("Listening...");
        animator?.SetBool("isListening", true);

        // using defaultDevice when none is set
        string device = string.IsNullOrEmpty(micDeviceName) ? null : micDeviceName;

        // start recording from the mic
        recordedClip = Microphone.Start(micDeviceName, false, 15, 16000); // 15 sec max, 16kHz
        if (recordedClip == null)
        {
            Debug.LogError("VoiceLoop: Microphone.Start failed (no device?).");
            SetStateIdle();
        }
    
    }

    private async void StopListeningAndProcess()
    {
        state = State.Transcribing;
        animator?.SetBool("isListening", false);

        Microphone.End(micDeviceName);
        speechBubble.SetState("Transcribing...");

        cts = new CancellationTokenSource();
        string userTranscript = await GetTranscript(recordedClip, cts.Token);

        if (string.IsNullOrEmpty(userTranscript) || cts.IsCancellationRequested)
        {
            SetStateIdle();
            return;
        }

        speechBubble.ShowMessage($"<b>You:</b> {userTranscript}");
        chatHistory.Add(("user", userTranscript));

        // llm
        state = State.Thinking;
        speechBubble.SetState("Thinking...");
        string fullResponse = await GetLLMResponse(cts.Token);

        if (string.IsNullOrEmpty(fullResponse) || cts.IsCancellationRequested)
        {
            SetStateIdle();
            return;
        }
        chatHistory.Add(("assistant", fullResponse));

        // TTS
        state = State.Speaking;
        animator?.SetBool("isSpeaking", true);

        AudioClip responseClip = await tts.SynthesizeAsync(fullResponse, cts.Token);

        if (responseClip != null && !cts.IsCancellationRequested)
        {
            audioSource.PlayOneShot(responseClip);

            // wait for clip to finish before returning to idle
            await Task.Delay((int)(responseClip.length * 1000));

        }
        SetStateIdle();
    }

    private async Task<string> GetTranscript(AudioClip clip, CancellationToken ct)
    {
        string finalTranscript = null;
        await foreach (var chunk in transcriber.TranscribeChunks(clip, ct))
        {
            // with this api we only get one final chunk
            finalTranscript = chunk;
        }
        return finalTranscript;
    }

    private async Task<string> GetLLMResponse(CancellationToken ct)
    {
        var responseBuilder = new StringBuilder();
        string currentText = "";
        
        Debug.Log("[VoiceLoop] Starting LLM stream...");

        await foreach (var token in llm.ChatStream(chatHistory, ct))
        {
            responseBuilder.Append(token);
            currentText = responseBuilder.ToString();

            // Update subtitles on main thread
            string textToShow = currentText; 
            MainThread.Enqueue(() =>
            {
                if (speechBubble != null)
                {
                    Debug.Log($"[VoiceLoop] Updating bubble with: {textToShow.Substring(0, Math.Min(50, textToShow.Length))}...");
                    speechBubble.ShowMessage($"<b>Ducky:</b> {textToShow}");
                }
                else
                {
                    Debug.LogWarning("[VoiceLoop] speechBubble is null during update!");
                }
            });
        }
        
        Debug.Log($"[VoiceLoop] LLM stream complete. Total length: {responseBuilder.Length}");
        return responseBuilder.ToString();
    }

    private void CancelAll()
    {
        cts?.Cancel();
        audioSource?.Stop();
        if (state == State.Listening)
        {
            Microphone.End(micDeviceName);
        }
        SetStateIdle();
    }

    private void SetStateIdle()
    {

        // we will leave the last response displaying
        state = State.Idle;
        animator?.SetBool("isListening", false);
        animator?.SetBool("isSpeaking", false);
    }

    void OnDestroy()
    {
        (tts as System.IDisposable)?.Dispose();
    }
}