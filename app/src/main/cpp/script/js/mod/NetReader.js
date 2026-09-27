class NetReader {
    constructor(values) {
        this.values = Array.isArray(values) ? values : [];
        this.index = 0;
    }

    get HasMore() { return this.index < this.values.length; }

    Read() {
        if (this.index >= this.values.length) {
            throw new RangeError('NetReader: leu mais do que o outro lado escreveu (' + this.values.length + ' valores)');
        }
        return this.values[this.index++];
    }

    ReadInt32() { return Number(this.Read()) | 0; }
    ReadSingle() { return Number(this.Read()); }
    ReadBoolean() { return !!this.Read(); }
    ReadString() { return String(this.Read()); }
    ReadFlags() { return this.Read(); }

    ReadVector2() {
        const v = this.Read();
        return Vector2.new(v.X, v.Y);
    }
}

for (const name of ['ReadByte', 'ReadSByte', 'ReadInt16', 'ReadUInt16', 'ReadUInt32', 'Read7BitEncodedInt']) {
    NetReader.prototype[name] = NetReader.prototype.ReadInt32;
}
NetReader.prototype.ReadInt64 = NetReader.prototype.ReadSingle;
NetReader.prototype.ReadDouble = NetReader.prototype.ReadSingle;
