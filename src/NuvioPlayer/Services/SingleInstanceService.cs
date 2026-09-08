using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Interfaces;

namespace NuvioPlayer.Services;

public class SingleInstanceService : ISingleInstanceService
{
    private const string MutexName = "Local\\NuvioPlayer_SingleInstance_Mutex";
    private const string PipeName = "NuvioPlayer_IPC_Pipe";

    private readonly ILogger<SingleInstanceService> _logger;
    private Mutex? _mutex;
    private bool _hasMutex;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public SingleInstanceService(ILogger<SingleInstanceService> logger)
    {
        _logger = logger;
    }

    public bool TryAcquireSingleInstance()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            _hasMutex = createdNew;
            _logger.LogInformation("Single instance acquisition result: createdNew={CreatedNew}", createdNew);
            return _hasMutex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire single instance mutex");
            _hasMutex = false;
            return false;
        }
    }

    public async Task<bool> SendArgsToRunningInstanceAsync(IEnumerable<string> args, int timeoutMs = 3000)
    {
        try
        {
            _logger.LogInformation("Attempting to connect to running Nuvio Player instance via named pipe...");
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            using var cts = new CancellationTokenSource(timeoutMs);

            await client.ConnectAsync(cts.Token);

            using var writer = new StreamWriter(client) { AutoFlush = true };
            string json = JsonSerializer.Serialize(args);
            await writer.WriteLineAsync(json);

            _logger.LogInformation("Successfully sent command line arguments to existing instance.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send arguments to existing instance.");
            return false;
        }
    }

    public void StartListening(Action<List<string>> onArgsReceived)
    {
        if (_listenerTask != null) return;

        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoopAsync(onArgsReceived, _cts.Token));
    }

    private async Task ListenLoopAsync(Action<List<string>> onArgsReceived, CancellationToken ct)
    {
        _logger.LogInformation("Starting IPC pipe server listener...");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server);
                string? line = await reader.ReadLineAsync(ct);

                if (!string.IsNullOrWhiteSpace(line))
                {
                    try
                    {
                        var args = JsonSerializer.Deserialize<List<string>>(line);
                        if (args != null && args.Count > 0)
                        {
                            _logger.LogInformation("Received {Count} arguments from secondary instance.", args.Count);
                            onArgsReceived(args);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize IPC message: {Line}", line);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IPC listener loop");
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("IPC pipe server listener stopped.");
    }

    public void Release()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch { }

        if (_hasMutex && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch { }
            _hasMutex = false;
        }

        _mutex?.Dispose();
        _mutex = null;
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }
}
