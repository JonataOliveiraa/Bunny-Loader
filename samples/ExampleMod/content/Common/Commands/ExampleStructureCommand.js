import { ExampleStructure } from '../../Content/Structures/ExampleStructure.js';

const SendTileSquare = Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'];

// "/castle": constrói o castelo de exemplo no chão embaixo do jogador, para
// ver a estrutura sem criar um mundo novo. Tipo World: no multijogador quem
// constrói é o servidor, que manda os blocos novos a todos.
export class ExampleStructureCommand extends ModCommand {
    get Command() { return 'castle'; }
    get Type() { return CommandType.World; }
    get Description() { return ModLocalization.Translate('Commands.ExampleStructureCommand.Description'); }

    Action(caller, input, args) {
        const Main = Terraria.Main;
        const player = caller.Player;
        const x = Math.floor(player.Center.X / 16);
        const feet = Math.floor((player.position.Y + player.height) / 16);

        const ground = ExampleStructure.FindGround(x, feet, Math.min(feet + 50, Main.maxTilesY - 50));
        if (ground < 0) throw new UsageException(ModLocalization.Translate('Commands.ExampleStructureCommand.NoGround'));

        const area = ExampleStructure.Place(x, ground);

        // A área mais a terra que pode ter ido embaixo da fundação.
        if (Main.netMode & 2) SendTileSquare(-1, area.X - 1, area.Y - 1, area.Width + 2, area.Height + 32, 0);
        caller.Reply(ModLocalization.Translate('Commands.ExampleStructureCommand.Built'));
    }
}
