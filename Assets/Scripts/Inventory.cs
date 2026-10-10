using System.Collections.Generic;
using UnityEngine;

// One kind of thing the player can carry.
public class Item
{
    public readonly string Name;
    public readonly Sprite Icon;

    public Item(string name, Sprite icon)
    {
        Name = name;
        Icon = icon;
    }
}

// Everything the player is carrying. There is no limit: each new kind of item adds a stack, and stacks count up forever.
public class Inventory
{
    public class Stack
    {
        public Item Item;
        public int Count;
    }

    // In the order the player first picked each kind up.
    public readonly List<Stack> Stacks = new List<Stack>();

    public void Add(Item item)
    {
        foreach (Stack stack in Stacks)
        {
            if (stack.Item != item) continue;
            stack.Count++;
            return;
        }
        Stacks.Add(new Stack { Item = item, Count = 1 });
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
