// O sorteio do spawn natural, como o pool do EditSpawnPool do tModLoader: tipo
// de NPC -> peso. O 0 é o spawn do jogo (peso 1); os de mod entram com o
// SpawnChance. Índice direto (pool[tipo] = peso, delete pool[0]) ou os métodos
// do IDictionary do C#.
class SpawnPool {
    get Count() { return Object.keys(this).length; }
    get Keys() { return Object.keys(this).map(Number); }

    Add(type, weight) {
        if (this.ContainsKey(type)) throw new Error('SpawnPool.Add: o tipo ' + type + ' já está no sorteio');
        this[type] = weight;
    }

    ContainsKey(type) { return Object.prototype.hasOwnProperty.call(this, type); }

    Remove(type) {
        const had = this.ContainsKey(type);
        delete this[type];
        return had;
    }

    Clear() {
        for (const key of Object.keys(this)) delete this[key];
    }

    // O tipo sorteado pelo Main.rand, ou null com o total 0 (nada nasce). Peso
    // negativo vale 0.
    Choose() {
        let total = 0;
        const entries = [];
        for (const key of Object.keys(this)) {
            const weight = Math.max(0, Number(this[key]) || 0);
            entries.push([Number(key), weight]);
            total += weight;
        }
        let r = Rand.NextFloat() * total;
        for (const [type, weight] of entries) {
            if (r < weight) return type;
            r -= weight;
        }
        return null;
    }
}
