package dev.bunnyloader.mods

/** Ativados primeiro; mantém a escolha do usuário e acrescenta novos pacotes dentro de cada grupo. */
internal object ModOrder {
    fun resolve(installed: List<String>, saved: List<String>, enabled: Set<String> = emptySet()): List<String> {
        val available = installed.toSet()
        val kept = saved.filter { it in available }.distinct()
        val known = kept.toSet()
        return enabledFirst(kept + (available - known).sorted(), enabled)
    }

    fun enabledFirst(order: List<String>, enabled: Set<String>): List<String> =
        order.filter { it in enabled } + order.filter { it !in enabled }

    fun dependenciesFirst(order: List<String>, ids: Map<String, String>, dependencies: Map<String, List<String>>): List<String> {
        val sorted = LinkedHashSet<String>()
        val visiting = mutableSetOf<String>()
        fun resolve(ref: String): String? = ref.takeIf { it in ids } ?: order.filter { ids[it] == ref }.singleOrNull()
        fun visit(uid: String) {
            if (uid in sorted || !visiting.add(uid)) return
            dependencies[uid].orEmpty().mapNotNull(::resolve).forEach(::visit)
            visiting.remove(uid)
            sorted += uid
        }
        order.forEach(::visit)
        return sorted.toList()
    }

    /** Quem acabou de ser ligado vai para o topo da lista (e carrega primeiro). */
    fun activate(order: List<String>, uid: String, enabled: Set<String>): List<String> {
        if (uid !in order || uid in enabled) return order
        return listOf(uid) + order.filter { it != uid }
    }

    fun move(order: List<String>, uid: String, direction: Int, enabled: Set<String> = emptySet()): List<String> {
        if (direction != -1 && direction != 1) return order
        val from = order.indexOf(uid)
        val to = from + direction
        if (from < 0 || to !in order.indices) return order
        if ((uid in enabled) != (order[to] in enabled)) return order
        return order.toMutableList().apply {
            val other = this[to]
            this[to] = this[from]
            this[from] = other
        }
    }

    fun moveTo(order: List<String>, uid: String, targetUid: String, enabled: Set<String> = emptySet()): List<String> {
        val from = order.indexOf(uid)
        val to = order.indexOf(targetUid)
        if (from < 0 || to < 0 || from == to) return order
        if ((uid in enabled) != (targetUid in enabled)) return order
        return order.toMutableList().apply { add(to, removeAt(from)) }
    }
}
