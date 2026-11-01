# Title menu themes

A `ModMenu` changes the title screen while the game is in its menus: the
logo, the sun, the moon, the background and the music. Declare the class and
it registers itself.

```csharp
public class ExampleModMenu : ModMenu
{
    private const string menuAssetPath = "ExampleMod/Assets/Textures/Menu";

    private Asset<Texture2D> logoTexture = null!;

    public override void Load() => logoTexture = ModContent.Request<Texture2D>($"{menuAssetPath}/ExampleModTitle");

    public override Asset<Texture2D> Logo => logoTexture;

    public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Music/MysteriousMystery");

    public override ModSurfaceBackgroundStyle MenuBackgroundStyle => ModContent.GetInstance<ExampleSurfaceBackgroundStyle>();

    public override string DisplayName => "Example ModMenu";
}
```

## Choosing the theme

The player picks the theme in **Settings → General → Menu Theme**: each tap
moves to the next available theme, the game's own ("Terraria") included,
and the title changes behind the settings at once. The choice is kept in
`BunnyLoader.menu.json` in the game's save folder. Without a choice, or when
the chosen theme is gone or not available, the last mod theme that is
available wins; without any, the game's title. tModLoader's footer button is
not used.

`IsAvailable` false hides a theme; `IsSelected` tells whether it is the one in
use; `OnSelected` and `OnDeselected` run when it changes; `Update(isOnTitleScreen)`
runs every menu frame (`isOnTitleScreen` on the main title, `Main.menuMode == 0`).
`MenuLoader.CurrentMenu` is the theme in use.

## What a theme changes

- `Logo`: drawn where the game draws its logo, with the game's position,
  rotation and scale; `null` keeps the game's logo.
  `PreDrawLogo(spriteBatch, ref logoDrawCenter, ref logoRotation, ref logoScale, ref drawColor)`
  can change them or return false to skip the logo; `PostDrawLogo` runs after
  it with the values used. The color starts as the game's day and night logo
  colors together.
- `SunTexture` and `MoonTexture`: drawn by the game in place of its own, in
  the menus only (the moon is one frame); `null` keeps the game's.
- `MenuBackgroundStyle`: a `ModSurfaceBackgroundStyle` whose layers fill the
  title's background (see [Backgrounds](13-biomas.md#backgrounds)). The
  special seeds keep the game's background.
- `Music`: a music slot. The default (`MusicID.TitleJourneysBeginningWithIntro`,
  as in tModLoader) and `-1` keep the game's title music; a mod track or
  another of the game's tracks plays on the title, except while generating a
  world with a special seed and during the credits.

`DisplayName` (the mod's name by default) is the theme's name in the settings.

The `csmusicmenu` pack checks the theme choice and switching, `Update`, the
logo hooks, the background and the music.
