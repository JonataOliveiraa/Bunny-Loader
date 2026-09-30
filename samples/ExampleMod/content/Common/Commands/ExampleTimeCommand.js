const { MessageID } = Terraria.ID;

// "/addtime <ticks>": avança ou volta a hora do mundo (o ExampleTimeCommand do
// tModLoader). Tipo World: no chat sozinho, e no servidor no multijogador,
// que manda a hora nova a todos.
export class ExampleTimeCommand extends ModCommand {
    get Command() { return 'addTime'; }
    get Type() { return CommandType.World; }

    Action(caller, input, args) {
        const Main = Terraria.Main;
        if (!args.length) throw new UsageException(ModLocalization.Translate('Commands.Common.NoParameters'));

        const extra = Number(args[0]);
        if (!Number.isInteger(extra)) {
            throw new UsageException(ModLocalization.Translate('Commands.Common.NotInteger').replace('{0}', args[0]));
        }

        // O dia (0 a 54000) e a noite (0 a 32400) num ciclo só (0 a 86400).
        const day = Main.dayLength, cycle = Main.dayLength + Main.nightLength;
        let time = Main.time + (Main.dayTime ? 0 : day) + extra;
        time = ((time % cycle) + cycle) % cycle;
        Main.dayTime = time < day;
        Main.time = Main.dayTime ? time : time - day;

        if (Main.netMode === 2) Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'](
            MessageID.WorldData, -1, -1, null, 0, 0, 0, 0, 0, 0, 0);
    }
}
