using System;

namespace log4net;

// O tipo do Mod.Logger do tModLoader (o ILog do log4net), com os membros que os
// mods usam. Aqui nao ha log4net: cada linha vai para o log da sessao e para o
// logcat, com o nome do mod na frente.
public interface ILog
{
    bool IsDebugEnabled { get; }

    bool IsInfoEnabled { get; }

    bool IsWarnEnabled { get; }

    bool IsErrorEnabled { get; }

    bool IsFatalEnabled { get; }

    void Debug(object message);

    void Debug(object message, Exception exception);

    void DebugFormat(string format, params object[] args);

    void Info(object message);

    void Info(object message, Exception exception);

    void InfoFormat(string format, params object[] args);

    void Warn(object message);

    void Warn(object message, Exception exception);

    void WarnFormat(string format, params object[] args);

    // Error e Fatal abrem o painel de erro dentro do jogo.
    void Error(object message);

    void Error(object message, Exception exception);

    void ErrorFormat(string format, params object[] args);

    void Fatal(object message);

    void Fatal(object message, Exception exception);

    void FatalFormat(string format, params object[] args);
}
