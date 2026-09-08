using System.IO;
using Microsoft.Extensions.Logging;

namespace NuvioPlayer.Helpers;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public FileLoggerProvider(string logFilePath)
    {
        _logFilePath = logFilePath;
        var dir = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _logFilePath, _lock);
    }

    public void Dispose()
    {
    }

    private class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly object _lock;

        public FileLogger(string categoryName, string filePath, object lockObj)
        {
            _categoryName = categoryName;
            _filePath = filePath;
            _lock = lockObj;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string message = formatter(state, exception);
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"[{time}] [{logLevel.ToString().ToUpperInvariant()}] [{_categoryName}] {message}";
            if (exception != null)
            {
                line += Environment.NewLine + exception;
            }

            System.Diagnostics.Debug.WriteLine(line);

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine);
                }
                catch
                {
                    // Avoid recursive crashing from logger failures
                }
            }
        }
    }
}
