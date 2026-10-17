// A lista do ModifyInterfaceLayers: um Array com os nomes do List<> do C#
// (FindIndex, Insert, RemoveAt, Count...), na ordem em que as camadas desenham.
class InterfaceLayerList extends Array {
    static get [Symbol.species]() { return Array; }

    get Count() { return this.length; }

    FindIndex(match) { return this.findIndex(match); }
    FindLastIndex(match) { return this.findLastIndex(match); }
    Find(match) { return this.find(match); }
    IndexOf(layer) { return this.indexOf(layer); }
    Contains(layer) { return this.includes(layer); }
    Add(layer) { this.push(layer); }
    AddRange(layers) { for (const l of layers) this.push(l); }
    Insert(index, layer) { this.splice(index, 0, layer); }
    InsertRange(index, layers) { this.splice(index, 0, ...layers); }
    RemoveAt(index) { this.splice(index, 1); }
    RemoveAll(match) {
        let removed = 0;
        for (let i = this.length - 1; i >= 0; i--) {
            if (match(this[i])) {
                this.splice(i, 1);
                removed++;
            }
        }
        return removed;
    }

    Remove(layer) {
        const i = this.indexOf(layer);
        if (i < 0) return false;
        this.splice(i, 1);
        return true;
    }
}
