// Quem recebe é o HandlePacket(reader, whoAmI) do mesmo mod, do outro lado.
class ModPacket extends NetWriter {
    constructor(mod) {
        super();
        this.mod = mod;
    }

    // Do cliente vai ao servidor; do servidor, a um cliente ou a todos menos ignoreClient.
    Send(toClient = -1, ignoreClient = -1) {
        ModNet.Send({ k: 'packet', m: this.mod.uuid, d: this.values }, toClient, ignoreClient);
    }
}
