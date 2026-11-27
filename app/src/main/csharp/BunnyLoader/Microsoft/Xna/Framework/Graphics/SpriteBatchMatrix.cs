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

    static int vertexTL = -1, vertexTR, vertexBL, vertexBR;

    static bool installed;

    static uint cachedUpdate;
    static bool cached;
    static Matrix gameTransform, uiScale, viewTransform, viewZoom;

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
        if (FromGame(in matrix)) return;

        SpriteBatcher batcher = batch._batcher;
        if (batcher.IsNull) return;
        Install();

        lock (pending)
        {
            if (!pending.ContainsKey(batcher.Pointer)) (*Active)++;
            pending[batcher.Pointer] = new Pending { Matrix = matrix, Start = batcher._batchItemCount, Immediate = sortMode == SpriteSortMode.Immediate };
        }
    }

    internal static void End(SpriteBatch batch)
    {
        if (*Active == 0 || batch.IsNull) return;
        Finish(batch._batcher);
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

    static void Install()
    {
        if (installed) return;
        installed = true;

        ItemHooks.Once("SpriteBatch matrix", () =>
        {
            HookGates.Define<On_SpriteBatch.hook_End>(Gate);
            HookGates.Want<On_SpriteBatch.hook_End>();
            On_SpriteBatch.End += EndHook;

            HookGates.Define<On_SpriteBatcher.hook_DrawImmediateBatch>(Gate);
            HookGates.Want<On_SpriteBatcher.hook_DrawImmediateBatch>();
            On_SpriteBatcher.DrawImmediateBatch += DrawImmediateBatch;
        });
    }

    static GateSpec Gate() => new() { Arg = HookGates.AbsoluteArg, PointerOffset = -1, FieldOffset = 0, FieldSize = 4, Address = (IntPtr)Active };

    static void EndHook(On_SpriteBatch.orig_End orig, SpriteBatch self)
    {
        if (!self.IsNull) Finish(self._batcher);
        orig(self);
    }

    static void Finish(SpriteBatcher batcher)
    {
        if (batcher.IsNull) return;

        Pending state;
        lock (pending)
        {
            if (!pending.Remove(batcher.Pointer, out state)) return;
            (*Active)--;
        }

        if (state.Immediate) return;
        int count = batcher._batchItemCount;
        Transform(batcher, state.Start <= count ? state.Start : 0, count, in state.Matrix);
    }

    static void DrawImmediateBatch(On_SpriteBatcher.orig_DrawImmediateBatch orig, SpriteBatcher self, Effect effect)
    {
        Pending state;
        bool found;
        lock (pending) found = pending.TryGetValue(self.Pointer, out state);

        if (found && state.Immediate) Transform(self, 0, 1, in state.Matrix);
        orig(self, effect);
    }

    static void Transform(SpriteBatcher batcher, int first, int end, in Matrix m)
    {
        GameArray<SpriteBatchItem> items = batcher._batchItemList;
        if (items.IsNull) return;
        Span<SpriteBatchItem> span = items.AsSpan();
        end = Math.Min(end, span.Length);

        if (vertexTL < 0)
        {
            vertexTR = ItemHooks.FieldOffset(SpriteBatchItem.Class, "vertexTR");
            vertexBL = ItemHooks.FieldOffset(SpriteBatchItem.Class, "vertexBL");
            vertexBR = ItemHooks.FieldOffset(SpriteBatchItem.Class, "vertexBR");
            vertexTL = ItemHooks.FieldOffset(SpriteBatchItem.Class, "vertexTL");
        }

        for (int i = first; i < end; i++)
        {
            nint item = span[i].Pointer;
            if (item == 0) continue;

            Apply((Vector2*)(item + vertexTL), in m);
            Apply((Vector2*)(item + vertexTR), in m);
            Apply((Vector2*)(item + vertexBL), in m);
            Apply((Vector2*)(item + vertexBR), in m);
        }
    }

    static void Apply(Vector2* position, in Matrix m)
    {
        float x = position->X, y = position->Y;
        position->X = x * m.M11 + y * m.M21 + m.M41;
        position->Y = x * m.M12 + y * m.M22 + m.M42;
    }
}
