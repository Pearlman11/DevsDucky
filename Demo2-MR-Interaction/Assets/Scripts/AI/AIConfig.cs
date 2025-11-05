using UnityEngine;

[CreateAssetMenu(fileName = "AIConfig", menuName = "Ducky/AI Config", order = 1)]
public class AIConfig : ScriptableObject
{
    [Header("Meta Voice SDK (Wit.ai)")]
    [Tooltip("We will get this from the Wit.ai App settings")]
    public string WitClientToken;

    [Header("Llama LLM API")]
    [Tooltip("e.g., https://api.llama-api.com/v1/chat/completions")]
    public string LlamaApiEndpoint;

    [Tooltip("API Key for LLM Provider")]
    public string LlamaApiKey;
    public string LlamaModel = "llama3-8b-8192";
    public float Temperature = 0.5f;

    [Header("Android TTS")]
    public float TtsSpeechRate = 1.0f;


}
