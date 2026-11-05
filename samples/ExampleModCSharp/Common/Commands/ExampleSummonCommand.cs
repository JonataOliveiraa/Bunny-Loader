using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.Commands
{
    public class ExampleSummonCommand : ModCommand
    {
        public static LocalizedText UsageText { get; private set; }
        public static LocalizedText DescriptionText { get; private set; }
        public static LocalizedText[] ErrorText { get; private set; }

        public override void SetStaticDefaults() {
            string key = $"Commands.{nameof(ExampleSummonCommand)}.";
            UsageText = Mod.GetLocalization($"{key}Usage");
            DescriptionText = Mod.GetLocalization($"{key}Description");
            ErrorText = Enumerable.Range(0, 5).Select(i => Mod.GetLocalization($"{key}Error_{i}")).ToArray();
        }

        public override CommandType Type
            => CommandType.World;

        public override string Command
            => "summon";

        public override string Usage
            => UsageText.Value;

        public override string Description
            => DescriptionText.Value;

        public override void Action(CommandCaller caller, string input, string[] args) {
            
            if (args.Length == 0) {
                throw new UsageException(ErrorText[0].Value);
            }
            if (!int.TryParse(args[0], out int type)) {
                throw new UsageException(ErrorText[1].Format(args[0]));
            }

            int xSpawnPosition = (int)caller.Player.Bottom.X;
            int ySpawnPosition = (int)caller.Player.Bottom.Y;
            int numToSpawn = 1;
            bool relativeX = false;
            bool relativeY = false;

            if (args.Length > 1) {
                
                if (args[1].StartsWith('~')) {
                    relativeX = true;
                    args[1] = args[1].Substring(1);
                }
                
                if (!int.TryParse(args[1], out xSpawnPosition)) {
                    throw new UsageException(ErrorText[2].Format(args[1]));
                }
            }

            if (args.Length > 2) {
                
                if (args[2].StartsWith('~')) {
                    relativeY = true;
                    args[2] = args[2].Substring(1);
                }
                
                if (!int.TryParse(args[2], out ySpawnPosition)) {
                    throw new UsageException(ErrorText[3].Format(args[2]));
                }
            }

            if (relativeX) {
                xSpawnPosition += (int)caller.Player.Bottom.X;
            }
            if (relativeY) {
                ySpawnPosition += (int)caller.Player.Bottom.Y;
            }

            if (args.Length > 3) {
                if (!int.TryParse(args[3], out numToSpawn)) {
                    throw new UsageException(ErrorText[4].Format(args[3]));
                }
            }

            for (int k = 0; k < numToSpawn; k++) {
                
                int slot = NPC.NewNPC(Terraria.DataStructures.EntitySource_DebugCommand.New(), xSpawnPosition, ySpawnPosition, type);

                if ((Main.netMode & NetmodeID.Server) != 0 && slot < Main.maxNPCs) {
                    NetMessage.SendData(MessageID.SyncNPC, number: slot);
                }
            }
        }
    }
}
