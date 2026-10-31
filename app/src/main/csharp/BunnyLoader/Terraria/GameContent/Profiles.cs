using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace Terraria.GameContent;

public class Profiles
{
    public class StackedNPCProfile : ITownNPCProfile
    {
        internal ITownNPCProfile[] _profiles;

        public StackedNPCProfile(params ITownNPCProfile[] profilesInOrderOfVariants) => _profiles = profilesInOrderOfVariants;

        ITownNPCProfile Of(NPC npc)
        {
            int index = npc.townNpcVariationIndex;
            return _profiles[(uint)index < (uint)_profiles.Length ? index : 0];
        }

        public int RollVariation() => 0;

        public string GetNameForVariant(NPC npc) => Of(npc).GetNameForVariant(npc);

        public Asset<Texture2D> GetTextureNPCShouldUse(NPC npc) => Of(npc).GetTextureNPCShouldUse(npc);

        public int GetHeadTextureIndex(NPC npc) => Of(npc).GetHeadTextureIndex(npc);
    }

    public class DefaultNPCProfile : ITownNPCProfile
    {
        internal string _rootFilePath;
        internal int _defaultVariationHeadIndex;
        internal Asset<Texture2D> _defaultNoAlt;
        internal Asset<Texture2D> _defaultParty;

        public DefaultNPCProfile(string texture, int headIndex, string? partyTexture = null)
        {
            _rootFilePath = texture;
            _defaultVariationHeadIndex = headIndex;
            _defaultNoAlt = ModContent.Request<Texture2D>(texture);
            _defaultParty = partyTexture != null ? ModContent.Request<Texture2D>(partyTexture) : _defaultNoAlt;
        }

        public int RollVariation() => 0;

        public string GetNameForVariant(NPC npc) => NPC.getNewNPCName(npc.type) ?? "";

        public Asset<Texture2D> GetTextureNPCShouldUse(NPC npc)
        {
            if (npc.IsABestiaryIconDummy && !npc.ForcePartyHatOn) return _defaultNoAlt;
            return npc.altTexture == 1 ? _defaultParty : _defaultNoAlt;
        }

        public int GetHeadTextureIndex(NPC npc) => _defaultVariationHeadIndex;
    }
}
