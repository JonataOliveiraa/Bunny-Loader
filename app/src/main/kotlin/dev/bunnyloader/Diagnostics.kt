package dev.bunnyloader

import android.util.Log
import java.lang.reflect.InvocationTargetException

const val TAG = "BunnyLoader"

/**
 * Reflection embrulha a exceção real em InvocationTargetException, cujo
 * getMessage() é null — foi exatamente isso que produziu o inútil
 * "Falha ao criar a UnityPlayer: null". Desembrulha até a causa de verdade.
 */
fun Throwable.rootCause(): Throwable {
    var current = this
    val seen = HashSet<Throwable>()
    while (seen.add(current)) {
        val next = when (current) {
            is InvocationTargetException -> current.targetException
            is ExceptionInInitializerError -> current.cause
            else -> null
        } ?: break
        current = next
    }
    return current
}

/** Ex.: "UnsatisfiedLinkError: dlopen failed: library libmain.so not found". */
fun Throwable.describe(): String {
    val root = rootCause()
    val name = root.javaClass.simpleName.ifEmpty { root.javaClass.name }
    return root.message?.let { "$name: $it" } ?: name
}

/** Loga a cadeia inteira, não só a ponta. */
fun logFailure(what: String, t: Throwable) {
    Log.e(TAG, "FALHA em $what -> ${t.describe()}", t)
    var cause: Throwable? = t
    var depth = 0
    while (cause != null && depth < 8) {
        Log.e(TAG, "  [causa $depth] ${cause.javaClass.name}: ${cause.message}")
        val next = if (cause is InvocationTargetException) cause.targetException else cause.cause
        if (next === cause) break
        cause = next
        depth++
    }
}
