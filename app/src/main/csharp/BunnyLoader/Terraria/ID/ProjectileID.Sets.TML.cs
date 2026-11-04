using System;
using Terraria.ModLoader;

namespace Terraria.ID;

public readonly partial struct ProjectileID
{
    public static partial class Sets
    {
        static bool[] explosives = Array.Empty<bool>();
        public static bool[] Explosive
        {
            get
            {
                if (explosives.Length < ProjectileLoader.ProjectileCount)
                {
                    bool first = explosives.Length == 0;
                    Array.Resize(ref explosives, ProjectileLoader.ProjectileCount);
                    if (first) foreach (int type in new[] { Bomb, Dynamite, Grenade, StickyBomb, HappyBomb, BombSkeletronPrime, Explosives, GrenadeI, RocketI, ProximityMineI, GrenadeII, RocketII, ProximityMineII, GrenadeIII, RocketIII, ProximityMineIII, GrenadeIV, RocketIV, ProximityMineIV, Landmine, RocketSkeleton, RocketSnowmanI, RocketSnowmanII, RocketSnowmanIII, RocketSnowmanIV, StickyGrenade, StickyDynamite, BouncyBomb, BouncyGrenade, BombFish, PartyGirlGrenade, BouncyDynamite, DD2GoblinBomb, ScarabBomb, ClusterRocketI, ClusterGrenadeI, ClusterMineI, ClusterFragmentsI, ClusterRocketII, ClusterRocketII, ClusterMineII, ClusterFragmentsII, WetRocket, WetGrenade, WetMine, LavaRocket, LavaGrenade, LavaMine, HoneyRocket, HoneyGrenade, HoneyMine, MiniNukeRocketI, MiniNukeGrenadeI, MiniNukeMineI, MiniNukeRocketII, MiniNukeGrenadeII, MiniNukeMineII, DryRocket, DryGrenade, DryMine, ClusterSnowmanRocketI, ClusterSnowmanRocketII, WetSnowmanRocket, LavaSnowmanRocket, HoneySnowmanRocket, MiniNukeSnowmanRocketI, MiniNukeSnowmanRocketII, DrySnowmanRocket, ClusterSnowmanFragmentsI, ClusterSnowmanFragmentsII, WetBomb, LavaBomb, HoneyBomb, DryBomb, DirtBomb, DirtStickyBomb, SantankMountRocket, TNTBarrel, FreezeBomb, SuperBomb, SuperStickyBomb, AcornSlingshotAcorn }) explosives[type] = true;
                }
                return explosives;
            }
        }
    }
}
