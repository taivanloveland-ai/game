using System.Collections.Generic;
using UnityEngine;

// One kind of thing the player can carry.
public class Item
{
    public readonly string Name;
    public readonly Sprite Icon;
    // The block this item becomes when it is placed in the world, or Air for things that cannot be placed.
    public readonly PlanetOne.SideBlock Block;

    public bool Placeable => Block != PlanetOne.SideBlock.Air;

    public Item(string name, Sprite icon, PlanetOne.SideBlock block = PlanetOne.SideBlock.Air)
    {
        Name = name;
        Icon = icon;
        Block = block;
    }
}

// Everything the player is carrying. There is no limit: each new kind of item adds a stack, and stacks count up forever.
public class Inventory
{
    public const int HotbarSlots = 10;

    public class Stack
    {
        public Item Item;
        public int Count;
    }

    // In the order the player first picked each kind up.
    public readonly List<Stack> Stacks = new List<Stack>();

    public void Add(Item item, int count = 1)
    {
        foreach (Stack stack in Stacks)
        {
            if (stack.Item != item) continue;
            stack.Count += count;
            return;
        }
        Stacks.Add(new Stack { Item = item, Count = count });
    }

    // Tools the player has made, in the order they made them.
    public readonly List<Item> Tools = new List<Item>();

    // What is in each hotbar slot, or null for an empty slot. A slot holds a tool, or a kind of block the player
    // dragged there from their items. A block in a slot is still counted in the stacks above. When the player runs
    // out of a block, its slot stays kept for it, so the next ones they pick up are in the same slot.
    public readonly Item[] Hotbar = new Item[HotbarSlots];

    // Which hotbar slot is equipped, counting from 0, or -1 for none. The first slot is equipped to begin with,
    // so the first tool the player gets is ready to use.
    public int EquippedSlot;

    // The thing in the equipped slot, or null if that slot is empty or nothing is equipped.
    public Item Equipped => EquippedSlot >= 0 && EquippedSlot < Hotbar.Length ? Hotbar[EquippedSlot] : null;

    // A new tool goes in the first empty hotbar slot. If the bar is full it takes the place of the first block on it.
    public void AddTool(Item tool)
    {
        Tools.Add(tool);
        int slot = System.Array.IndexOf(Hotbar, null);
        for (int i = 0; slot < 0 && i < Hotbar.Length; i++)
        {
            if (!IsTool(Hotbar[i])) slot = i;
        }
        if (slot >= 0) Hotbar[slot] = tool;
    }

    public bool IsTool(Item item)
    {
        return Tools.Contains(item);
    }

    // Puts a kind of item the player is carrying in a hotbar slot. It leaves any other slot it was in.
    // Returns false, changing nothing, if the slot holds a tool: tools are never pushed off the bar.
    public bool SetSlot(int slot, Item item)
    {
        if (IsTool(Hotbar[slot])) return false;
        for (int i = 0; i < Hotbar.Length; i++)
        {
            if (Hotbar[i] == item) Hotbar[i] = null;
        }
        Hotbar[slot] = item;
        return true;
    }

    // Swaps what is in two hotbar slots. The equipped slot stays where it is.
    public void SwapSlots(int a, int b)
    {
        Item held = Hotbar[a];
        Hotbar[a] = Hotbar[b];
        Hotbar[b] = held;
    }

    // Takes a block off the hotbar; the player still carries it. Tools stay put.
    public void ClearSlot(int slot)
    {
        if (!IsTool(Hotbar[slot])) Hotbar[slot] = null;
    }

    // Equips a hotbar slot. Choosing the slot that is already equipped puts the tool away.
    public void ToggleSlot(int slot)
    {
        EquippedSlot = EquippedSlot == slot ? -1 : slot;
    }

    // Moves the equipped slot along the hotbar by a number of slots, going round at the ends.
    public void StepSlot(int steps)
    {
        // With nothing equipped, the first step lands on the first slot going right, or the last going left.
        int from = EquippedSlot >= 0 ? EquippedSlot : steps > 0 ? -1 : 0;
        EquippedSlot = ((from + steps) % Hotbar.Length + Hotbar.Length) % Hotbar.Length;
    }

    // Creative mode: nothing the player has is ever used up.
    public bool Infinite;

    // Takes a number of one kind of item away. Returns false, taking nothing, if the player has fewer than that.
    public bool Remove(Item item, int count)
    {
        if (Infinite) return CountOf(item) > 0;
        for (int i = 0; i < Stacks.Count; i++)
        {
            if (Stacks[i].Item != item) continue;
            if (Stacks[i].Count < count) return false;
            Stacks[i].Count -= count;
            if (Stacks[i].Count == 0) Stacks.RemoveAt(i);
            return true;
        }
        return false;
    }

    public int CountOf(Item item)
    {
        foreach (Stack stack in Stacks)
        {
            if (stack.Item == item) return stack.Count;
        }
        return 0;
    }
}
