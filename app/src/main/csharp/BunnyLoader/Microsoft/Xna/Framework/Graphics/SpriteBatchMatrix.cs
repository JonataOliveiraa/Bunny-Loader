using BunnyLoader;
using BunnyLoader.Interop;
using Terraria;
using Terraria.Graphics;

namespace Microsoft.Xna.Framework.Graphics;

internal static unsafe class SpriteBatchMatrix
{
    static readonly int* Pending = Host.Native()->SpriteMatrixPending();

    static uint cachedUpdate;
    static bool cached;
    static bool warned;
    static Matrix gameTransform, uiScale, viewTransform, viewZoom;

    internal static void Begin(SpriteBatch batch, SpriteSortMode sortMode, GameNullable<Matrix> transformMatrix)
    {
        if (batch.IsNull) return;

        if (!transformMatrix.HasValue)
        {
            if (*Pending != 0) Host.Native()->SpriteMatrixDrop(batch.Pointer);
            return;
        }

        Matrix matrix = transformMatrix.Value;
        if (FromGame(in matrix))
        {
            if (*Pending != 0) Host.Native()->SpriteMatrixDrop(batch.Pointer);
            return;
        }

        float* m = stackalloc float[6] { matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M41, matrix.M42 };
        int result = Host.Native()->SpriteMatrixBegin(batch.Pointer, m, sortMode == SpriteSortMode.Immediate ? 1 : 0);

        if (result <= 0 && !warned)
        {
            warned = true;
            Log.Warn(result < 0
                ? "SpriteBatch: more than 16 batches with a matrix are open at once; the others are drawn without their matrix"
                : "SpriteBatch: the matrix passed to Begin cannot be applied on this game version");
        }
    }

    internal static bool TryPending(SpriteBatch batch, out Matrix matrix)
    {
        matrix = default;
        if (*Pending == 0 || batch.IsNull) return false;

        float* m = stackalloc float[6];
        if (Host.Native()->SpriteMatrixGet(batch.Pointer, m) == 0) return false;

        matrix = new Matrix(m[0], m[1], 0f, 0f, m[2], m[3], 0f, 0f, 0f, 0f, 1f, 0f, m[4], m[5], 0f, 1f);
        return true;
    }

    static bool FromGame(in Matrix matrix)
    {
        if (matrix == Matrix.Identity) return true;

        uint update = Main._gameUpdateCount;
        if (!cached || update != cachedUpdate)
        {
            gameTransform = Main.Transform;
            uiScale = Main.UIScaleMatrix;
            SpriteViewMatrix view = Main.GameViewMatrix;
            viewTransform = view.IsNull ? Matrix.Identity : view.TransformationMatrix;
            viewZoom = view.IsNull ? Matrix.Identity : view.ZoomMatrix;
            cachedUpdate = update;
            cached = true;
        }

        return matrix == gameTransform || matrix == uiScale || matrix == viewTransform || matrix == viewZoom;
    }
}
