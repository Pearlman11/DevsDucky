using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public interface ITranscriber
{
    /// <summary>
    /// Transcribes an audio clip. may yield one or more chunks
    /// </summary>
    IAsyncEnumerable<string> TranscribeChunks(AudioClip clip, CancellationToken ct = default);
}
