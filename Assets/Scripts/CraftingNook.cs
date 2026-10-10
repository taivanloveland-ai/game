using UnityEngine;

// A little nook in the side of the crashed ship, with a hammer and a wrench hanging in it. Right clicking it walks
// the player over and opens a small crafting menu. It makes two tools: 8 ferns and 5 ice shards make an ice pickaxe,
// and 15 ferns and 10 ice shards make an ice axe.
// The nook is on the ship's front, so it is used from the top-down view.
public class CraftingNook : MonoBehaviour
{
    const int FernsNeeded = 8;
    const int ShardsNeeded = 5;
    // The ice axe takes 15 ferns and 10 ice shards.
    const int AxeFernsNeeded = 15, AxeShardsNeeded = 10;
    // The player has to be within a block of the nook to use it.
    const float Reach = 1.6f;
    // The walk to the nook is given up if the player stops making progress for this long.
    const float StuckSeconds = 1.5f;

    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    static readonly Color32 Frame = new Color32(150, 158, 170, 255), Rail = new Color32(104, 112, 124, 255), Recess = new Color32(30, 34, 42, 255);
    static readonly Color32 HammerHead = new Color32(176, 182, 190, 255), Handle = new Color32(140, 100, 62, 255), WrenchMetal = new Color32(190, 196, 204, 255);
    static readonly Color32 IceLight = new Color32(206, 235, 248, 255), IceDark = new Color32(108, 170, 210, 255), Binding = new Color32(58, 182, 162, 255);
    static readonly Color32 HandleLight = new Color32(178, 134, 90, 255), IceFace = new Color32(160, 208, 236, 255);
    // The nook, one text row per pixel row from the top: F frame, R the rail the tools hang from, d the dark recess,
    // s the cords, M the hammer's head, h its handle, W the wrench.
    static readonly string[] NookPicture =
    {
        "FFFFFFFFFFFFFFFF",
        "FRRRRRRRRRRRRRRF",
        "FdddsddddddsdddF",
        "FdMMMMMdddWdWddF",
        "FdMMMMMdddWdWddF",
        "FdddhdddddWWWddF",
        "FdddhddddddWdddF",
        "FdddhddddddWdddF",
        "FdddhddddddWdddF",
        "FdddhddddddWdddF",
        "FdddhddddddWdddF",
        "FdddhdddddWWWddF",
        "FdddddddddWdWddF",
        "FdddddddddWWWddF",
        "FddddddddddddddF",
        "FFFFFFFFFFFFFFFF",
    };
    // The ice pickaxe is painted at the same fine scale as the snow picture: this many pixels across.
    const int PickaxePixels = 128;

    // True while the crafting menu is showing.
    public bool Open { get; private set; }
    public Item Pickaxe { get; private set; }
    public Item Axe { get; private set; }

    // Something the workbench can make, what it takes, and a few words on what it is for.
    class Recipe
    {
        public Item Tool;
        public int Ferns, Shards;
        public string Use;
    }

    Recipe[] recipes;

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Gatherables gatherables;
    Inventory inventory;
    Hud hud;
    Camera cam;
    // The square of the ship's front that the nook is set into.
    Vector2Int cell;
    Transform picture;
    bool walking;
    Vector2 lastSpot;
    float lastMovedAt;
    GUIStyle titleStyle, lineStyle, missingStyle;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, Gatherables gatherables, Inventory inventory, Hud hud, Camera cam, Vector2Int cell)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.gatherables = gatherables;
        this.inventory = inventory;
        this.hud = hud;
        this.cam = cam;
        this.cell = cell;


        Pickaxe = new Item("Ice pickaxe", PlanetOne.PaintSprite(PickaxePixels, PickaxePixels, new Vector2(0.5f, 0.5f), PickaxePixels, true, (x, y) => PickaxePixel(x, y)));
        Axe = new Item("Ice axe", PlanetOne.PaintSprite(PickaxePixels, PickaxePixels, new Vector2(0.5f, 0.5f), PickaxePixels, true, (x, y) => AxePixel(x, y)));
        recipes = new[]
        {
            new Recipe { Tool = Pickaxe, Ferns = FernsNeeded, Shards = ShardsNeeded, Use = "for mining" },
            new Recipe { Tool = Axe, Ferns = AxeFernsNeeded, Shards = AxeShardsNeeded, Use = "for spiders and bushes" },
        };

        // The picture sits on the ship's front face, which is drawn one square up from the ground in front of it.
        var go = new GameObject("Crafting Nook");
        go.transform.SetParent(planet.TopDownRoot, false);
        picture = go.transform;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = PlanetOne.PaintSprite(16, 16, new Vector2(0.5f, 0f), (x, y) => NookPixel(x, y));
        // Sorted by its bottom edge, which puts it over the ship and under a player standing in front of it.
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
    }

    static Color32 NookPixel(int x, int y)
    {
        switch (NookPicture[y][x])
        {
            case 'F': return Frame;
            case 'R': case 's': return Rail;
            case 'M': return HammerHead;
            case 'h': return Handle;
            case 'W': return WrenchMetal;
            default: return Recess;
        }
    }

    // One pixel of the ice pickaxe picture: a wooden handle running from bottom left to top right, bound with fern
    // near the top, with a curved ice head across the end that tapers to a point on each side.
    static Color32 PickaxePixel(int x, int y)
    {
        var p = new Vector2(x + 0.5f, y + 0.5f);
        var foot = new Vector2(26f, 112f);
        var tip = new Vector2(86f, 44f);
        Vector2 along = (tip - foot).normalized;
        var across = new Vector2(-along.y, along.x);

        // The head is part of a ring centred back down the handle, so it curves toward the hand.
        const float headRadius = 64f, headSpread = 44f;
        Vector2 fromCentre = p - (tip - along * headRadius);
        float spread = Mathf.Abs(Vector2.SignedAngle(along, fromCentre)) / headSpread;
        if (spread < 1f)
        {
            float halfThick = Mathf.Lerp(8f, 1.5f, spread * spread);
            float outward = fromCentre.magnitude - headRadius;
            // The outer edge of the curve catches the light.
            if (Mathf.Abs(outward) <= halfThick) return outward > 0f ? IceLight : IceDark;
        }

        float distance = Vector2.Dot(p - foot, along), length = (tip - foot).magnitude;
        float side = Vector2.Dot(p - foot, across);
        if (distance < 0f || distance > length || Mathf.Abs(side) > 5f) return Clear;
        // Bands of fern wrapped round the handle just below the head.
        float share = distance / length;
        if (share > 0.72f && share < 0.9f && (int)(distance / 4f) % 2 == 0) return Binding;
        return side < -1f ? HandleLight : Handle;
    }

    // One pixel of the ice axe picture. It is shaped after Thor's axe: a long handle running from bottom left to top
    // right, with a heavy head across the end that is a broad curved blade on one side and a blunt hammer on the other.
    static Color32 AxePixel(int x, int y)
    {
        var p = new Vector2(x + 0.5f, y + 0.5f);
        var foot = new Vector2(20f, 116f);
        var tip = new Vector2(82f, 40f);
        Vector2 along = (tip - foot).normalized;
        var across = new Vector2(-along.y, along.x);
        float length = (tip - foot).magnitude;
        // How far up the handle the pixel is, and how far to one side of it: the blade is on the minus side.
        float up = Vector2.Dot(p - foot, along), side = Vector2.Dot(p - foot, across);
        float fromHead = up - (length - 16f);

        // The blade: narrow where it meets the handle, flaring out to a long curved edge.
        if (side < -5f && side > -52f)
        {
            float reach = -side;
            float halfTall = 9f + reach * 0.5f;
            float edge = 50f - fromHead * fromHead / 46f;
            if (Mathf.Abs(fromHead) <= halfTall && reach <= edge)
            {
                // The sharpened edge is pale, the face of the blade is the ice's own blue, and the lower half is in shade.
                if (reach > edge - 6f) return IceLight;
                return fromHead < -halfTall * 0.25f ? IceDark : IceFace;
            }
        }
        // The hammer: a blunt block on the other side, with a lit top edge.
        if (side > 5f && side < 34f && Mathf.Abs(fromHead) <= 15f)
        {
            if (fromHead > 10f || side > 29f) return IceLight;
            return fromHead < -6f ? IceDark : IceFace;
        }
        // The collar that joins the two sides, bound to the handle with fern.
        if (Mathf.Abs(side) <= 8f && Mathf.Abs(fromHead) <= 17f) return (int)((fromHead + 17f) / 5f) % 2 == 0 ? Binding : IceDark;

        if (up < 0f || up > length || Mathf.Abs(side) > 5f) return Clear;
        // A wrap of fern for the grip near the foot of the handle.
        float share = up / length;
        if (share > 0.08f && share < 0.3f && (int)(up / 4f) % 2 == 0) return Binding;
        return side < -1f ? HandleLight : Handle;
    }

    // The planet repeats, so after a trip round it the ship is met again a whole planet away from where it began.
    // This is the corner of the nook's square in the copy of the ship nearest the player.
    Vector2 Corner => planet.Nearest(cell, player.GroundPosition);
    // The spot on the ground at the nook's foot.
    Vector2 Foot => Corner + new Vector2(0.5f, 0f);

    bool InReach => Vector2.Distance(player.GroundPosition, Foot) <= Reach && player.FeetHeight < 0.5f;

    void Update()
    {
        // The nook is only there in the top-down view.
        if (mining.Active)
        {
            Open = false;
            walking = false;
            return;
        }
        picture.position = Foot + new Vector2(0f, 0.02f);
        if (GameInput.RightClickPressed()) ClickAt(cam.ScreenToWorldPoint(GameInput.PointerPosition()));
        if (walking) Walk();
        // Walking away or pressing Escape puts the menu away.
        if (Open && (!InReach || GameInput.CancelPressed())) Open = false;
    }

    // Handles a right click at a point on screen (given in world units). If it is on the nook, the player walks over
    // if they need to and the crafting menu opens. Returns false if the click was somewhere else.
    public bool ClickAt(Vector2 point)
    {
        Vector2 corner = Corner;
        if (point.x < corner.x || point.x > corner.x + 1f || point.y < corner.y || point.y > corner.y + 1f) return false;
        gatherables.CancelWalk();
        if (InReach)
        {
            Open = true;
            return true;
        }
        walking = true;
        lastSpot = player.transform.position;
        lastMovedAt = Time.time;
        return true;
    }

    // Steers the player one frame closer to the nook in a straight line, and opens the menu on arrival.
    void Walk()
    {
        // Give up if the player took over the controls or started walking to something else.
        if (player.HasManualInput || gatherables.Walking)
        {
            walking = false;
            return;
        }
        if (InReach)
        {
            walking = false;
            Open = true;
            return;
        }

        Vector2 spot = player.transform.position;
        if (Vector2.Distance(spot, lastSpot) > 0.05f)
        {
            lastSpot = spot;
            lastMovedAt = Time.time;
        }
        else if (Time.time - lastMovedAt > StuckSeconds)
        {
            walking = false;
            return;
        }

        // Aim for the ground just in front of the nook, jumping when the ground just ahead is higher than the player's feet.
        Vector2 heading = (Foot + Vector2.down * 0.4f - player.GroundPosition).normalized;
        Vector2 justAhead = player.GroundPosition + heading * 0.55f;
        int topAhead = planet.TopAt(Mathf.FloorToInt(justAhead.x), Mathf.FloorToInt(justAhead.y));
        player.Steer(heading, topAhead <= 1 && topAhead > player.FeetHeight + 0.05f);
    }

    // True if the player has what a recipe needs.
    bool CanMake(Recipe recipe)
    {
        return inventory.CountOf(gatherables.PlantItem) >= recipe.Ferns && inventory.CountOf(gatherables.ShardItem) >= recipe.Shards;
    }

    // Uses up the ferns and shards and adds the tool to the hotbar. Returns false if there are not enough.
    bool Make(Recipe recipe)
    {
        if (!CanMake(recipe)) return false;
        inventory.Remove(gatherables.PlantItem, recipe.Ferns);
        inventory.Remove(gatherables.ShardItem, recipe.Shards);
        inventory.AddTool(recipe.Tool);
        hud.Say("Crafted an " + recipe.Tool.Name.ToLower());
        return true;
    }

    public bool CanCraft => CanMake(recipes[0]);
    public bool CanCraftAxe => CanMake(recipes[1]);

    // Makes an ice pickaxe, or an ice axe.
    public bool Craft()
    {
        return Make(recipes[0]);
    }

    public bool CraftAxe()
    {
        return Make(recipes[1]);
    }

    void OnGUI()
    {
        if (!Open) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            missingStyle = new GUIStyle(lineStyle);
            missingStyle.normal.textColor = new Color(1f, 0.55f, 0.5f);
        }

        // One column for each thing that can be made, side by side.
        const float column = 300f, height = 250f;
        float width = column * recipes.Length + 20f;
        var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        GUI.Box(panel, GUIContent.none);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x, panel.y + 8f, width, 26f), "Ship workbench", titleStyle);
        if (GUI.Button(new Rect(panel.xMax - 34f, panel.y + 6f, 28f, 24f), "X")) Open = false;

        for (int i = 0; i < recipes.Length; i++)
        {
            Recipe recipe = recipes[i];
            float left = panel.x + 10f + i * column;
            GUI.DrawTexture(new Rect(left + 16f, panel.y + 46f, 56f, 56f), recipe.Tool.Icon.texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(left + 86f, panel.y + 46f, column - 96f, 56f), recipe.Tool.Name + "\n" + recipe.Use, lineStyle);

            DrawNeed(new Rect(left + 16f, panel.y + 112f, column - 32f, 34f), gatherables.PlantItem, recipe.Ferns);
            DrawNeed(new Rect(left + 16f, panel.y + 150f, column - 32f, 34f), gatherables.ShardItem, recipe.Shards);

            GUI.enabled = CanMake(recipe);
            if (GUI.Button(new Rect(left + 70f, panel.y + 198f, column - 140f, 36f), "Craft")) Make(recipe);
            GUI.enabled = true;
        }
    }

    // One line of the recipe: the item's picture, its name, and how many the player has out of how many are needed.
    void DrawNeed(Rect row, Item item, int needed)
    {
        int have = inventory.CountOf(item);
        GUI.DrawTexture(new Rect(row.x + 8f, row.y + 2f, 30f, 30f), item.Icon.texture, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(row.x + 50f, row.y, row.width - 50f, row.height), item.Name + "   " + have + " / " + needed, have >= needed ? lineStyle : missingStyle);
    }
}
