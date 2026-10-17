// Um evento de UIElement (o `event` do C#): `el.OnLeftClick.Add(fn)` é o
// `+=`, `Remove(fn)` o `-=`. Atribuir uma função (`el.OnLeftClick = fn`)
// troca todas as que havia por ela.
class UIElementEvent {
    #handlers = [];

    Add(fn) {
        if (typeof fn !== 'function') throw new TypeError('UIElement event: Add(fn) expects a function');
        this.#handlers.push(fn);
        return this;
    }

    Remove(fn) {
        const i = this.#handlers.lastIndexOf(fn);
        if (i >= 0) this.#handlers.splice(i, 1);
        return this;
    }

    Clear() {
        this.#handlers.length = 0;
    }

    get Count() { return this.#handlers.length; }

    Invoke(...args) {
        if (this.#handlers.length === 0) return;
        for (const fn of [...this.#handlers]) fn(...args);
    }
}
