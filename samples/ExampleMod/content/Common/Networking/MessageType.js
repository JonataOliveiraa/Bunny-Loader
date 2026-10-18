// Os tipos de pacote do Example Mod (o ExampleMod.MessageType do
// tModLoader): o primeiro byte de cada ModPacket, lido no HandlePacket do main.js.
export class MessageType {
    static ExampleStatIncreasePlayerSync = 0;
    static ExampleTeleportToStatue = 1;
    static ExampleDodge = 2;
    static ExampleTownPetUnlockOrExchange = 3;
    static ExampleResourceEffect = 4;
    static StartVictoryPose = 5;
    static CancelVictoryPose = 6;
    static SendCustomUseStylePlayerDirection = 7;
}
