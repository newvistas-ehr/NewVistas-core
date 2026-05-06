// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;

namespace NewVistas.Wpf_UI.Services;

/// <summary>
/// Lightweight ILogger that forwards formatted messages to a callback Action.
/// Used by ZWR import to show real-time progress in the WPF UI.
/// </summary>
public class ActionLogger : ILogger
{
    private readonly Action<string> _writeAction;
    private readonly LogLevel _minLevel;

    public ActionLogger(Action<string> writeAction, LogLevel minLevel = LogLevel.Information)
    {
        _writeAction = writeAction;
        _minLevel = minLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        string message = formatter(state, exception);
        string prefix = logLevel switch
        {
            LogLevel.Warning => "[WARN] ",
            LogLevel.Error => "[ERROR] ",
            _ => ""
        };
        _writeAction($"{prefix}{message}");
    }
}
