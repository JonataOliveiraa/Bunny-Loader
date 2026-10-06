class SystemNetworkHooks {
    static Install(cls) {
        if (Hooks.Overrides(cls, ModSystem, 'HijackSendData')) ModNet.InstallEntity();
        if (!Hooks.Overrides(cls, ModSystem, 'HijackGetData')) return;
        Hooks.Once('system.GetData', () => {
            Terraria.MessageBuffer['void ProcessData(byte[] messageData, int length, out int messageType)'].hook(
                (original, self, data, length, typeOut) => {
                    if (!data || length < 1 || length > data.length) return original(self, data, length, typeOut);
                    const stream = System.IO.MemoryStream.new();
                    stream['void .ctor(byte[] buffer, int index, int count)'](data, 0, length);
                    const reader = System.IO.BinaryReader.new();
                    reader['void .ctor(Stream input)'](stream);
                    stream.Position = 1;
                    const type = new Ref(data[0]), input = new Ref(reader);
                    let hijacked = false;
                    for (const entry of SystemLoader.Entries('HijackGetData')) {
                        try {
                            if (entry.instance.HijackGetData(type, input, self.whoAmI) === true) hijacked = true;
                            if (input.value) input.value.BaseStream.Position = 1;
                        } catch (error) { Safe.Report(entry.label, error); input.value = reader; stream.Position = 1; }
                    }
                    const validType = Number.isInteger(type.value) && type.value >= 0 && type.value <= 255;
                    if (hijacked) { typeOut.value = validType ? type.value : data[0]; return; }
                    if (!validType || !input.value) {
                        return original(self, data, length, typeOut);
                    }
                    if (input.value !== reader) {
                        try {
                            const payload = input.value['byte[] ReadBytes(int count)'](65534);
                            const replacement = System.Byte.newArray(payload.length + 1);
                            replacement[0] = type.value;
                            System.Array['void Copy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)'](payload, 0, replacement, 1, payload.length);
                            return original(self, replacement, replacement.length, typeOut);
                        } catch (error) { Safe.Report('ModSystem.HijackGetData.reader', error); return original(self, data, length, typeOut); }
                    }
                    if (type.value === data[0]) return original(self, data, length, typeOut);
                    const replacement = System.Byte.newArray(length);
                    System.Array['void Copy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)'](data, 0, replacement, 0, length);
                    replacement[0] = type.value;
                    return original(self, replacement, length, typeOut);
                });
        });
    }

    static HijackSend(msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7) {
        if (!SystemLoader.Entries('HijackSendData').length) return false;
        if (Terraria.Main.netMode === 0) return false;
        const who = Terraria.Main.netMode === 2 && remote >= 0 ? remote : 256;
        return SystemLoader.Any('HijackSendData', who, msgType, remote, ignore, text, number, n2, n3, n4, n5, n6, n7);
    }
}
