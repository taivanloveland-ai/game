using UnityEngine;

// On-screen inventory: a hotbar for tools along the bottom, and a panel of carried items that Tab opens and closes.
public class Hud : MonoBehaviour
{
    // The hotbar shows the tools the player has made, first made on the left.
    const int HotbarSlots = 5;
    const float SlotSize = 56f;
    const float Gap = 6f;
    const int ItemsPerRow = 5;
    const float ItemSlotSize = 84f;
    static readonly Color HighlightColor = new Color32(150, 215, 255, 255);

    Inventory inventory;
    bool itemsOpen;
    string message;
    float messageUntil;
    GUIStyle countStyle, nameStyle, titleStyle, messageStyle;

    public void Setup(Inventory inventory)
    {
        this.inventory = inventory;
    }

    // Shows a short line of text above the hotbar for a moment.
    public void Say(string text)
    {
        message = text;
        messageUntil = Time.time + 1.2f;
    }

    void Update()
    {
        if (GameInput.ItemsPressed()) itemsOpen = !itemsOpen;
        int slot = GameInput.HotbarSlotPressed();
        if (slot >= 0 && slot < HotbarSlots) inventory.ToggleSlot(slot);
    }

    void OnGUI()
    {
        if (countStyle == null)
        {
            countStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperCenter };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        float hotbarWidth = HotbarSlots * SlotSize + (HotbarSlots - 1) * Gap;
        float hotbarLeft = (Screen.width - hotbarWidth) / 2f;
        float hotbarTop = Screen.height - SlotSize - 16f;
        for (int i = 0; i < HotbarSlots; i++)
        {
            var slot = new Rect(hotbarLeft + i * (SlotSize + Gap), hotbarTop, SlotSize, SlotSize);
            GUI.Box(slot, GUIContent.none);
            // The equipped tool's slot gets a light blue glow and frame.
            if (i == inventory.EquippedSlot && i < inventory.Tools.Count) DrawHighlight(slot);
            if (i < inventory.Tools.Count)
            {
                GUI.DrawTexture(new Rect(slot.x + 6f, slot.y + 6f, slot.width - 12f, slot.height - 12f), inventory.Tools[i].Icon.texture, ScaleMode.ScaleToFit);
            }
        }
        GUI.Label(new Rect(hotbarLeft - 60f, hotbarTop - 22f, hotbarWidth + 120f, 20f), "Tools    [1-5] equip or put away        [Tab] Items", nameStyle);

        if (Time.time < messageUntil)
        {
            GUI.Label(new Rect(0f, hotbarTop - 60f, Screen.width, 30f), message, messageStyle);
        }

        if (itemsOpen) DrawItems();
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

    // The panel grows a row at a time as the player finds more kinds of item.
    void DrawItems()
    {
        int count = inventory.Stacks.Count;
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)ItemsPerRow));
        float width = ItemsPerRow * ItemSlotSize + (ItemsPerRow + 1) * Gap;
        float height = 40f + rows * (ItemSlotSize + Gap) + Gap;
        var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        GUI.Box(panel, GUIContent.none);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x, panel.y + 8f, width, 26f), "Items", titleStyle);

        if (count == 0)
        {
            GUI.Label(new Rect(panel.x, panel.y + 60f, width, 24f), "Nothing yet", nameStyle);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Inventory.Stack stack = inventory.Stacks[i];
            var slot = new Rect(
                panel.x + Gap + (i % ItemsPerRow) * (ItemSlotSize + Gap),
                panel.y + 40f + (i / ItemsPerRow) * (ItemSlotSize + Gap),
                ItemSlotSize, ItemSlotSize);
            GUI.Box(slot, GUIContent.none);
            GUI.DrawTexture(new Rect(slot.x + 22f, slot.y + 20f, slot.width - 44f, slot.height - 40f), stack.Item.Icon.texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(slot.x, slot.y + 2f, slot.width, 18f), stack.Item.Name, nameStyle);
            GUI.Label(new Rect(slot.x, slot.y, slot.width - 6f, slot.height - 2f), "x" + stack.Count, countStyle);
        }
    }
}
