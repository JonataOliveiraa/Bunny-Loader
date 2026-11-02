using Microsoft.Xna.Framework;
using Terraria.Graphics.Effects;

namespace Terraria;

public readonly partial struct Player
{
    public void ManageSpecialBiomeVisuals(string biomeName, bool inZone, Vector2 activationSource = default)
    {
        SkyManager skies = SkyManager.Instance;
        CustomSky sky = skies[biomeName];
        if (!sky.IsNull && inZone != sky.IsActive())
        {
            if (inZone) skies.Activate(biomeName, activationSource);
            else skies.Deactivate(biomeName);
        }
        Filter filter = Filters.Scene[biomeName];
        if (!filter.IsNull)
        {
            if (inZone != filter.IsActive())
            {
                if (inZone) Filters.Scene.Activate(biomeName, activationSource);
                else Filters.Scene.Deactivate(biomeName);
            }
            else if (inZone)
            {
                filter.GetShader().UseTargetPosition(activationSource);
            }
        }
        Overlay overlay = Overlays.Scene[biomeName];
        if (!overlay.IsNull && inZone != (overlay.Mode != OverlayMode.Inactive))
        {
            if (inZone) Overlays.Scene.Activate(biomeName, activationSource);
            else Overlays.Scene.Deactivate(biomeName);
        }
    }
}
