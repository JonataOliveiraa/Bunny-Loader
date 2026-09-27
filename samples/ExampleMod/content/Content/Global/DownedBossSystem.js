export class DownedBossSystem extends ModSystem {
    static downedExampleBoss = false;

    ClearWorld() {
        DownedBossSystem.downedExampleBoss = false;
    }

    SaveWorldData(tag) {
        if (DownedBossSystem.downedExampleBoss) tag.downedExampleBoss = true;
    }

    LoadWorldData(tag) {
        DownedBossSystem.downedExampleBoss = tag.ContainsKey('downedExampleBoss');
    }

    NetSend(writer) {
        writer.WriteFlags(DownedBossSystem.downedExampleBoss);
    }

    NetReceive(reader) {
        [DownedBossSystem.downedExampleBoss] = reader.ReadFlags();
    }
}
