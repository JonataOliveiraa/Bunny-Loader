using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader.Hooks;
using BunnyLoader.Interop;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader.Core;

namespace Microsoft.Xna.Framework.Graphics;

internal static unsafe class SpriteBatchMatrix
{
    struct Pending
    {
        public Matrix Matrix;
        public int Start;
        public bool Immediate;
    }

    static readonly int* Active = Awake();

    static readonly Dictionary<nint, Pending> pending = new();

    static int[]? vertices;

    static bool installed;

    static int* Awake()
    {
        int* flag = (int*)NativeMemory.Alloc((nuint)sizeof(int));
        *flag = 0;
        return flag;
    }

    internal static void Begin(SpriteBatch batch, SpriteSortMode sortMode, GameNullable<Matrix> transformMatrix)
    {
        if (!transformMatrix.HasValue || batch.IsNull) return;

        Matrix matrix = transformMatrix.Value;
        batch._renderMatrix = matrix;
        if (FromGame(matrix)) return;

        SpriteBatcher batcher = batch._batcher;
        if (batcher.IsNull) return;
        Install();

        lock (pending)
        {
            if (!pending.ContainsKey(batcher.Pointer)) (*Active)++;
            pending[batcher.Pointer] = new Pending { Matrix = matrix, Start = batcher._batchItemCount, Immediate = sortMode == SpriteSortMode.Immediate };
        }
    }

    static bool FromGame(Matrix matrix)
    {
        if (matrix == Matrix.Identity || matrix == Main.Transform || matrix == Main.UIScaleMatrix) return true;

        SpriteViewMatrix view = Main.GameViewMatrix;
        return !view.IsNull && (matrix == view.TransformationMatrix || matrix == view.ZoomMatrix);
    }

    static void Install()
    {
        if (installed) return;
        installed = true;

        ItemHooks.Once("SpriteBatch matrix", () =>
        {
            HookGates.Define<On_SpriteBatch.hook_End>(Gate);
            HookGates.Want<On_SpriteBatch.hook_End>();
            On_SpriteBatch.End += End;

            HookGates.Define<On_SpriteBatcher.hook_DrawImmediateBatch>(Gate);
            HookGates.Want<On_SpriteBatcher.hook_DrawImmediateBatch>();
            On_SpriteBatcher.DrawImmediateBatch += DrawImmediateBatch;
        });
    }

    static GateSpec Gate() => new() { Arg = HookGates.AbsoluteArg, PointerOffset = -1, FieldOffset = 0, FieldSize = 4, Address = (IntPtr)Active };

    static void End(On_SpriteBatch.orig_End orig, SpriteBatch self)
    {
        SpriteBatcher batcher = self._batcher;
        Pending state = default;
        bool found = false;

        if (!batcher.IsNull)
        {
            lock (pending)
            {
                found = pending.Remove(batcher.Pointer, out state);
                if (found) (*Active)--;
            }
        }

        if (found && !state.Immediate)
        {
            int count = batcher._batchItemCount;
            Transform(batcher, state.Start <= count ? state.Start : 0, count, state.Matrix);
        }

        orig(self);
    }

    static void DrawImmediateBatch(On_SpriteBatcher.orig_DrawImmediateBatch orig, SpriteBatcher self, Effect effect)
    {
        Pending state;
        bool found;
        lock (pending) found = pending.TryGetValue(self.Pointer, out state);

        if (found && state.Immediate) Transform(self, 0, 1, state.Matrix);
        orig(self, effect);
    }

    static void Transform(SpriteBatcher batcher, int first, int end, Matrix m)
    {
        GameArray<SpriteBatchItem> items = batcher._batchItemList;
        if (items.IsNull) return;
        end = Math.Min(end, items.Length);

        vertices ??= new[]
        {
            ItemHooks.FieldOffset(SpriteBatchItem.Class, "vertexTL"),
            ItemHooks.FieldOffset(SpriteBatchItem.Class, "vertexTR"),
            ItemHooks.FieldOffset(SpriteBatchItem.Class, "vertexBL"),
            ItemHooks.FieldOffset(SpriteBatchItem.Class, "vertexBR"),
        };

        for (int i = first; i < end; i++)
        {
            SpriteBatchItem item = items[i];
            if (item.IsNull) continue;

            foreach (int offset in vertices)
            {
                Vector2* position = (Vector2*)(item.Pointer + offset);
                float x = position->X, y = position->Y;
                position->X = x * m.M11 + y * m.M21 + m.M41;
                position->Y = x * m.M12 + y * m.M22 + m.M42;
            }
        }
    }
}
