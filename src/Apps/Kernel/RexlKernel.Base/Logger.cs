// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

public enum LogLevel
{
    None,
    Top,
    Mid,
    Low,
    All,
}

public sealed class Logger
{
    private readonly string _session;

    public readonly LogLevel Level;

    public Logger(string session, LogLevel lvl)
    {
        _session = session;
        Level = lvl;
    }

    public void Log(LogLevel lvl, string msg)
    {
        if (lvl <= Level)
            Console.WriteLine("*** {0}: {1}", _session, msg);
    }
    public void Log(LogLevel lvl, string msg, params object[] args)
    {
        if (lvl <= Level)
        {
            if (args.Length > 0)
                msg = string.Format(msg, args);
            Console.WriteLine("*** {0}: {1}", _session, msg);
        }
    }

    public void LogTop(string msg) { Log(LogLevel.Top, msg); }
    public void LogTop(string msg, params object[] args) { Log(LogLevel.Top, msg, args); }

    public void LogMid(string msg) { Log(LogLevel.Mid, msg); }
    public void LogMid(string msg, params object[] args) { Log(LogLevel.Mid, msg, args); }

    public void LogLow(string msg) { Log(LogLevel.Low, msg); }
    public void LogLow(string msg, params object[] args) { Log(LogLevel.Low, msg, args); }

    public void LogAll(string msg) { Log(LogLevel.All, msg); }
    public void LogAll(string msg, params object[] args) { Log(LogLevel.All, msg, args); }
}
