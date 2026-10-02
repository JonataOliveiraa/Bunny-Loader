package dev.bunnyloader.mods

/** Mantém a escolha do usuário; pacotes novos entram depois dos já ordenados. */
internal object ModOrder {
    fun resolve(installed: List<String>, saved: List<String>): List<String> {
        val available = installed.toSet()
        val kept = saved.filter { it in available }.distinct()
        val known = kept.toSet()
        return kept + (available - known).sorted()
    }

    fun move(order: List<String>, uid: String, direction: Int): List<String> {
        if (direction != -1 && direction != 1) return order
        val from = order.indexOf(uid)
        val to = from + direction
        if (from < 0 || to !in order.indices) return order
        return order.toMutableList().apply {
            val other = this[to]
            this[to] = this[from]
            this[from] = other
        }
    }

    fun moveTo(order: List<String>, uid: String, targetUid: String): List<String> {
        val from = order.indexOf(uid)
        val to = order.indexOf(targetUid)
        if (from < 0 || to < 0 || from == to) return order
        return order.toMutableList().apply { add(to, removeAt(from)) }
    }
}
