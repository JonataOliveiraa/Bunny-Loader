class NetWriter {
    constructor() {
        this.values = [];
    }

    Write(value) {
        const isVector = value && typeof value === 'object' && !Array.isArray(value) &&
            'X' in value && 'Y' in value && typeof value.X === 'number';
        this.values.push(isVector ? { X: value.X, Y: value.Y } : value);
    }

    WriteFlags(...flags) { this.values.push(flags.map(Boolean)); }
    WriteVector2(v) { this.values.push({ X: v.X, Y: v.Y }); }
}

for (const name of ['WriteByte', 'WriteSByte', 'WriteInt16', 'WriteUInt16', 'WriteInt32', 'WriteUInt32',
                    'WriteInt64', 'WriteSingle', 'WriteDouble', 'WriteBoolean', 'WriteString', 'Write7BitEncodedInt']) {
    NetWriter.prototype[name] = NetWriter.prototype.Write;
}
