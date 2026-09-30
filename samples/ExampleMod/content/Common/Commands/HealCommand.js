// "/heal <quantidade>" (ou "/curar"): cura quem digitou. O do ExMod. Tipo Chat:
// roda no aparelho de quem digitou, sozinho ou no multijogador.
export class HealCommand extends ModCommand {
    get Command() { return 'heal'; }
    get Aliases() { return ['curar']; }
    get Type() { return CommandType.Chat; }

    Action(caller, input, args) {
        const amount = Math.floor(Number(args[0]));
        if (!args.length || !Number.isFinite(amount) || amount <= 0) {
            throw new UsageException(ModLocalization.Translate('Commands.HealCommand.Error'));
        }

        const player = caller.Player;
        const healed = Math.min(player.statLifeMax2, player.statLife + amount) - player.statLife;
        if (healed <= 0) {
            caller.Reply(ModLocalization.Translate('Commands.HealCommand.Full'), ModCommand.ResponseColor);
            return;
        }
        player.statLife += healed;
        player.HealEffect(healed, true);
        caller.Reply(ModLocalization.Translate('Commands.HealCommand.Healed').replace('{0}', healed), Color.LawnGreen);
    }
}
