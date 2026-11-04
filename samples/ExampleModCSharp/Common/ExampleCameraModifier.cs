using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using ICameraModifier = Terraria.Graphics.CameraModifiers.IModCameraModifier;

namespace ExampleMod.Common
{

    public class ExampleCameraModifier : ICameraModifier
    {
        private int framesToLast;
        private int framesElapsed;
        public Vector2 targetPosition;

        public string UniqueIdentity { get; private set; }
        public bool Finished { get; private set; }
        public bool IsAScreenShake => false;

        public ExampleCameraModifier(Vector2 position, int frames, string uniqueIdentity = null) {
            targetPosition = position - new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);
            framesToLast = frames;
            UniqueIdentity = uniqueIdentity;
        }

        public void Update(ref CameraInfo cameraInfo) {

            float progress = Utils.GetLerpValue(0, framesToLast, framesElapsed);

            float lerpAmount = progress switch {
                < 0.5f => Utils.Remap(progress, 0, 0.5f, 0, 1),
                > 0.8f => Utils.Remap(progress, 0.8f, 1f, 1, 0),
                _ => 1,
            };

            cameraInfo.CameraPosition = Vector2.Lerp(cameraInfo.CameraPosition, targetPosition, lerpAmount);

            if (FocusHelper.GameplayActive && !Main.gamePaused) {
                framesElapsed++;
            }

            if (framesElapsed >= framesToLast) {
                Finished = true;
            }
        }
    }
}
