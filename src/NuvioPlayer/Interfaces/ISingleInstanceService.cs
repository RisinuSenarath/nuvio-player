using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuvioPlayer.Interfaces;

public interface ISingleInstanceService : IDisposable
{
    bool TryAcquireSingleInstance();
    Task<bool> SendArgsToRunningInstanceAsync(IEnumerable<string> args, int timeoutMs = 3000);
    void StartListening(Action<List<string>> onArgsReceived);
    void Release();
}
