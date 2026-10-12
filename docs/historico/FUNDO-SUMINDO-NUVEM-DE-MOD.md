# The background that vanishes (only the blue sky) with a mod cloud

Round of 2026-09-30, on the user's Galaxy A15, with the Example Mod. Sometimes
the whole surface background vanished and only the sky remained, until the
game was closed. Tools: [`tools/tests/bgwatch`](../../tools/tests/bgwatch)
(watches the background), [`tools/tests/cloudforce`](../../tools/tests/cloudforce)
(forces a mod cloud on screen) and the phone's logcat over ZeroTier. Log
excerpt: [`dados/2026-09-30/fundo-sumindo-nuvem-de-mod.log`](dados/2026-09-30/fundo-sumindo-nuvem-de-mod.log).

> **Confirmed on 2026-10-10** by [`tools/tests/horizonclouds`](../../tools/tests/horizonclouds),
> on MuMu with `NextHorizonRenderer` on: with 13 mod clouds on screen, the
> three cloud passes close (`CloudsEnd` 3 per frame) and the background draws
> the 4 layers, as without a mod cloud. Without the mod cloud's mask (the state
> before the fix), the defect comes back: `CloudsEnd` 0, 1 layer, only the sky
> on screen.

## What happened

1. Mobile's horizon drawing (`NextHorizonRenderer.DrawCloud`, the
   "HorizonShaders" option) reads `TextureMaskManager.CloudMasks[cloud.type]`
   **without checking** the array's bounds. The mask says which 16 × 16 pieces
   of the cloud cover the sun (with `Main.SunMask`).
2. `CloudLoader` grew `TextureAssets.Cloud` for the mod clouds (types after
   `CloudID.Count`), but not `CloudMasks`. With a mod cloud on screen, the read
   went past the end of the array and gave a `NullReferenceException`
   (non-null garbage there would crash the game).
3. The exception left in the middle of drawing with `SpriteBatch_Cloud` open.
   Every following frame, `CloudsStart` tried to open the batch again
   (`Begin cannot be called again until End has been successfully called`)
   and `DrawSurfaceBG` stopped there: only the sky.

It had never shown up on MuMu because the mod cloud has a small weight in the
roll. The horizon drawing is on there too (`Main.HorizonShadersEnabled`),
contrary to what was thought.

## The fix

`CloudLoader.#GrowMasks`: when the clouds are installed (after
`XNAUnityRunner.Awake`, which loads the masks), `CloudMasks` grows to the mod
types and each mod cloud gets a `TextureMask` with the texture's size and
`outputWidth` 0: `IsSolid` always returns false, the cloud is drawn and just
does not darken the sun. And a cloud whose texture did not load no longer goes
into the roll (a null `TextureAssets.Cloud` would break the same drawing).

## For the next table

The phone has per-type tables the PC does not (per-texture masks,
`CloudMasks`, `BackgroundMasks`) and reads them without checking bounds. Mod
content that gets a new type has to grow all of them, not just the texture's.
