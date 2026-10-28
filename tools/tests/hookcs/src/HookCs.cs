using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

public sealed class HookCs : Mod
{
    static ILog log = null!;

    static int pass, fail;
    static int frames, setDefaultsCalls;
    static uint firstUpdateCount;
    static bool worldDone;

    public override void Load()
    {
        log = Logger;
        Calls();
        Hooks();
        On_Item.SetDefaults += CountSetDefaults;
        On_Player.Update += WorldPhase;
        log.Info($"hookcs load phase: pass={pass} fail={fail}");
    }

    static void Check(string name, Func<string?> body)
    {
        try
        {
            string? error = body();
            if (error == null)
            {
                pass++;
                log.Info($"hookcs PASS {name}");
            }
            else
            {
                fail++;
                log.Warn($"hookcs FAIL {name}: {error}");
            }
        }
        catch (Exception e)
        {
            fail++;
            log.Warn($"hookcs FAIL {name}: {e.GetType().Name}: {e.Message}");
        }
    }

    static bool Near(float a, float b, float eps = 1e-3f) => MathF.Abs(a - b) <= eps;

    static string V(Vector2 v) => $"({v.X}, {v.Y})";

    // ---- chamadas ao jogo (sem hook) ----

    static void Calls()
    {
        Check("struct layouts match the game", () =>
        {
            List<string> bad = Layouts.Verify();
            return bad.Count == 0 ? null : string.Join("; ", bad);
        });
        Check("call HFA args, F32 return: Vector2.Distance", () =>
        {
            float d = Vector2.Distance(new Vector2(0, 0), new Vector2(3, 4));
            return Near(d, 5) ? null : $"got {d}";
        });
        Check("call 3 floats: MathHelper.Clamp", () =>
        {
            float c = MathHelper.Clamp(5f, 0f, 2f);
            return Near(c, 2) ? null : $"got {c}";
        });
        Check("call x8 return (64 bytes): Matrix.CreateScale(float)", () =>
        {
            Matrix m = Matrix.CreateScale(2f);
            return Near(m.M11, 2) && Near(m.M22, 2) && Near(m.M33, 2) && Near(m.M44, 1) && Near(m.M12, 0) ? null : $"M11={m.M11} M22={m.M22} M44={m.M44}";
        });
        Check("call ref+ref+out big struct: Matrix.Add(ref, ref, out)", () =>
        {
            Matrix a = Matrix.CreateScale(2f), b = Matrix.CreateScale(3f);
            Matrix.Add(ref a, ref b, out Matrix r);
            return Near(r.M11, 5) && Near(r.M44, 2) ? null : $"M11={r.M11} M44={r.M44}";
        });
        Check("call big struct by value + x8: Matrix.Add(Matrix, Matrix)", () =>
        {
            Matrix r = Matrix.Add(Matrix.CreateScale(2f), Matrix.CreateScale(3f));
            return Near(r.M11, 5) && Near(r.M44, 2) ? null : $"M11={r.M11} M44={r.M44}";
        });
        Check("call ref+ref+out float: Quaternion.Dot", () =>
        {
            var q1 = new Quaternion { X = 1, Y = 2, Z = 3, W = 4 };
            var q2 = new Quaternion { X = 5, Y = 6, Z = 7, W = 8 };
            Quaternion.Dot(ref q1, ref q2, out float d);
            return Near(d, 70) ? null : $"got {d}";
        });
        Check("call HFA2 return: Vector2.Normalize(Vector2)", () =>
        {
            Vector2 n = Vector2.Normalize(new Vector2(3, 4));
            return Near(n.X, 0.6f) && Near(n.Y, 0.8f) ? null : $"got {V(n)}";
        });
        Check("call HFA3 args and return: Vector3.Cross", () =>
        {
            Vector3 c = Vector3.Cross(new Vector3(1, 0, 0), new Vector3(0, 1, 0));
            return Near(c.X, 0) && Near(c.Y, 0) && Near(c.Z, 1) ? null : $"got {c}";
        });
        Check("call struct this + 16-byte arg + bool: Rectangle.Intersects", () =>
        {
            var r = new Rectangle(0, 0, 10, 10);
            bool yes = r.Intersects(new Rectangle(5, 5, 10, 10)), no = r.Intersects(new Rectangle(20, 20, 5, 5));
            return yes && !no ? null : $"yes={yes} no={no}";
        });
        Check("call 16-byte return (x0/x1): Rectangle.Union", () =>
        {
            Rectangle u = Rectangle.Union(new Rectangle(0, 0, 10, 10), new Rectangle(5, 5, 10, 10));
            return u.X == 0 && u.Y == 0 && u.Width == 15 && u.Height == 15 ? null : $"got {u}";
        });
        Check("call 4-byte struct args and return: Color.Lerp", () =>
        {
            Color c = Color.Lerp(new Color(0, 0, 0, 255), new Color(200, 100, 50, 255), 0.5f);
            return Math.Abs(c.R - 100) <= 1 && Math.Abs(c.G - 50) <= 1 && Math.Abs(c.B - 25) <= 1 && c.A == 255 ? null : $"got {c}";
        });
        Check("call Nullable<Vector3> arg (null and value): Matrix.CreateBillboard", () =>
        {
            Vector3 o = new(0, 0, 0), cam = new(0, 0, 10), up = new(0, 1, 0);
            Matrix.CreateBillboard(ref o, ref cam, ref up, null, out Matrix a);
            Matrix.CreateBillboard(ref o, ref cam, ref up, new Vector3(0, 0, -1), out Matrix b);
            return Near(a.M44, 1) && Near(b.M44, 1) && !float.IsNaN(a.M11) && !float.IsNaN(b.M11) ? null : $"a.M44={a.M44} b.M44={b.M44}";
        });
        Check("call string return: Lang.GetItemNameValue(98)", () =>
        {
            string? s = Lang.GetItemNameValue(98);
            log.Info($"hookcs   item 98 is '{s}'");
            return string.IsNullOrEmpty(s) ? "empty" : null;
        });
        Check("call default args + bool: WorldGen.InWorld", () =>
        {
            bool inside = WorldGen.InWorld(100, 100), outside = WorldGen.InWorld(-5, -5);
            return !outside ? null : $"inside={inside} outside={outside}";
        });
        Check("static string field: Main.versionNumber", () =>
        {
            string? v = Main.versionNumber;
            log.Info($"hookcs   version='{v}'");
            return string.IsNullOrEmpty(v) ? "empty version" : null;
        });
        Check("constructor + instance method + fields: Item.New().SetDefaults(98)", () =>
        {
            Item item = Item.New();
            item.SetDefaults(98, default);
            return item.type == 98 && item.useTime > 0 && item.damage > 0 ? null : $"type={item.type} useTime={item.useTime} damage={item.damage}";
        });
        // No menu o getter do Main.myPlayer do celular lanca NullReferenceException
        // (o jogador local ainda nao existe): uma excecao gerida do jogo.
        Check("game exception becomes GameException: Main.myPlayer in the menu", () =>
        {
            try
            {
                int me = Main.myPlayer;
                return $"no exception (returned {me})";
            }
            catch (GameException e)
            {
                log.Info($"hookcs   game said: {e.Message}");
                return null;
            }
        });
        // Caminho rapido (sem a transicao de GC): a matematica do XNA. O
        // Matrix.Lerp(Matrix, Matrix, float) do jogo lanca NotImplementedException.
        Check("fast path: game exception still becomes GameException: Matrix.Lerp", () =>
        {
            try
            {
                Matrix r = Matrix.Lerp(Matrix.CreateScale(2f), Matrix.CreateScale(4f), 0.5f);
                return $"no exception (M11={r.M11})";
            }
            catch (GameException e)
            {
                log.Info($"hookcs   game said: {e.Message}");
                return null;
            }
        });
        Check("null proxy throws NullReferenceException", () =>
        {
            try
            {
                default(Player).CountBuffs();
                return "no exception";
            }
            catch (NullReferenceException)
            {
                return null;
            }
        });
        Check("GameNullable conversions", () =>
        {
            GameNullable<float> a = 1.5f, b = null;
            float? back = a;
            return a.HasValue && !b.HasValue && back == 1.5f ? null : "conversion";
        });
    }

    // ---- hooks ----

    static void Hooks()
    {
        Check("hook float args + F32 return, add and remove: On_MathHelper.Hermite", () =>
        {
            float baseV = MathHelper.Hermite(1, 2, 3, 4, 0.5f);
            On_MathHelper.hook_Hermite h = (orig, a, b, c, d, t) => orig(a, b, c, d, t) + 1000f;
            On_MathHelper.Hermite += h;
            float hooked = MathHelper.Hermite(1, 2, 3, 4, 0.5f);
            On_MathHelper.Hermite -= h;
            float after = MathHelper.Hermite(1, 2, 3, 4, 0.5f);
            return Near(hooked, baseV + 1000) && Near(after, baseV) ? null : $"base={baseV} hooked={hooked} after={after}";
        });
        Check("hook chain order: last added runs first", () =>
        {
            var order = new List<string>();
            On_MathHelper.hook_Hermite a = (orig, v1, t1, v2, t2, x) => { order.Add("A"); return orig(v1, t1, v2, t2, x); };
            On_MathHelper.hook_Hermite b = (orig, v1, t1, v2, t2, x) => { order.Add("B"); return orig(v1, t1, v2, t2, x); };
            On_MathHelper.Hermite += a;
            On_MathHelper.Hermite += b;
            MathHelper.Hermite(1, 2, 3, 4, 0.5f);
            On_MathHelper.Hermite -= a;
            On_MathHelper.Hermite -= b;
            string got = string.Join(",", order);
            return got == "B,A" ? null : $"order {got}";
        });
        Check("hook ref + out: On_Vector2.Dot(ref, ref, out float)", () =>
        {
            On_Vector2.hook_Dot_refVector2_refVector2_outfloat h = (orig, ref v1, ref v2, out r) =>
            {
                v1.X = 10;
                orig(ref v1, ref v2, out r);
                r *= 2;
            };
            On_Vector2.Dot_refVector2_refVector2_outfloat += h;
            var a = new Vector2(1, 2);
            var b = new Vector2(3, 4);
            Vector2.Dot(ref a, ref b, out float d);
            On_Vector2.Dot_refVector2_refVector2_outfloat -= h;
            // (10*3 + 2*4) * 2 = 76
            return Near(a.X, 10) && Near(d, 76) ? null : $"a.X={a.X} d={d}";
        });
        Check("hook Nullable<Vector3> arg + out Matrix: On_Matrix.CreateBillboard", () =>
        {
            GameNullable<Vector3> seen = default;
            On_Matrix.hook_CreateBillboard h = (orig, ref o, ref c, ref up, fwd, out m) =>
            {
                seen = fwd;
                orig(ref o, ref c, ref up, fwd, out m);
                m.M44 = 7;
            };
            On_Matrix.CreateBillboard += h;
            Vector3 op = new(0, 0, 0), cam = new(0, 0, 10), upv = new(0, 1, 0);
            Matrix.CreateBillboard(ref op, ref cam, ref upv, new Vector3(1, 2, 3), out Matrix m1);
            GameNullable<Vector3> withValue = seen;
            Matrix.CreateBillboard(ref op, ref cam, ref upv, null, out Matrix m2);
            GameNullable<Vector3> withNull = seen;
            On_Matrix.CreateBillboard -= h;
            bool okValue = withValue.HasValue && Near(withValue.Value.X, 1) && Near(withValue.Value.Z, 3);
            return okValue && !withNull.HasValue && Near(m1.M44, 7) && Near(m2.M44, 7) ? null
                : $"value={withValue} null={withNull} m1.M44={m1.M44}";
        });
        Check("hook Nullable<int> + Nullable<float> args: On_NPC.ScaleStats", () =>
        {
            GameNullable<int> seenA = default;
            GameNullable<float> seenB = default;
            int calls = 0;
            On_NPC.hook_ScaleStats h = (orig, self, a, b) =>
            {
                seenA = a;
                seenB = b;
                calls++;
            };
            On_NPC.ScaleStats += h;
            NPC npc = NPC.New();
            npc.ScaleStats(5, 1.5f);
            GameNullable<int> a1 = seenA;
            GameNullable<float> b1 = seenB;
            npc.ScaleStats(null, null);
            On_NPC.ScaleStats -= h;
            return calls == 2 && a1.HasValue && a1.Value == 5 && b1.HasValue && Near(b1.Value, 1.5f) && !seenA.HasValue && !seenB.HasValue
                ? null : $"calls={calls} a1={a1} b1={b1} a2={seenA} b2={seenB}";
        });
        Check("hook HFA2 arg and return: On_Vector2.Normalize_Vector2", () =>
        {
            On_Vector2.hook_Normalize_Vector2 h = (orig, v) => v.X == 77 ? new Vector2(1, 2) : orig(v);
            On_Vector2.Normalize_Vector2 += h;
            Vector2 special = Vector2.Normalize(new Vector2(77, 0));
            Vector2 normal = Vector2.Normalize(new Vector2(3, 4));
            On_Vector2.Normalize_Vector2 -= h;
            return Near(special.X, 1) && Near(special.Y, 2) && Near(normal.X, 0.6f) ? null : $"special={V(special)} normal={V(normal)}";
        });
        Check("hook struct `ref self` + 16-byte arg: On_Rectangle.Intersects_Rectangle", () =>
        {
            On_Rectangle.hook_Intersects_Rectangle h = (orig, ref self, other) => self.X == 1234 || orig(ref self, other);
            On_Rectangle.Intersects_Rectangle += h;
            bool forced = new Rectangle(1234, 0, 1, 1).Intersects(new Rectangle(0, 0, 1, 1));
            bool normal = new Rectangle(0, 0, 1, 1).Intersects(new Rectangle(50, 50, 1, 1));
            On_Rectangle.Intersects_Rectangle -= h;
            return forced && !normal ? null : $"forced={forced} normal={normal}";
        });
        // O Matrix.Lerp(Matrix, Matrix, float) do jogo lanca NotImplementedException.
        Check("hook big by-value arg + float + x8 return: On_Matrix.Multiply_Matrix_float", () =>
        {
            On_Matrix.hook_Multiply_Matrix_float h = (orig, m, factor) =>
            {
                Matrix r = orig(m, factor);
                r.M11 += 100;
                return r;
            };
            On_Matrix.Multiply_Matrix_float += h;
            Matrix scaled = Matrix.Multiply(Matrix.CreateScale(2f), 1.5f);
            On_Matrix.Multiply_Matrix_float -= h;
            return Near(scaled.M11, 103) && Near(scaled.M22, 3) ? null : $"M11={scaled.M11} M22={scaled.M22}";
        });
        Check("hook string arg and return: On_Lang.GetNPCNameValue", () =>
        {
            On_Lang.hook_GetNPCNameValue h = (orig, id) => id == 1 ? "HOOKED" : orig(id);
            On_Lang.GetNPCNameValue += h;
            string? a = Lang.GetNPCNameValue(1), b = Lang.GetNPCNameValue(2);
            On_Lang.GetNPCNameValue -= h;
            return a == "HOOKED" && b != "HOOKED" && !string.IsNullOrEmpty(b) ? null : $"a='{a}' b='{b}'";
        });
        Check("hook bool return + 8-byte struct arg: On_WorldGen.InWorld_Point_int", () =>
        {
            On_WorldGen.hook_InWorld_Point_int h = (orig, pt, f) => pt.X == -77 || orig(pt, f);
            On_WorldGen.InWorld_Point_int += h;
            bool forced = WorldGen.InWorld(new Point(-77, 0)), normal = WorldGen.InWorld(new Point(-78, 0));
            On_WorldGen.InWorld_Point_int -= h;
            return forced && !normal ? null : $"forced={forced} normal={normal}";
        });
        Check("hook that throws before orig: the game method still runs", () =>
        {
            float baseV = MathHelper.Hermite(1, 2, 3, 4, 0.25f);
            On_MathHelper.hook_Hermite h = (orig, a, b, c, d, t) => throw new InvalidOperationException("hookcs test error (expected)");
            On_MathHelper.Hermite += h;
            float got = MathHelper.Hermite(1, 2, 3, 4, 0.25f);
            On_MathHelper.Hermite -= h;
            return Near(got, baseV) ? null : $"base={baseV} got={got}";
        });
        Check("hook that throws after orig: the original result is kept", () =>
        {
            float baseV = MathHelper.Hermite(1, 2, 3, 4, 0.75f);
            On_MathHelper.hook_Hermite h = (orig, a, b, c, d, t) =>
            {
                orig(a, b, c, d, t);
                throw new InvalidOperationException("hookcs test error (expected)");
            };
            On_MathHelper.Hermite += h;
            float got = MathHelper.Hermite(1, 2, 3, 4, 0.75f);
            On_MathHelper.Hermite -= h;
            return Near(got, baseV) ? null : $"base={baseV} got={got}";
        });
        // Collision.CanHit(null, null): o jogo le source.position e lanca
        // NullReferenceException (gerida, pegavel).
        Check("game exception inside orig reaches the hook: On_Collision.CanHit_Entity_Entity", () =>
        {
            bool caught = false;
            On_Collision.hook_CanHit_Entity_Entity h = (orig, source, target) =>
            {
                try { return orig(source, target); }
                catch (GameException) { caught = true; return true; }
            };
            On_Collision.CanHit_Entity_Entity += h;
            bool r = Collision.CanHit(default(Entity), default(Entity));
            On_Collision.CanHit_Entity_Entity -= h;
            return caught && r ? null : $"caught={caught} r={r}";
        });
    }

    // ---- chamadas do proprio jogo ----

    static void CountSetDefaults(On_Item.orig_SetDefaults orig, Item self, int type, Terraria.GameContent.Items.ItemVariant variant)
    {
        orig(self, type, variant);
        setDefaultsCalls++;
    }

    static void WorldPhase(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (worldDone || i != Main.myPlayer || Main.gameMenu) return;
        frames++;
        if (frames == 1) firstUpdateCount = Main.GameUpdateCount;
        if (frames != 30) return;
        worldDone = true;
        Check("world: game-driven hooks on the main thread (Player.Update, Item.SetDefaults)", () =>
        {
            log.Info($"hookcs   Player.Update frames={frames} Item.SetDefaults calls={setDefaultsCalls}");
            return frames == 30 ? null : "frames";
        });
        Check("world: Main.player[myPlayer] and ref field Entity.position", () =>
        {
            Player p = Main.player[Main.myPlayer];
            Vector2 pos = p.position;
            return p == self && !p.IsNull && pos.X > 0 && pos.Y > 0 ? null : $"same={p == self} pos={V(pos)}";
        });
        Check("world: property with HFA return: Entity.Center", () =>
        {
            Vector2 c = self.Center, pos = self.position;
            float ex = pos.X + self.width / 2f, ey = pos.Y + self.height / 2f;
            return Near(c.X, ex) && Near(c.Y, ey) ? null : $"center={V(c)} expected=({ex}, {ey})";
        });
        Check("world: out Nullable<int> + proxy return: Player.GetEffectiveArmor", () =>
        {
            Item armor = self.GetEffectiveArmor(0, out GameNullable<int> shared);
            log.Info($"hookcs   armor[0] type={(armor.IsNull ? -1 : armor.type)} shared={shared}");
            return armor.IsNull ? "null item" : null;
        });
        Check("world: Nullable<Vector2> return (12 bytes): Player.HandPosition", () =>
        {
            GameNullable<Vector2> hand = self.HandPosition;
            log.Info($"hookcs   HandPosition={hand}");
            return null;
        });
        Check("world: array of proxies + property: inventory[selectedItem].type", () =>
        {
            Item held = self.inventory[self.selectedItem];
            log.Info($"hookcs   holding type {held.type} ('{Lang.GetItemNameValue(held.type)}')");
            return held.IsNull ? "null" : null;
        });
        Check("world: fast-path getter runs its hook, and a Mono GC inside it: On_Player.get_HandPosition", () =>
        {
            int calls = 0;
            On_Player.hook_get_HandPosition h = (orig, p) =>
            {
                calls++;
                var junk = new List<byte[]>();
                for (int k = 0; k < 200; k++) junk.Add(new byte[1024]);
                GC.Collect();
                orig(p);
                return new Vector2(1, 2);
            };
            On_Player.get_HandPosition += h;
            GameNullable<Vector2> hand = self.HandPosition;
            On_Player.get_HandPosition -= h;
            return calls == 1 && hand.HasValue && Near(hand.Value.X, 1) && Near(hand.Value.Y, 2) ? null : $"calls={calls} hand={hand}";
        });
        Check("world: ref field write keeps the value: statLife", () =>
        {
            int life = self.statLife;
            self.statLife = life;
            return self.statLife == life ? null : "changed";
        });
        Check("world: static property advances: Main.GameUpdateCount", () =>
        {
            uint now = Main.GameUpdateCount;
            return now > firstUpdateCount ? null : $"first={firstUpdateCount} now={now}";
        });
        On_Item.SetDefaults -= CountSetDefaults;
        log.Info($"hookcs FIM pass={pass} fail={fail}");
        Main.NewText($"Teste dos hooks C#: {pass} ok, {fail} falha(s)", 255, 240, 120);
    }
}
