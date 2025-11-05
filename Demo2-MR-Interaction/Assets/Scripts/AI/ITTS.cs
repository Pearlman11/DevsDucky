using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;



public interface ITTS
{
    // Synthesizes text into playable AudioClip
    Task<AudioClip> SynthesizeAsync(string text, CancellationToken ct = default);
        
}
