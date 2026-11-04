using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.CustomModType
{

    public abstract class ModVictoryPose : ModTexturedType, ILocalizedModType
    {

        public int Type { get; internal set; }

        public int PoseTime { get; set; } = 60;

        public virtual string LocalizationCategory => "VictoryPoses";
        public virtual LocalizedText VictoryCheer => this.GetLocalization(nameof(VictoryCheer), PrettyPrintName);

        protected sealed override void Register() {
            ModTypeLookup<ModVictoryPose>.Register(this);
            Type = VictoryPoseLoader.Add(this);
        }

        public sealed override void SetupContent() {
            ModContent.Request<Texture2D>(Texture);
            _ = VictoryCheer;
            VictoryPoseID.Search.Add(FullName, Type);
            SetStaticDefaults();
        }

        public static float ElapsedPoseTime(Player player) => player.GetModPlayer<VictoryPosePlayer>().ElapsedPoseTime;

        public virtual void OnStartPose(Player player) {
        }

        public virtual void Update(Player player) {
        }

        public virtual void OnEndPose(Player player) {
        }

        public virtual Rectangle? GetTextureFrame(Asset<Texture2D> texture) {
            return null;
        }

    }
}
