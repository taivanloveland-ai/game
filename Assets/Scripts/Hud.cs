using UnityEngine;

// On-screen inventory: a hotbar along the bottom for tools and blocks, and a panel of carried items that Tab opens
// and closes. Blocks are dragged from the panel onto the hotbar to build with them.
public class Hud : MonoBehaviour
{
    const int HotbarSlots = Inventory.HotbarSlots;
    const float SlotSize = 56f;
    const float Gap = 6f;
    const int ItemsPerRow = 5;
    const float ItemSlotSize = 84f;
    static readonly Color HighlightColor = new Color32(150, 215, 255, 255);

    Inventory inventory;
    bool itemsOpen;
    string message;
    float messageUntil;
    GUIStyle countStyle, smallCountStyle, nameStyle, titleStyle, messageStyle;

    // What is being dragged with the mouse, and the hotbar slot it was picked up from (-1 if it came from the items panel).
    Item dragged;
    int draggedFrom;

    public void Setup(Inventory inventory)
    {
        this.inventory = inventory;
        GameInput.PointerOverInventory = PointerOverInventory;
    }

    // Shows a short line of text above the hotbar for a moment.
    public void Say(string text)
    {
        message = text;
        messageUntil = Time.time + 1.2f;
    }

    void Update()
    {
        if (GameInput.ItemsPressed())
        {
            itemsOpen = !itemsOpen;
            dragged = null;
        }
        int slot = GameInput.HotbarSlotPressed();
        if (slot >= 0 && slot < HotbarSlots) inventory.ToggleSlot(slot);
        // The mouse wheel moves along the hotbar one slot at a time.
        int scroll = GameInput.ScrollSteps();
        if (scroll != 0) inventory.StepSlot(scroll);
    }

    Rect HotbarRect()
    {
        float width = HotbarSlots * SlotSize + (HotbarSlots - 1) * Gap;
        return new Rect((Screen.width - width) / 2f, Screen.height - SlotSize - 16f, width, SlotSize);
    }

    Rect HotbarSlot(int i)
    {
        Rect bar = HotbarRect();
        return new Rect(bar.x + i * (SlotSize + Gap), bar.y, SlotSize, SlotSize);
    }

    // The panel grows a row at a time as the player finds more kinds of item.
    Rect ItemsRect()
    {
        int rows = Mathf.Max(1, Mathf.CeilToInt(inventory.Stacks.Count / (float)ItemsPerRow));
        float width = ItemsPerRow * ItemSlotSize + (ItemsPerRow + 1) * Gap;
        float height = 40f + rows * (ItemSlotSize + Gap) + Gap;
        return new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
    }

    Rect ItemSlot(int i)
    {
        Rect panel = ItemsRect();
        return new Rect(
            panel.x + Gap + (i % ItemsPerRow) * (ItemSlotSize + Gap),
            panel.y + 40f + (i / ItemsPerRow) * (ItemSlotSize + Gap),
            ItemSlotSize, ItemSlotSize);
    }

    // True while the mouse is over the hotbar or the items panel, or is dragging something.
    bool PointerOverInventory()
    {
        if (inventory == null) return false;
        if (dragged != null) return true;
        Vector2 pointer = GameInput.PointerPosition();
        pointer.y = Screen.height - pointer.y;
        return HotbarRect().Contains(pointer) || (itemsOpen && ItemsRect().Contains(pointer));
    }

    // The hotbar slot under a point on screen, or -1.
    int HotbarSlotAt(Vector2 point)
    {
        for (int i = 0; i < HotbarSlots; i++)
        {
            if (HotbarSlot(i).Contains(point)) return i;
        }
        return -1;
    }

    void OnGUI()
    {
        if (countStyle == null)
        {
            countStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            smallCountStyle = new GUIStyle(countStyle) { fontSize = 14 };
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperCenter };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        HandleDragging(Event.current);

        Rect bar = HotbarRect();
        for (int i = 0; i < HotbarSlots; i++)
        {
            Rect slot = HotbarSlot(i);
            Item item = inventory.Hotbar[i];
            GUI.Box(slot, GUIContent.none);
            // The equipped slot gets a light blue glow and frame, even when it is empty, so scrolling can be followed.
            if (i == inventory.EquippedSlot) DrawHighlight(slot);
            if (item == null || (item == dragged && draggedFrom == i)) continue;
            // Blocks show how many the player has left. A block they have run out of stays in its slot, drawn faint,
            // waiting for more.
            bool tool = inventory.IsTool(item);
            int count = tool ? 1 : inventory.CountOf(item);
            GUI.color = new Color(1f, 1f, 1f, count > 0 ? 1f : 0.3f);
            GUI.DrawTexture(new Rect(slot.x + 6f, slot.y + 6f, slot.width - 12f, slot.height - 12f), item.Icon.texture, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (!tool) GUI.Label(new Rect(slot.x, slot.y, slot.width - 4f, slot.height - 1f), count.ToString(), smallCountStyle);
        }
        GUI.Label(new Rect(bar.x - 120f, bar.y - 22f, bar.width + 240f, 20f), "[1-9, 0] or mouse wheel: equip        [Tab] Items: drag blocks onto the bar to build", nameStyle);

        if (Time.time < messageUntil)
        {
            GUI.Label(new Rect(0f, bar.y - 60f, Screen.width, 30f), message, messageStyle);
        }

        if (itemsOpen) DrawItems();

        // Whatever is being dragged rides along under the mouse.
        if (dragged != null)
        {
            Vector2 mouse = Event.current.mousePosition;
            GUI.DrawTexture(new Rect(mouse.x - 22f, mouse.y - 22f, 44f, 44f), dragged.Icon.texture, ScaleMode.ScaleToFit);
        }
    }

    // Picking up starts on a block in the items panel or on the hotbar. Letting go over a hotbar slot puts it there.
    // A block taken from the hotbar and let go anywhere else comes off the bar.
    void HandleDragging(Event e)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            int slot = HotbarSlotAt(e.mousePosition);
            if (slot >= 0 && inventory.Hotbar[slot] != null)
            {
                dragged = inventory.Hotbar[slot];
                draggedFrom = slot;
            }
            for (int i = 0; itemsOpen && dragged == null && i < inventory.Stacks.Count; i++)
            {
                if (!ItemSlot(i).Contains(e.mousePosition)) continue;
                if (inventory.Stacks[i].Item.Placeable)
                {
                    dragged = inventory.Stacks[i].Item;
                    draggedFrom = -1;
                }
                else
                {
                    Say("Only blocks can go on the bar");
                }
            }
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && dragged != null)
        {
            int slot = HotbarSlotAt(e.mousePosition);
            if (slot >= 0 && draggedFrom >= 0) inventory.SwapSlots(draggedFrom, slot);
            else if (slot >= 0 && !inventory.SetSlot(slot, dragged)) Say("That slot holds a tool");
            else if (slot < 0 && draggedFrom >= 0) inventory.ClearSlot(draggedFrom);
            dragged = null;
        }
    }

    // A soft light blue fill with a brighter frame round it.
    static void DrawHighlight(Rect slot)
    {
        const float edge = 3f;
        GUI.color = new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, 0.28f);
        GUI.DrawTexture(slot, Texture2D.whiteTexture);
        GUI.color = HighlightColor;
        GUI.DrawTexture(new Rect(slot.x, slot.y, slot.width, edge), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(slot.x, slot.yMax - edge, slot.width, edge), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(slot.x, slot.y, edge, slot.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(slot.xMax - edge, slot.y, edge, slot.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawItems()
    {
        int count = inventory.Stacks.Count;
        Rect panel = ItemsRect();
        GUI.Box(panel, GUIContent.none);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x, panel.y + 8f, panel.width, 26f), "Items", titleStyle);

        if (count == 0)
        {
            GUI.Label(new Rect(panel.x, panel.y + 60f, panel.width, 24f), "Nothing yet", nameStyle);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Inventory.Stack stack = inventory.Stacks[i];
            Rect slot = ItemSlot(i);
            GUI.Box(slot, GUIContent.none);
            GUI.DrawTexture(new Rect(slot.x + 22f, slot.y + 20f, slot.width - 44f, slot.height - 40f), stack.Item.Icon.texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(slot.x, slot.y + 2f, slot.width, 18f), stack.Item.Name, nameStyle);
            GUI.Label(new Rect(slot.x, slot.y, slot.width - 6f, slot.height - 2f), "x" + stack.Count, countStyle);
        }
    }
}
