using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


// for history
using ChatHistory = System.Collections.Generic.List<(string role, string content)>;
public interface IChatLLM
{
    /// <summary> 
    /// sends chat history and streams back response token
    /// </summary> 

    IAsyncEnumerable<string> ChatStream(ChatHistory history, CancellationToken ct = default);









}
