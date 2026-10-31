using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.GameContent;

namespace Terraria.ModLoader;

public class NPCInteractionList(int type)
{
    public class Entry(NPCInteraction interaction, bool enabled = true)
    {
        public NPCInteraction NPCInteraction { get; internal set; } = interaction;
        public bool Enabled { get; internal set; } = enabled;

        public override string ToString() => $"(NPCInteraction: {NPCInteraction}, Enabled: {Enabled})";
    }

    public int Type => type;

    private readonly List<Entry> _entries = new List<Entry>();

    public IEnumerable<NPCInteraction> Interactions => _entries.Select(e => e.NPCInteraction);

    public IReadOnlyList<Entry> Entries => _entries;

    public Entry Prepend(NPCInteraction interaction)
    {
        Entry entry = new(interaction);
        _entries.Insert(0, entry);
        return entry;
    }

    public Entry Append(NPCInteraction interaction)
    {
        Entry entry = new(interaction);
        _entries.Add(entry);
        return entry;
    }

    public Entry InsertAfter(NPCInteraction interactionToRegister, NPCInteraction interactionAfter)
    {
        Entry entry = new(interactionToRegister);
        int index = _entries.FindIndex(e => e.NPCInteraction.Equals(interactionAfter));

        if (index is not -1) {
            _entries.Insert(index + 1, entry);
        }
        else {
            _entries.Add(entry);
        }
        return entry;
    }

    public Entry InsertAfter(NPCInteraction interactionToRegister, Entry interactionAfter)
    {
        return InsertAfter(interactionToRegister, interactionAfter.NPCInteraction);
    }

    public Entry InsertBefore(NPCInteraction interactionToRegister, NPCInteraction interactionBefore)
    {
        Entry entry = new(interactionToRegister);
        int index = _entries.FindIndex(e => e.NPCInteraction.Equals(interactionBefore));

        if (index is not -1) {
            _entries.Insert(index, entry);
        }
        else {
            _entries.Add(entry);
        }
        return entry;
    }

    public Entry InsertBefore(NPCInteraction interactionToRegister, Entry interactionBefore)
    {
        return InsertBefore(interactionToRegister, interactionBefore.NPCInteraction);
    }

    public void Disable(Entry interaction)
    {
        if (interaction != null) interaction.Enabled = false;
    }

    public void Disable(NPCInteraction interaction)
    {
        Entry? entry = _entries.FirstOrDefault(e => e.NPCInteraction.Equals(interaction));
        if (entry != null) entry.Enabled = false;
    }

    public bool IsEnabled(Entry interaction)
    {
        return interaction?.Enabled ?? false;
    }

    public bool IsEnabled(NPCInteraction interaction)
    {
        return _entries.FirstOrDefault(e  => e.NPCInteraction.Equals(interaction))?.Enabled ?? false;
    }
}
