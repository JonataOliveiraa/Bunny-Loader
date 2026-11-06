using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;

namespace Terraria.ModLoader;

public sealed record InvasionWave(int Points, params InvasionEnemy[] Enemies);

public readonly record struct InvasionEnemy(int Type, float Weight = 1, int Points = 1);

public abstract class ModInvasion : ModType, ILocalizedModType
{
    public int Type { get; internal set; }

    public virtual string LocalizationCategory => "Invasions";

    public virtual LocalizedText DisplayName => Language.GetOrRegister(this.GetLocalizationKey(nameof(DisplayName)), () => Regex.Replace(Name, "([a-z0-9])([A-Z])", "$1 $2"));

    public virtual string IconTexture => (GetType().Namespace + "." + Name).Replace('.', '/') + "_Icon";

    public virtual Color MessageColor => new Color(175, 75, 255);

    public virtual int Music => -1;

    public abstract IList<InvasionWave> Waves { get; }

    public virtual int SpawnRate => 60;

    public virtual int MaxSpawns => 8;

    public virtual bool CanStart() => true;

    public virtual bool CanSpawn(Player player) => player.ZoneOverworldHeight;

    public virtual bool ShouldFail() => false;

    public virtual int PointsFor(NPC npc, InvasionWave wave)
    {
        foreach (InvasionEnemy enemy in wave.Enemies)
            if (enemy.Type == npc.type)
                return enemy.Points;
        return 0;
    }

    public virtual int ScalePoints(int points, int players) => Math.Max(1, (int)Math.Round(points * (1 + 0.5 * Math.Max(0, players - 1))));

    public virtual void OnStart()
    {
    }

    public virtual void OnWaveStart(int wave)
    {
    }

    public virtual void OnWaveComplete(int wave)
    {
    }

    public virtual void OnComplete()
    {
    }

    public virtual void OnFail()
    {
    }

    public virtual void Update()
    {
    }

    protected sealed override void Register() => InvasionLoader.Register(this);

    public sealed override void SetupContent() => SetStaticDefaults();
}
