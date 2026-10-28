using System;
using System.Globalization;
using log4net;

namespace BunnyLoader;

internal sealed class ModLogger : ILog
{
    readonly string prefix;

    public ModLogger(string name) => prefix = $"[{name}] ";

    public bool IsDebugEnabled => true;

    public bool IsInfoEnabled => true;

    public bool IsWarnEnabled => true;

    public bool IsErrorEnabled => true;

    public bool IsFatalEnabled => true;

    public void Debug(object message) => Log.Info(prefix + message);

    public void Debug(object message, Exception exception) => Log.Info(prefix + With(message, exception));

    public void DebugFormat(string format, params object[] args) => Log.Info(prefix + Format(format, args));

    public void Info(object message) => Log.Info(prefix + message);

    public void Info(object message, Exception exception) => Log.Info(prefix + With(message, exception));

    public void InfoFormat(string format, params object[] args) => Log.Info(prefix + Format(format, args));

    public void Warn(object message) => Log.Warn(prefix + message);

    public void Warn(object message, Exception exception) => Log.Warn(prefix + With(message, exception));

    public void WarnFormat(string format, params object[] args) => Log.Warn(prefix + Format(format, args));

    public void Error(object message) => Log.Error(prefix + message);

    public void Error(object message, Exception exception) => Log.Error(prefix + With(message, exception));

    public void ErrorFormat(string format, params object[] args) => Log.Error(prefix + Format(format, args));

    public void Fatal(object message) => Log.Error(prefix + message);

    public void Fatal(object message, Exception exception) => Log.Error(prefix + With(message, exception));

    public void FatalFormat(string format, params object[] args) => Log.Error(prefix + Format(format, args));

    static string With(object message, Exception exception) => $"{message}\n{exception}";

    static string Format(string format, object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
