using System;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;

namespace Terraria.ModLoader.Config;

public abstract class ModConfig : ILocalizedModType, ILoadable
{
    public Mod Mod { get; internal set; } = null!;

    public string Name { get; internal set; } = "";

    public string FullName => $"{Mod.Name}/{Name}";

    public virtual string LocalizationCategory => "Configs";

    public virtual LocalizedText DisplayName => Language.GetOrRegister(this.GetLocalizationKey(nameof(DisplayName)), () => Regex.Replace(Name, "([A-Z])", " $1").Trim());

    public abstract ConfigScope Mode { get; }

    public virtual bool Autoload(ref string name) => Mod.ContentAutoloadingEnabled;

    public virtual void OnLoaded()
    {
    }

    public virtual void OnChanged()
    {
    }

    public virtual bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message) => true;

    public virtual void HandleAcceptClientChangesReply(bool success, int player, NetworkText message)
    {
    }

    public virtual ModConfig Clone() => (ModConfig)MemberwiseClone();

    public virtual bool NeedsReload(ModConfig pendingConfig)
    {
        foreach (ConfigOption option in ConfigManager.OptionsOf(GetType()))
            if (option.ReloadRequired && !Equals(option.Get(this), option.Get(pendingConfig)))
                return true;
        return false;
    }

    public ConfigSaveResult SaveChanges(ModConfig? pendingConfig = null, Action<string, Color>? status = null, bool silent = true, bool broadcast = true) =>
        ConfigManager.SaveChanges(this, pendingConfig ?? this, status, silent, broadcast);

    bool ILoadable.IsLoadingEnabled(Mod mod) => true;

    void ILoadable.Load(Mod mod)
    {
        Mod = mod;
        string name = GetType().Name;
        if (!Autoload(ref name))
            return;
        Name = name;
        ConfigManager.Add(this);
    }

    void ILoadable.Unload()
    {
    }
}
