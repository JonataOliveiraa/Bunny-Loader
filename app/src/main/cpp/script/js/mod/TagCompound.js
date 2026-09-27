class TagCompound {
    get Count() { return Object.keys(this).length; }

    ContainsKey(key) { return Object.prototype.hasOwnProperty.call(this, key); }
    Get(key, fallback) { return this.ContainsKey(key) ? this[key] : fallback; }
    Set(key, value) { this[key] = value; }
    Add(key, value) { this[key] = value; }
    Remove(key) { delete this[key]; }

    GetBool(key) { return !!this.Get(key, false); }
    GetInt(key) { return Number(this.Get(key, 0)) | 0; }
    GetFloat(key) { return Number(this.Get(key, 0)); }
    GetString(key) { return String(this.Get(key, '')); }
    GetList(key) { return Array.isArray(this[key]) ? this[key] : []; }
    GetCompound(key) { return TagCompound.from(this[key]); }

    static from(data) {
        return Object.assign(new TagCompound(), data && typeof data === 'object' ? data : {});
    }
}
