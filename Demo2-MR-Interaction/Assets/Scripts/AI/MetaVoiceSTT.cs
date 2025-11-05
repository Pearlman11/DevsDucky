using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;
using UnityEngine;
using UI;


/// <summary>
/// Implements ITranscriber using the Wit.ai HTTP API.
/// This is required to satisfy the `TranscribeChunks(AudioClip)` interface,
/// as the Meta Voice SDK's `AppVoiceExperience` wants to control the mic itself.
/// </summary>
public class MetaVoiceSTT : ITranscriber
{
    private readonly string _apiToken;
    private static readonly HttpClient httpClient = new HttpClient();

     // trim junk often found in SSE/stream responses
    private static readonly char RS = (char)0x1E; // ASCII record Separator
    private static readonly char[] TrimChars = new[] { '\r', '\n', '\0', ' ', (char)0x1E };



    public MetaVoiceSTT(string clientToken)
    {
        _apiToken = clientToken;

    }
    
    public async IAsyncEnumerable<string> TranscribeChunks(AudioClip clip, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (clip == null)
        {
            Debug.LogError("TranscribeChunks: AudioClip is null");
            yield break;
        }

        // Convert AudioClip to WAV
        byte[] wavData = WavUtility.FromAudioClip(clip);
        if (wavData == null)
        {
            Debug.LogError("TranscribeChunks: Failed to convert AudioClip to WAV.");
            yield break;
        }

        // prepare HTTP Request
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.wit.ai/speech");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken);
        request.Content = new ByteArrayContent(wavData);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");

        // send request
        HttpResponseMessage response = null;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            Debug.LogError($"Wit.ai request failed: {e.Message}");
            yield break;
        }
        catch (TaskCanceledException)
        {
            Debug.Log("Wit.ai request cancelled");
            yield break;
        }

        string finalTranscript = null;
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        StringBuilder jsonBuffer = new StringBuilder();
        int braceDepth = 0;



       while(!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            string line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // clean up control characters
            var cleanLine = line.Trim(TrimChars);
            if (cleanLine.Length == 0) continue;

            if (cleanLine.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
            {
                cleanLine = cleanLine.Substring(5).Trim(TrimChars);
            }
            if (cleanLine.Length == 0) continue;


            // Accumulate json and track brace depth
            foreach(char c in cleanLine)
            {
                jsonBuffer.Append(c);
                if (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
                // complete json object found
                if (braceDepth == 0 && jsonBuffer.Length > 0)
                {
                    string completeJson = jsonBuffer.ToString();
                    jsonBuffer.Clear();

                    try
                    {
                        var json = JObject.Parse(completeJson);
                        var type = json["type"]?.Value<string>();
                        var text = json["text"]?.Value<string>();

                        if (!string.IsNullOrEmpty(text))
                        {
                            Debug.Log($"[Wit.ai {type}] {text}");

                            if (type == "FINAL_TRANSCRIPTION" || type == "FINAL_UNDERSTANDING")
                            {
                                finalTranscript = text;
                                break;
                            }
                            else if (json["is_final"]?.Value<bool>() == true)
                            {
                                finalTranscript = text;
                                break;
                            }
                            
                            
                        }
                    }
                    catch (System.Exception e)
                    {
                         Debug.LogWarning($"Failed to parse Wit.ai JSON: {completeJson}. Error: {e.Message}");
                    }
            
                }

               
            }
            
        }
        if (!string.IsNullOrEmpty(finalTranscript))
        {
            Debug.Log($"[Wit FINAL] {finalTranscript}");
            yield return finalTranscript;
        }
        else
        {
           Debug.LogWarning($"Wit.ai stream ended without a final transcript.");
        }
    }
}
