using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using BunnyLoader;
using BunnyLoader.Interop;
using Slider = GUIVirtualInputControllerEdit_ControlPanel.ControlPanelSizeSlider;
using SlotType = VirtualControlsSlotTypeMapping_Layout.SlotType;

namespace Terraria.ModLoader.Core;

internal static unsafe class TouchControlSizes
{
    internal const string Suffix = "_BL";
    internal const int MinPercent = 50;
    internal const int MaxPercent = 250;
    internal const int StepPercent = 10;
    internal const int MarkPercent = 50;

    internal enum Table { Axis, Button, Group }

    internal sealed class Family
    {
        internal readonly string Prefix;
        internal readonly Table Table;
        internal int[] Vanilla = Array.Empty<int>();
        internal int[] Steps = Array.Empty<int>();
        internal int[] Percents = Array.Empty<int>();
        internal int[] Marks = Array.Empty<int>();
        internal int Reference;

        internal Family(string prefix, Table table)
        {
            Prefix = prefix;
            Table = table;
        }

        internal string IdOf(int size) => Array.IndexOf(Vanilla, size) >= 0 ? $"{Prefix}_{size}" : $"{Prefix}_{size}{Suffix}";

        internal int Nearest(int size)
        {
            int best = Vanilla[0];
            foreach (int v in Vanilla)
                if (MathF.Abs(MathF.Log(size / (float)v)) < MathF.Abs(MathF.Log(size / (float)best))) best = v;
            return best;
        }

        internal int StepIndex(int size)
        {
            int best = 0;
            for (int i = 1; i < Steps.Length; i++)
                if (Math.Abs(Steps[i] - size) < Math.Abs(Steps[best] - size)) best = i;
            return best;
        }
    }

    sealed class Row
    {
        public string Hardware = "";
        public string Slot = "";
        public string Style = "";
        public int Size;
    }

    internal static readonly Family Axis = new("Axis", Table.Axis);
    internal static readonly Family Action = new("Action", Table.Button);
    internal static readonly Family Meta = new("Meta", Table.Button);
    internal static readonly Family GroupButton = new("Group", Table.Button);
    internal static readonly Family Group = new("Group", Table.Group);
    static readonly Family[] Families = { Axis, Action, Meta, GroupButton, Group };

    static readonly Dictionary<string, VirtualControlsAxisConfiguration_Layout> axisClones = new();
    static readonly Dictionary<string, VirtualControlsButtonConfiguration_Layout> buttonClones = new();
    static readonly Dictionary<string, VirtualControlsGroupConfiguration_Layout> groupClones = new();
    static readonly List<GameHandle> kept = new();
    static readonly GameClass ObjectClass = new("System", "Object");
    static MethodRef cloneMethod;
    static readonly int[] cloneLayout = { -1 };
    static IntPtr familiesFrom;
    static List<Row>? saved;
    static bool undoPending;

    static string FilePath => Path.Combine(Main.SavePath ?? "", "BunnyLoader.touchsizes.json");

    internal static void Install() => ItemHooks.Once("TouchControlSizes", () =>
    {
        On_VirtualControlsStyles_Layout.GetAxisConfiguration += (orig, self, id) =>
        {
            VirtualControlsAxisConfiguration_Layout found = orig(self, id);
            if (found.IsNull && IsOurs(id)) Guard("axis style", () => found = AxisConfig(self, id!));
            return found;
        };
        On_VirtualControlsStyles_Layout.GetButtonConfiguration += (orig, self, id) =>
        {
            VirtualControlsButtonConfiguration_Layout found = orig(self, id);
            if (found.IsNull && IsOurs(id)) Guard("button style", () => found = ButtonConfig(self, id!));
            return found;
        };
        On_VirtualControlsStyles_Layout.GetGroupConfiguration += (orig, self, id) =>
        {
            VirtualControlsGroupConfiguration_Layout found = orig(self, id);
            if (found.IsNull && IsOurs(id)) Guard("group style", () => found = GroupConfig(self, id!));
            return found;
        };
        On_VirtualControlsStyles_Layout.SaveConfiguration += (orig, self, preferences) =>
        {
            Guard("save", () => Capture(self));
            WithVanillaSizes(Hardware(self), () => orig(self, preferences));
        };
        On_VirtualControlsStyles_Layout.LoadConfiguration += (orig, self, preferences) =>
        {
            orig(self, preferences);
            Guard("load", () => Apply(self));
        };
        On_VirtualControlsStyles_Layout.GetShareCode += (orig, profile) =>
        {
            string? code = null;
            VirtualControlsHardwareConfiguration_Layout hardware = profile.IsNull ? default : profile.HardwareConfig;
            WithVanillaSizes(hardware.IsNull ? new List<VirtualControlsHardwareConfiguration_Layout>() : new List<VirtualControlsHardwareConfiguration_Layout> { hardware },
                () => code = orig(profile));
            return code;
        };
        On_GUIVirtualInputControllerEdit_ControlPanel_ControlPanelSizeSlider.GetMaxValue += (orig, self) =>
            Editing(out _, out Family? family) ? family!.Steps.Length - 1 : orig(self);
        On_GUIVirtualInputControllerEdit_ControlPanel_ControlPanelSizeSlider.GetValue += (orig, self) =>
        {
            if (!Editing(out VirtualControlsSlotTypeMapping_Layout style, out Family? family)) return orig(self);
            return family!.StepIndex(SizeOf(style.ControlTypeId)) / (float)(family.Steps.Length - 1);
        };
        On_GUIVirtualInputControllerEdit_ControlPanel_ControlPanelSizeSlider.SetValue += (orig, self, value) =>
        {
            if (!Editing(out VirtualControlsSlotTypeMapping_Layout style, out Family? family))
            {
                orig(self, value);
                return;
            }
            Guard("size slider", () => SetSize(style, family!, value));
        };
        On_GUIVirtualInputControllerEdit_ControlPanel_ControlPanelSizeSlider.Draw += (orig, self) =>
        {
            orig(self);
            if (undoPending) Guard("undo", () => AddUndo(self));
        };
        On_GUIVirtualInputControllerEdit_ControlPanel_ControlPanelSizeSlider.GetStringValue += (orig, self) =>
            Editing(out VirtualControlsSlotTypeMapping_Layout style, out Family? family)
                ? family!.Percents[family.StepIndex(SizeOf(style.ControlTypeId))].ToString(CultureInfo.InvariantCulture) + "%"
                : orig(self);
        On_GUIVirtualInputControllerEdit_ControlPanel_ControlPanelSizeSlider.SizeBackingHandler += (orig, self, position, size, layout, controllerOver) =>
        {
            if (!Editing(out _, out Family? family))
            {
                orig(self, position, size, layout, controllerOver);
                return;
            }
            Guard("size notches", () => DrawNotches(family!, size, layout));
        };
        Guard("saved sizes", () => Apply(VirtualControlsStyles_Layout.Instance));
    });

    static void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception e) { ItemHooks.Report($"TouchControlSizes: {what}", e); }
    }

    internal static bool IsOurs(string? id) => id != null && id.EndsWith(Suffix, StringComparison.Ordinal);

    internal static int SizeOf(string? id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        string core = IsOurs(id) ? id[..^Suffix.Length] : id;
        int bar = core.LastIndexOf('_');
        return bar >= 0 && int.TryParse(core.AsSpan(bar + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int size) ? size : 0;
    }

    static string PrefixOf(string id)
    {
        int bar = id.IndexOf('_');
        return bar > 0 ? id[..bar] : id;
    }

    internal static Family? FamilyOf(Table table, string prefix)
    {
        foreach (Family family in Families)
            if (family.Table == table && family.Prefix == prefix) return family.Vanilla.Length > 0 ? family : null;
        return null;
    }

    internal static void Refresh(VirtualControlsStyles_Layout styles)
    {
        if (styles.IsNull || styles.Pointer == familiesFrom) return;
        familiesFrom = styles.Pointer;
        Collect(Axis, styles.AxisTypes, c => c.Id);
        Collect(Group, styles.GroupTypes, c => c.Id);
        GameArray<VirtualControlsButtonConfiguration_Layout> buttons = styles.ButtonTypes;
        Collect(Action, buttons, c => c.Id);
        Collect(Meta, buttons, c => c.Id);
        Collect(GroupButton, buttons, c => c.Id);
    }

    static void Collect<T>(Family family, GameArray<T> configs, Func<T, string?> id) where T : unmanaged, IGameObject
    {
        var sizes = new List<int>();
        for (int i = 0; !configs.IsNull && i < configs.Length; i++)
        {
            T config = configs[i];
            if (config.Pointer == IntPtr.Zero || id(config) is not string name || IsOurs(name)) continue;
            if (PrefixOf(name) != family.Prefix || name.Length <= family.Prefix.Length + 1) continue;
            if (!int.TryParse(name.AsSpan(family.Prefix.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int size) || size <= 0) continue;
            if (!sizes.Contains(size)) sizes.Add(size);
        }
        sizes.Sort();
        family.Vanilla = sizes.ToArray();
        family.Reference = sizes.Count == 0 ? 0 : sizes[(sizes.Count - 1) / 2];
        (int Size, int Percent)[] steps = StepsOf(family.Vanilla, family.Reference);
        family.Steps = Array.ConvertAll(steps, s => s.Size);
        family.Percents = Array.ConvertAll(steps, s => s.Percent);
        var marks = new List<int>();
        for (int i = 0; i < steps.Length; i++)
            if (steps[i].Percent % MarkPercent == 0) marks.Add(i);
        family.Marks = marks.ToArray();
    }

    internal static (int Size, int Percent)[] StepsOf(int[] vanilla, int reference)
    {
        if (vanilla.Length == 0 || reference <= 0) return Array.Empty<(int, int)>();
        var steps = new List<(int Size, int Percent)>();
        foreach (int v in vanilla) steps.Add((v, (int)MathF.Round(v * 100f / reference)));
        for (int percent = MinPercent; percent <= MaxPercent; percent += StepPercent)
        {
            bool near = false;
            foreach (int v in vanilla)
                if (MathF.Abs(percent - v * 100f / reference) < StepPercent * 0.75f) near = true;
            int size = (int)MathF.Round(reference * percent / 100f);
            if (!near && !steps.Exists(s => s.Size == size)) steps.Add((size, percent));
        }
        steps.Sort((a, b) => a.Size.CompareTo(b.Size));
        return steps.ToArray();
    }

    static void DrawNotches(Family family, Microsoft.Xna.Framework.Vector2 size, Slider_Layout layout)
    {
        VirtualInputControllerControlConfiguration_Layout config = VirtualInputControllerControlConfiguration_Layout.Instance;
        TransactionButton_Layout notch = config.IsNull ? default : config.SliderNotch;
        if (notch.IsNull || layout.IsNull) return;
        float start = notch.Location.X, spacing = (size.X - 2 * layout.SliderEndCap) / (family.Steps.Length - 1), scale = 0;
        try
        {
            foreach (int index in family.Marks)
            {
                notch.Location.X = start + index * spacing;
                GUITransactionButton.Draw(notch, default(Item), "", true, ref scale, false, false, false, true);
            }
        }
        finally { notch.Location.X = start; }
    }

    static bool Editing(out VirtualControlsSlotTypeMapping_Layout style, out Family? family)
    {
        style = default;
        family = null;
        try
        {
            VirtualControlsHardwareConfigurationMapping_Layout slot = GUIVirtualInputControllerEdit_ControlPanel.ActiveSlotConfig;
            if (slot.IsNull || slot.Style.IsNull) return false;
            style = slot.Style;
            Refresh(VirtualControlsStyles_Layout.Instance);
            family = style.ControlType switch
            {
                SlotType.Axis => FamilyOf(Table.Axis, Axis.Prefix),
                SlotType.Group => FamilyOf(Table.Group, Group.Prefix),
                SlotType.Button => ButtonFamily(style, GUIVirtualInputControllerEdit_ControlPanel.ActiveControl),
                _ => null,
            };
            return family != null && family.Steps.Length > 1;
        }
        catch (Exception e)
        {
            ItemHooks.Report("TouchControlSizes: active control", e);
            return false;
        }
    }

    static Family? ButtonFamily(VirtualControlsSlotTypeMapping_Layout style, VirtualControlsBinding_Layout binding)
    {
        string? wanted = null;
        GUIInstance gui = GUIInstance.Active;
        GUIVirtualInputControllerEdit editor = gui.IsNull ? default : gui.GUIVirtualInputControllerEdit;
        if (!editor.IsNull && !binding.IsNull && !TouchButtonLoader.IsModded((int)binding.Action))
        {
            editor.GetDefaultControlSetup(binding.Action, out _, out GameString configuration);
            wanted = configuration;
        }
        if (wanted == null || (!wanted.StartsWith(Action.Prefix, StringComparison.Ordinal) && !wanted.StartsWith(Meta.Prefix, StringComparison.Ordinal)))
            wanted = style.ControlTypeId;
        return FamilyOf(Table.Button, wanted != null && wanted.StartsWith(Meta.Prefix, StringComparison.Ordinal) ? Meta.Prefix : Action.Prefix);
    }

    static void SetSize(VirtualControlsSlotTypeMapping_Layout style, Family family, float value)
    {
        int index = Math.Clamp((int)MathF.Round(value * (family.Steps.Length - 1)), 0, family.Steps.Length - 1);
        string id = Ensure(VirtualControlsStyles_Layout.Instance, family, family.Steps[index]);
        if (style.ControlTypeId == id) return;
        style.ControlTypeId = id;
        undoPending = true;
    }

    static void AddUndo(Slider slider)
    {
        if (!undoPending) return;
        GUISlider.DragState drag = slider.dragState;
        if (!drag.IsNull && drag.wasDragging) return;
        undoPending = false;
        GUIInstance gui = GUIInstance.Active;
        if (!gui.IsNull && !gui.GUIInterfaceEdit.IsNull && gui.GUIInterfaceEdit.Enabled) gui.GUIInterfaceEdit.AddControlUndo();
    }

    internal static string Ensure(VirtualControlsStyles_Layout styles, Family family, int size)
    {
        string id = family.IdOf(size);
        if (!IsOurs(id)) return id;
        switch (family.Table)
        {
            case Table.Axis: AxisConfig(styles, id); break;
            case Table.Button: ButtonConfig(styles, id); break;
            case Table.Group: GroupConfig(styles, id); break;
        }
        return id;
    }

    static nint Clone(nint obj)
    {
        ref readonly var m = ref GameInterop.Method(ref cloneMethod, ObjectClass, "object MemberwiseClone()", cloneLayout);
        CallFrame f;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out f);
        f.X[0] = obj;
        f.X[1] = (long)m.Info;
        f.Ret = RetKind.Int;
        GameInterop.Call(m.Fn, &f);
        nint copy = (nint)(long)f.Out[0];
        if (copy == 0) throw new InvalidOperationException("MemberwiseClone returned null");
        kept.Add(new GameHandle(copy));
        return copy;
    }

    static bool Parse(string id, Table table, out Family family, out int size, out float factor, out string source)
    {
        family = null!;
        factor = 1;
        source = "";
        size = SizeOf(id);
        if (FamilyOf(table, PrefixOf(id)) is not Family found || size <= 0) return false;
        family = found;
        int nearest = found.Nearest(size);
        factor = size / (float)nearest;
        source = $"{found.Prefix}_{nearest}";
        return true;
    }

    static GameArray<T> Appended<T>(GameArray<T> array, T item) where T : unmanaged
    {
        int length = array.IsNull ? 0 : array.Length;
        GameArray<T> grown = array.Resized(length + 1);
        grown[length] = item;
        return grown;
    }

    static VirtualControlsAxisConfiguration_Layout AxisConfig(VirtualControlsStyles_Layout styles, string id)
    {
        if (axisClones.TryGetValue(id, out VirtualControlsAxisConfiguration_Layout known)) return known;
        Refresh(styles);
        if (styles.IsNull || !Parse(id, Table.Axis, out _, out _, out float f, out string source)) return default;
        VirtualControlsAxisConfiguration_Layout from = styles.GetAxisConfiguration(source);
        if (from.IsNull) return default;
        var config = new VirtualControlsAxisConfiguration_Layout(Clone(from.Pointer));
        config.Id = id;
        config.BlueLayout = Scaled(from.BlueLayout, f);
        config.GreenLayout = Scaled(from.GreenLayout, f);
        config.BadLayout = Scaled(from.BadLayout, f);
        config.ControlLayout = Scaled(from.ControlLayout, f);
        config.TopIcon = Scaled(from.TopIcon, f, true);
        styles.AxisTypes = Appended(styles.AxisTypes, config);
        axisClones[id] = config;
        return config;
    }

    static VirtualControlsButtonConfiguration_Layout ButtonConfig(VirtualControlsStyles_Layout styles, string id)
    {
        if (buttonClones.TryGetValue(id, out VirtualControlsButtonConfiguration_Layout known)) return known;
        Refresh(styles);
        if (styles.IsNull || !Parse(id, Table.Button, out _, out _, out float f, out string source)) return default;
        VirtualControlsButtonConfiguration_Layout from = styles.GetButtonConfiguration(source);
        if (from.IsNull) return default;
        var config = new VirtualControlsButtonConfiguration_Layout(Clone(from.Pointer));
        config.Id = id;
        config.BlueLayout = Scaled(from.BlueLayout, f);
        config.GreenLayout = Scaled(from.GreenLayout, f);
        config.TealLayout = Scaled(from.TealLayout, f);
        config.BadPositionLayout = Scaled(from.BadPositionLayout, f);
        config.ControlLayout = Scaled(from.ControlLayout, f);
        styles.ButtonTypes = Appended(styles.ButtonTypes, config);
        buttonClones[id] = config;
        return config;
    }

    static VirtualControlsGroupConfiguration_Layout GroupConfig(VirtualControlsStyles_Layout styles, string id)
    {
        if (groupClones.TryGetValue(id, out VirtualControlsGroupConfiguration_Layout known)) return known;
        Refresh(styles);
        if (styles.IsNull || !Parse(id, Table.Group, out _, out _, out float f, out string source)) return default;
        VirtualControlsGroupConfiguration_Layout from = styles.GetGroupConfiguration(source);
        if (from.IsNull) return default;
        var config = new VirtualControlsGroupConfiguration_Layout(Clone(from.Pointer));
        config.Id = id;
        config.CentreButtonStyle = ScaledButton(styles, from.CentreButtonStyle, f);
        config.OuterButtonStyle = ScaledButton(styles, from.OuterButtonStyle, f);
        config._centreMapping = default;
        config._outerMapping = default;
        config.OuterButtonRadiusMin *= f;
        config.OuterButtonRadiusMax *= f;
        config.IndicatorScale *= f;
        config.Backing = Scaled(from.Backing, f, false);
        styles.GroupTypes = Appended(styles.GroupTypes, config);
        groupClones[id] = config;
        return config;
    }

    static string? ScaledButton(VirtualControlsStyles_Layout styles, string? id, float f)
    {
        if (id == null || FamilyOf(Table.Button, PrefixOf(id)) is not Family family) return id;
        int size = SizeOf(id);
        return size <= 0 ? id : Ensure(styles, family, (int)MathF.Round(size * f));
    }

    static Axis_Layout Scaled(Axis_Layout from, float f)
    {
        if (from.IsNull) return from;
        var layout = new Axis_Layout(Clone(from.Pointer));
        layout.Scale *= f;
        layout.PickSize *= f;
        layout.NubbinPickSize *= f;
        return layout;
    }

    static QuickActionButton_Layout Scaled(QuickActionButton_Layout from, float f)
    {
        if (from.IsNull) return from;
        var layout = new QuickActionButton_Layout(Clone(from.Pointer));
        layout.Scale *= f;
        layout.DisabledScale *= f;
        layout.PressedScale *= f;
        layout.PickBorder *= f;
        layout.PickOffset *= f;
        return layout;
    }

    static Panel_Layout Scaled(Panel_Layout from, float f, bool move)
    {
        if (from.IsNull) return from;
        var panel = new Panel_Layout(Clone(from.Pointer));
        panel.Size *= f;
        if (move) panel.Location *= f;
        return panel;
    }

    static List<VirtualControlsHardwareConfiguration_Layout> Hardware(VirtualControlsStyles_Layout styles)
    {
        var result = new List<VirtualControlsHardwareConfiguration_Layout>();
        if (styles.IsNull) return result;
        GameArray<VirtualControlsHardwareConfiguration_Layout> all = styles.HardwareStyles;
        for (int i = 0; !all.IsNull && i < all.Length; i++)
            if (!all[i].IsNull && !result.Contains(all[i])) result.Add(all[i]);
        GameArray<VirtualControlsProfile_Layout> profiles = styles.Profiles;
        for (int i = 0; !profiles.IsNull && i < profiles.Length; i++)
        {
            VirtualControlsProfile_Layout profile = profiles[i];
            if (profile.IsNull) continue;
            VirtualControlsHardwareConfiguration_Layout hardware = profile.HardwareConfig;
            if (!hardware.IsNull && !result.Contains(hardware)) result.Add(hardware);
        }
        return result;
    }

    static IEnumerable<VirtualControlsSlotTypeMapping_Layout> Styles(VirtualControlsHardwareConfiguration_Layout hardware)
    {
        GameArray<VirtualControlsHardwareConfigurationMapping_Layout> slots = hardware.Slots;
        for (int i = 0; !slots.IsNull && i < slots.Length; i++)
            if (!slots[i].IsNull && !slots[i].Style.IsNull) yield return slots[i].Style;
    }

    internal static string Vanilla(string id, SlotType type)
    {
        Table table = type == SlotType.Axis ? Table.Axis : type == SlotType.Group ? Table.Group : Table.Button;
        if (!IsOurs(id) || FamilyOf(table, PrefixOf(id)) is not Family family) return id;
        return $"{family.Prefix}_{family.Nearest(SizeOf(id))}";
    }

    internal static void WithVanillaSizes(List<VirtualControlsHardwareConfiguration_Layout> hardware, Action run)
    {
        var restore = new List<(VirtualControlsSlotTypeMapping_Layout Style, string Id)>();
        try
        {
            Refresh(VirtualControlsStyles_Layout.Instance);
            foreach (VirtualControlsHardwareConfiguration_Layout config in hardware)
                foreach (VirtualControlsSlotTypeMapping_Layout style in Styles(config))
                {
                    string? id = style.ControlTypeId;
                    if (!IsOurs(id)) continue;
                    restore.Add((style, id!));
                    style.ControlTypeId = Vanilla(id!, style.ControlType);
                }
        }
        catch (Exception e) { ItemHooks.Report("TouchControlSizes: vanilla sizes", e); }
        try { run(); }
        finally
        {
            for (int i = restore.Count - 1; i >= 0; i--)
            {
                try { restore[i].Style.ControlTypeId = restore[i].Id; }
                catch (Exception e) { ItemHooks.Report("TouchControlSizes: give the size back", e); }
            }
        }
    }

    static void Capture(VirtualControlsStyles_Layout styles)
    {
        Refresh(styles);
        var rows = new List<Row>();
        var live = new HashSet<string>();
        foreach (VirtualControlsHardwareConfiguration_Layout hardware in Hardware(styles))
        {
            string? hardwareId = hardware.Id;
            if (string.IsNullOrEmpty(hardwareId)) continue;
            live.Add(hardwareId);
            GameArray<VirtualControlsHardwareConfigurationMapping_Layout> slots = hardware.Slots;
            for (int i = 0; !slots.IsNull && i < slots.Length; i++)
            {
                VirtualControlsHardwareConfigurationMapping_Layout slot = slots[i];
                if (slot.IsNull || slot.Style.IsNull) continue;
                string? id = slot.Style.ControlTypeId;
                if (!IsOurs(id)) continue;
                rows.Add(new Row { Hardware = hardwareId, Slot = slot.SlotId ?? "#" + i, Style = Vanilla(id!, slot.Style.ControlType), Size = SizeOf(id) });
            }
        }
        foreach (Row old in Saved())
            if (!live.Contains(old.Hardware)) rows.Add(old);
        saved = rows;
        var array = new JsonArray();
        foreach (Row r in rows)
            array.Add(new JsonObject { ["hardware"] = r.Hardware, ["slot"] = r.Slot, ["style"] = r.Style, ["size"] = r.Size });
        string file = FilePath;
        if (rows.Count == 0)
        {
            if (File.Exists(file)) File.Delete(file);
            return;
        }
        File.WriteAllText(file, new JsonObject { ["version"] = 1, ["slots"] = array }.ToJsonString());
    }

    static List<Row> Saved()
    {
        if (saved != null) return saved;
        saved = new List<Row>();
        string file = FilePath;
        try
        {
            if (!File.Exists(file) || JsonNode.Parse(File.ReadAllText(file))?["slots"] is not JsonArray array) return saved;
            foreach (JsonNode? node in array)
            {
                if (node is not JsonObject o) continue;
                saved.Add(new Row
                {
                    Hardware = o["hardware"]?.GetValue<string>() ?? "",
                    Slot = o["slot"]?.GetValue<string>() ?? "",
                    Style = o["style"]?.GetValue<string>() ?? "",
                    Size = o["size"]?.GetValue<int>() ?? 0,
                });
            }
        }
        catch (Exception e)
        {
            Log.Error($"TouchControlSizes: {file} is broken ({e.Message}); the touch controls keep the game's sizes");
        }
        return saved;
    }

    static void Apply(VirtualControlsStyles_Layout styles)
    {
        if (styles.IsNull) return;
        List<Row> rows = Saved();
        if (rows.Count == 0) return;
        Refresh(styles);
        foreach (VirtualControlsHardwareConfiguration_Layout hardware in Hardware(styles))
        {
            string? hardwareId = hardware.Id;
            GameArray<VirtualControlsHardwareConfigurationMapping_Layout> slots = hardware.Slots;
            for (int i = 0; !slots.IsNull && i < slots.Length; i++)
            {
                VirtualControlsHardwareConfigurationMapping_Layout slot = slots[i];
                if (slot.IsNull || slot.Style.IsNull) continue;
                string slotId = slot.SlotId ?? "#" + i;
                Row? row = rows.Find(r => r.Hardware == hardwareId && r.Slot == slotId);
                if (row == null || row.Size <= 0 || slot.Style.ControlTypeId != row.Style) continue;
                Table table = slot.Style.ControlType == SlotType.Axis ? Table.Axis : slot.Style.ControlType == SlotType.Group ? Table.Group : Table.Button;
                if (FamilyOf(table, PrefixOf(row.Style)) is not Family family) continue;
                slot.Style.ControlTypeId = Ensure(styles, family, row.Size);
            }
        }
    }
}
