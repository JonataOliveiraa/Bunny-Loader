using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using BunnyLoader;
using BunnyLoader.Interop;
using ContolType = VirtualControlsHardwareConfigurationMapping_Layout.ContolType;

namespace Terraria.ModLoader.Core;

internal static class TouchButtonLayouts
{
    sealed class Row
    {
        public string Slot = "";
        public int Index;
        public string? Action;
        public List<string?> Group = new();
    }

    static Dictionary<string, List<Row>>? saved;

    static string FilePath => Path.Combine(Main.SavePath ?? "", "BunnyLoader.touchbuttons.json");

    internal static void Save(VirtualControlsStyles_Layout styles, Action run)
    {
        if (TouchButtonLoader.Count == 0)
        {
            run();
            return;
        }
        try { Capture(styles); }
        catch (Exception e) { ItemHooks.Report("TouchButtons: save the layout", e); }
        WithoutMods(Profiles(styles), () =>
        {
            run();
            return 0;
        });
    }

    internal static T WithoutMods<T>(IReadOnlyList<VirtualControlsProfile_Layout> profiles, Func<T> run)
    {
        if (TouchButtonLoader.Count == 0) return run();
        List<Action> restore = Strip(profiles);
        try { return run(); }
        finally
        {
            for (int i = restore.Count - 1; i >= 0; i--)
            {
                try { restore[i](); }
                catch (Exception e) { ItemHooks.Report("TouchButtons: give the button back", e); }
            }
        }
    }

    internal static void Apply(VirtualControlsStyles_Layout styles)
    {
        if (styles.IsNull || TouchButtonLoader.Count == 0) return;
        Dictionary<string, List<Row>> data = Saved();
        int unbound = TouchButtonLoader.Unbound;
        foreach (VirtualControlsProfile_Layout profile in Profiles(styles))
        {
            if (!data.TryGetValue(profile.Id ?? "", out List<Row>? rows) || rows.Count == 0) continue;
            GameArray<VirtualControlsBinding_Layout> controls = profile.Controls;
            if (controls.IsNull) continue;
            var bySlot = new Dictionary<string, VirtualControlsBinding_Layout>();
            for (int i = 0; i < controls.Length; i++)
                if (!controls[i].IsNull) bySlot[SlotKey(controls[i], i)] = controls[i];
            foreach (Row row in rows)
            {
                if (!bySlot.TryGetValue(row.Slot, out VirtualControlsBinding_Layout binding))
                    binding = row.Index < controls.Length ? controls[row.Index] : default;
                if (binding.IsNull) continue;
                if (TouchButtonLoader.ByKey(row.Action) is ModTouchButton main && (int)binding.Action == unbound)
                    binding.Action = (ContolType)TouchButtonLoader.ActionOf(main);
                GameArray<ContolType> groups = binding.GroupActions;
                if (groups.IsNull) continue;
                for (int j = 0; j < row.Group.Count && j < groups.Length; j++)
                    if (TouchButtonLoader.ByKey(row.Group[j]) is ModTouchButton entry && (int)groups[j] == unbound)
                        groups[j] = (ContolType)TouchButtonLoader.ActionOf(entry);
            }
        }
    }

    static Dictionary<string, List<Row>> Saved()
    {
        if (saved != null) return saved;
        saved = new Dictionary<string, List<Row>>();
        string file = FilePath;
        try
        {
            if (!File.Exists(file)) return saved;
            if (JsonNode.Parse(File.ReadAllText(file))?["profiles"] is not JsonObject profiles) return saved;
            foreach ((string id, JsonNode? node) in profiles)
            {
                if (node is not JsonArray array) continue;
                var rows = new List<Row>();
                foreach (JsonNode? item in array)
                {
                    if (item is not JsonObject o) continue;
                    var row = new Row
                    {
                        Slot = o["slot"]?.GetValue<string>() ?? "",
                        Index = o["index"]?.GetValue<int>() ?? 0,
                        Action = o["action"]?.GetValue<string>(),
                    };
                    if (o["group"] is JsonArray group)
                        foreach (JsonNode? g in group) row.Group.Add(g?.GetValue<string>());
                    rows.Add(row);
                }
                saved[id] = rows;
            }
        }
        catch (Exception e)
        {
            Log.Error($"TouchButtons: {file} is broken ({e.Message}); starting with no mod buttons in the layouts");
        }
        return saved;
    }

    static void Capture(VirtualControlsStyles_Layout styles)
    {
        int unbound = TouchButtonLoader.Unbound;
        Dictionary<string, List<Row>> old = Saved();
        string? KeyOf(int action) => TouchButtonLoader.IsModded(action) ? TouchButtonLoader.ByAction(action)?.FullName : null;
        string? Kept(int action, string? previous) => action == unbound && previous != null && TouchButtonLoader.ByKey(previous) == null ? previous : null;

        var profiles = new Dictionary<string, List<Row>>();
        var live = new HashSet<string>();
        foreach (VirtualControlsProfile_Layout profile in Profiles(styles))
        {
            string id = profile.Id ?? "";
            if (id.Length == 0) continue;
            live.Add(id);
            var before = new Dictionary<string, Row>();
            if (old.TryGetValue(id, out List<Row>? previousRows))
                foreach (Row r in previousRows) before[r.Slot] = r;
            var rows = new List<Row>();
            GameArray<VirtualControlsBinding_Layout> controls = profile.Controls;
            for (int i = 0; !controls.IsNull && i < controls.Length; i++)
            {
                VirtualControlsBinding_Layout binding = controls[i];
                if (binding.IsNull) continue;
                string slot = SlotKey(binding, i);
                before.TryGetValue(slot, out Row? prev);
                int action = (int)binding.Action;
                var row = new Row { Slot = slot, Index = i, Action = TouchButtonLoader.IsModded(action) ? KeyOf(action) : Kept(action, prev?.Action) };
                bool any = row.Action != null;
                GameArray<ContolType> groups = binding.GroupActions;
                for (int j = 0; !groups.IsNull && j < groups.Length; j++)
                {
                    int g = (int)groups[j];
                    string? key = TouchButtonLoader.IsModded(g) ? KeyOf(g) : Kept(g, prev != null && j < prev.Group.Count ? prev.Group[j] : null);
                    row.Group.Add(key);
                    if (key != null) any = true;
                }
                if (any) rows.Add(row);
            }
            if (rows.Count > 0) profiles[id] = rows;
        }
        foreach ((string id, List<Row> rows) in old)
            if (!profiles.ContainsKey(id) && !live.Contains(id)) profiles[id] = rows;

        saved = profiles;
        var json = new JsonObject { ["version"] = 1 };
        var all = new JsonObject();
        foreach ((string id, List<Row> rows) in profiles)
        {
            var array = new JsonArray();
            foreach (Row r in rows)
            {
                var group = new JsonArray();
                foreach (string? g in r.Group) group.Add(g == null ? null : JsonValue.Create(g));
                array.Add(new JsonObject { ["slot"] = r.Slot, ["index"] = r.Index, ["action"] = r.Action, ["group"] = group });
            }
            all[id] = array;
        }
        json["profiles"] = all;
        File.WriteAllText(FilePath, json.ToJsonString());
    }

    static List<Action> Strip(IReadOnlyList<VirtualControlsProfile_Layout> profiles)
    {
        var unbound = (ContolType)TouchButtonLoader.Unbound;
        var undo = new List<Action>();
        foreach (VirtualControlsProfile_Layout profile in profiles)
        {
            GameArray<VirtualControlsBinding_Layout> controls = profile.Controls;
            for (int i = 0; !controls.IsNull && i < controls.Length; i++)
            {
                VirtualControlsBinding_Layout binding = controls[i];
                if (binding.IsNull) continue;
                ContolType action = binding.Action;
                if (TouchButtonLoader.IsModded((int)action))
                {
                    undo.Add(() => binding.Action = action);
                    binding.Action = unbound;
                }
                GameArray<ContolType> groups = binding.GroupActions;
                for (int j = 0; !groups.IsNull && j < groups.Length; j++)
                {
                    ContolType g = groups[j];
                    if (!TouchButtonLoader.IsModded((int)g)) continue;
                    int index = j;
                    undo.Add(() => groups[index] = g);
                    groups[j] = unbound;
                }
            }
        }
        return undo;
    }

    static List<VirtualControlsProfile_Layout> Profiles(VirtualControlsStyles_Layout styles)
    {
        var result = new List<VirtualControlsProfile_Layout>();
        if (styles.IsNull) return result;
        GameArray<VirtualControlsProfile_Layout> all = styles.Profiles;
        for (int i = 0; !all.IsNull && i < all.Length; i++)
            if (!all[i].IsNull) result.Add(all[i]);
        VirtualControlsProfile_Layout active = styles._activeProfile;
        if (!active.IsNull && !result.Contains(active)) result.Add(active);
        return result;
    }

    static string SlotKey(VirtualControlsBinding_Layout binding, int i) => binding.SlotId is { Length: > 0 } id ? id : "#" + i;
}
