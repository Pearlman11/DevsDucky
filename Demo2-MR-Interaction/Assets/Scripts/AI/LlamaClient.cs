using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using ChatHistory = System.Collections.Generic.List<(string role, string content)>;


/// <summary>
/// Implements IChatLLM using a standard Llama-compatible API endpoint
/// that supports Server-Sent Events (SSE) for streaming.
/// </summary>
public class LlamaClient : IChatLLM
{
    private readonly string _apiEndpoint;
    private readonly string _apikey;
    private readonly string _model;
    private readonly float _temperature;
    private static readonly HttpClient _httpClient = new HttpClient();

    public LlamaClient(string endpoint, string key, string model, float temp)
    {
        _apiEndpoint = endpoint;
        _apikey = key;
        _model = model;
        _temperature = temp;
    }

    public async IAsyncEnumerable<string> ChatStream(List<(string role, string content)> history, [EnumeratorCancellation]CancellationToken ct = default)
    {
        // Construct json payload
        var messages = new List<object>();
        foreach (var (role, content) in history)
        {
            messages.Add(new { role, content });
        }
        var payload = new
        {
            model = _model,
            messages = messages,
            temperature = _temperature,
            stream = true
        };

        string jsonPayload = JsonConvert.SerializeObject(payload);
        Debug.Log($"[LlamaClient] Sending request to: {_apiEndpoint}");
        Debug.Log($"[LlamaClient] Payload: {jsonPayload}");

        // Prepare HTTP Request
        using var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint);
        
        // Add authorization header only if API key is provided
        if (!string.IsNullOrEmpty(_apikey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apikey);
        }
        
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Send request and get stream
        HttpResponseMessage response = null;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            string errorBody = "";
            if (response != null)
            {
                try
                {
                    errorBody = await response.Content.ReadAsStringAsync();
                    Debug.LogError($"[LlamaClient] Server response body: {errorBody}");
                }
                catch { }
            }
            Debug.LogError($"[LlamaClient] Request failed with status {response?.StatusCode}: {e.Message}");
            yield break;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LlamaClient] Request failed: {e.Message}");
            yield break;
        }

        // Reading the server sent events (SSE) stream
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            string line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            // Remove the prefix
            string jsonData = line.Substring(6);

            if (jsonData.Trim() == "[DONE]") yield break;
            
            string token = null;
            try
            {
                var json = JObject.Parse(jsonData);

                // Path to the token depends on the API
                token = json["choices"]?[0]?["delta"]?["content"]?.Value<string>();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LlamaClient] Failed to parse stream chunk: {jsonData}. Error: {e.Message}");
            }
            
            if (token != null)
            {
                yield return token;
            }
        }
    }
}