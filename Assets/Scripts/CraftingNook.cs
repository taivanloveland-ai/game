using UnityEngine;

// A little nook in the side of the crashed ship, with a hammer and a wrench hanging in it. Right clicking it walks
// the player over and opens a small crafting menu. It makes two tools: 8 ferns and 5 ice shards make an ice pickaxe,
// and 15 ferns and 10 ice shards make an ice axe. And 20 algae clumps, 15 thorns and 2 thornam vines make thornam
// cleats, worn in the boots slot. An ice axe and 8 thornam sticks make a thornam ice axe, which swings half as fast
// again; and a thornam ice axe and 20 crystal shards make a crystal axe, which swings twice as fast as the ice axe.
// The pickaxe upgrades the same way, with the same things: the thornam pickaxe swings faster, and the crystal pickaxe
// also breaks things in half the hits.
// The nook is on the ship's front, so it is used from the top-down view.
public class CraftingNook : MonoBehaviour
{
    const int FernsNeeded = 8;
    const int ShardsNeeded = 5;
    // The ice axe takes 15 ferns and 10 ice shards.
    const int AxeFernsNeeded = 15, AxeShardsNeeded = 10;
    // Thornam cleats take 20 algae clumps, 15 thorns and 2 thornam vines.
    const int CleatsClumpsNeeded = 20, CleatsThornsNeeded = 15, CleatsVinesNeeded = 2;
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
    // Thornam cleats: boots with thorns on their soles (Taivan's picture, Art/ThornamCleats).
    public Item Cleats { get; private set; }
    // The thornam ice axe: the ice axe with a thornam stick for a handle (Art/ThornamIceAxe). And the crystal axe: the
    // same with a head of cave crystal (Art/CrystalAxe). They swing this many times as fast as the ice axe; each hit
    // does the same. (Until 2026-10-17 the thornam ice axe did 1.5 times the damage instead.)
    public Item ThornAxe { get; private set; }
    // Whether the ship has been fixed, on the workbench's other tab. (Until 2026-10-17 this was only its thrusters,
    // fixed with 3 magical orbs.)
    public bool ShipFixed;
    // What fixing the ship takes, and the battery from the South Pole (Taivan's picture, Art/Battery).
    public Item Battery { get; private set; }
    const int ShipCrystalShards = 30, ShipIceBlocks = 5, ShipIceShards = 20, ShipAlgaeClumps = 30, ShipStoneBlocks = 100, ShipOrbs = 3;
    // Which tab of the workbench is showing: crafting, or fixing the ship.
    bool fixingTab;
    public Item CrystalAxe { get; private set; }
    const float ThornAxeSwings = 1.5f, CrystalAxeSwings = 2f;
    const int ThornAxeSticksNeeded = 8, CrystalAxeShardsNeeded = 20;
    // The pickaxe's upgrades, the same way: the thornam pickaxe (Art/ThornamPickaxe), whose thornam stick swings
    // faster, and the crystal pickaxe (Art/CrystalPickaxe), whose crystal top is more efficient: it breaks things
    // in this share of the hits, and swings as fast as the thornam pickaxe.
    public Item ThornPickaxe { get; private set; }
    public Item CrystalPickaxe { get; private set; }
    const float ThornPickaxeSwings = 1.5f, CrystalPickaxeHits = 0.5f;

    // True for any of the pickaxes.
    public bool IsPickaxe(Item item) => item != null && (item == Pickaxe || item == ThornPickaxe || item == CrystalPickaxe);
    // How many times as fast as the ice pickaxe a pickaxe swings.
    public float PickaxeSwings(Item item) => item == ThornPickaxe || item == CrystalPickaxe ? ThornPickaxeSwings : 1f;
    // How many hits a pickaxe takes to break something that takes the ice pickaxe this many.
    public int PickaxeHits(Item item, int hits) => item == CrystalPickaxe ? Mathf.Max(1, Mathf.CeilToInt(hits * CrystalPickaxeHits)) : hits;

    // Crystal shards, which the cave crystals break into (Taivan's picture, Art/CrystalShard).
    public Item CrystalShard { get; private set; }

    // True for any of the axes.
    public bool IsAxe(Item item) => item != null && (item == Axe || item == ThornAxe || item == CrystalAxe);
    // How many times as fast as the ice axe an axe swings.
    public float AxeSwings(Item item) => item == CrystalAxe ? CrystalAxeSwings : item == ThornAxe ? ThornAxeSwings : 1f;

    // Something the workbench can make, what it takes, and a few words on what it is for.
    class Recipe
    {
        public Item Tool;
        // What it takes: each thing, and how many of it.
        public (Item Item, int Count)[] Needs;
        // The tool it is made from, if it is an upgrade: that tool is needed, and becomes the new one.
        public Item Upgrades;
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


        // Taivan's pictures of the two tools; the drawn-in-code ones stand in if his are missing.
        Sprite[] drawn = ToolPictures();
        Pickaxe = new Item("Ice pickaxe", drawn != null ? drawn[0] : PlanetOne.PaintSprite(PickaxePixels, PickaxePixels, new Vector2(0.5f, 0.5f), PickaxePixels, true, (x, y) => PickaxePixel(x, y)));
        Axe = new Item("Ice axe", drawn != null ? drawn[1] : PlanetOne.PaintSprite(PickaxePixels, PickaxePixels, new Vector2(0.5f, 0.5f), PickaxePixels, true, (x, y) => AxePixel(x, y)));
        Cleats = new Item("Thornam cleats", Picture("Art/ThornamCleats") ?? Pickaxe.Icon) { Boots = true };
        ThornAxe = new Item("Thornam ice axe", Picture("Art/ThornamIceAxe") ?? Axe.Icon);
        CrystalAxe = new Item("Crystal axe", Picture("Art/CrystalAxe") ?? Axe.Icon);
        ThornPickaxe = new Item("Thornam pickaxe", Picture("Art/ThornamPickaxe") ?? Pickaxe.Icon);
        CrystalPickaxe = new Item("Crystal pickaxe", Picture("Art/CrystalPickaxe") ?? Pickaxe.Icon);
        Battery = Picture("Art/Battery") != null ? new Item("Battery", Picture("Art/Battery")) : null;
        CrystalShard = new Item("Crystal shard", Picture("Art/CrystalShard") ?? gatherables.ShardItem.Icon);
        recipes = new[]
        {
            new Recipe { Tool = Pickaxe, Needs = new[] { (gatherables.PlantItem, FernsNeeded), (gatherables.ShardItem, ShardsNeeded) }, Use = "for mining" },
            new Recipe { Tool = Axe, Needs = new[] { (gatherables.PlantItem, AxeFernsNeeded), (gatherables.ShardItem, AxeShardsNeeded) }, Use = "for spiders and bushes" },
            new Recipe { Tool = Cleats, Needs = new[] { (gatherables.ClumpItem, CleatsClumpsNeeded), (gatherables.ThornItem, CleatsThornsNeeded), (gatherables.VineItem, CleatsVinesNeeded) }, Use = "worn: grip on ice, run faster" },
            new Recipe { Tool = ThornAxe, Upgrades = Axe, Needs = new[] { (gatherables.StickItem, ThornAxeSticksNeeded) }, Use = "your ice axe, upgraded: swings faster" },
            new Recipe { Tool = CrystalAxe, Upgrades = ThornAxe, Needs = new[] { (CrystalShard, CrystalAxeShardsNeeded) }, Use = "your thornam ice axe, upgraded: swings faster still" },
            new Recipe { Tool = ThornPickaxe, Upgrades = Pickaxe, Needs = new[] { (gatherables.StickItem, ThornAxeSticksNeeded) }, Use = "your ice pickaxe, upgraded: swings faster" },
            new Recipe { Tool = CrystalPickaxe, Upgrades = ThornPickaxe, Needs = new[] { (CrystalShard, CrystalAxeShardsNeeded) }, Use = "your thornam pickaxe, upgraded: fewer hits" },
        };

        // The picture sits on the ship's front face, which is drawn one square up from the ground in front of it.
        var go = new GameObject("Crafting Nook");
        go.transform.SetParent(planet.TopDownRoot, false);
        picture = go.transform;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = PlanetOne.PaintSprite(16, 16, new Vector2(0.5f, 0f), (x, y) => NookPixel(x, y));
        // Sorted by its bottom edge, which puts it over the ship and under a player standing in front of it.
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        // Taivan's picture of the ship has the nook in it already.
        if (CrashedShip.HasPicture) renderer.enabled = false;
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

    // One of Taivan's pictures, unchanged and drawn sharp, as an item's picture. Null if it is missing.
    static Sprite Picture(string path)
    {
        var picture = Resources.Load<Texture2D>(path);
        if (picture == null) return null;
        var texture = new Texture2D(picture.width, picture.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(PlanetOne.ReadPixels(picture));
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, picture.width, picture.height), new Vector2(0.5f, 0.5f), picture.height);
    }

    // Taivan's picture of the ice pickaxe (left) and the ice axe (right), side by side in Art/IceTools, cut into one
    // sharp square picture each without changing a pixel. Null if the picture is missing.
    static Sprite[] ToolPictures()
    {
        var sheet = Resources.Load<Texture2D>("Art/IceTools");
        if (sheet == null) return null;
        Color32[] pixels = PlanetOne.ReadPixels(sheet);
        int size = sheet.height;
        var tools = new Sprite[2];
        for (int i = 0; i < 2; i++)
        {
            var cut = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) cut[y * size + x] = pixels[y * sheet.width + i * size + x];
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(cut);
            texture.Apply();
            tools[i] = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return tools;
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
        // It is part of the ship, on the surface, so in a cave it is not there.
        picture.gameObject.SetActive(!planet.InCave);
        if (planet.InCave)
        {
            Open = false;
            walking = false;
            return;
        }
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
        if (recipe.Upgrades != null && !inventory.Tools.Contains(recipe.Upgrades)) return false;
        foreach ((Item item, int count) in recipe.Needs)
        {
            if (inventory.CountOf(item) < count) return false;
        }
        return true;
    }

    // Uses up what a recipe takes and gives the player what it makes: a tool goes on the hotbar, anything else among
    // their items. Returns false if there is not enough.
    bool Make(Recipe recipe)
    {
        if (!CanMake(recipe)) return false;
        foreach ((Item item, int count) in recipe.Needs) inventory.Remove(item, count);
        bool tool = recipe.Tool != Cleats;
        if (recipe.Upgrades != null)
        {
            // The old tool becomes the new one, in the same place on the hotbar.
            inventory.Tools[inventory.Tools.IndexOf(recipe.Upgrades)] = recipe.Tool;
            int slot = System.Array.IndexOf(inventory.Hotbar, recipe.Upgrades);
            if (slot >= 0) inventory.Hotbar[slot] = recipe.Tool;
            hud.Say("Upgraded your " + recipe.Upgrades.Name.ToLower() + " to a " + recipe.Tool.Name.ToLower());
            return true;
        }
        if (tool) inventory.AddTool(recipe.Tool);
        else inventory.Add(recipe.Tool);
        hud.Say((tool ? "Crafted an " : "Crafted ") + recipe.Tool.Name.ToLower() + (recipe.Tool.Boots ? ". Put them on in the boots slot [Tab]" : ""), recipe.Tool.Boots ? 3f : 1.2f);
        return true;
    }

    public bool CanCraft => CanMake(recipes[0]);
    public bool CanCraftAxe => CanMake(recipes[1]);

    // Makes an ice pickaxe, an ice axe, or thornam cleats.
    public bool Craft()
    {
        return Make(recipes[0]);
    }

    public bool CraftAxe()
    {
        return Make(recipes[1]);
    }

    public bool CraftCleats()
    {
        return Make(recipes[2]);
    }

    public bool UpgradeAxe()
    {
        return Make(recipes[3]);
    }

    public bool UpgradeToCrystalAxe()
    {
        return Make(recipes[4]);
    }

    public bool UpgradePickaxe()
    {
        return Make(recipes[5]);
    }

    public bool UpgradeToCrystalPickaxe()
    {
        return Make(recipes[6]);
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
        // As tall as the longest list of what something takes.
        int rows = 0;
        foreach (Recipe each in recipes) rows = Mathf.Max(rows, each.Needs.Length + (each.Upgrades != null ? 1 : 0));
        // As many to a row as fit across the screen. If it is still too big, it is all drawn smaller to fit.
        const float column = 400f;
        int across = Mathf.Clamp((int)((Screen.width - 40f) / column), 2, recipes.Length);
        float block = 112f + rows * 38f + 50f;
        int lines = (recipes.Length + across - 1) / across;
        const float tabs = 40f;
        float height = 40f + tabs + lines * block;
        float width = column * Mathf.Min(across, recipes.Length) + 20f;
        float shrink = Mathf.Min(1f, (Screen.height - 20f) / height, (Screen.width - 20f) / width);
        Matrix4x4 before = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f), Quaternion.identity, new Vector3(shrink, shrink, 1f)) * Matrix4x4.TRS(new Vector3(-Screen.width / 2f, -Screen.height / 2f, 0f), Quaternion.identity, Vector3.one) * before;
        var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        GUI.Box(panel, GUIContent.none);
        GUI.Box(panel, GUIContent.none);
        GameText.Label(new Rect(panel.x, panel.y + 8f, width, 26f), "Ship workbench", titleStyle);
        if (GameText.Button(new Rect(panel.xMax - 34f, panel.y + 6f, 28f, 24f), "X")) Open = false;
        // The two tabs: crafting, and fixing the ship.
        float tabWide = Mathf.Min(220f, width / 2f - 20f);
        GUI.color = fixingTab ? new Color(0.7f, 0.7f, 0.7f) : Color.white;
        if (GameText.Button(new Rect(panel.center.x - tabWide - 4f, panel.y + 38f, tabWide, 30f), "Crafting")) fixingTab = false;
        GUI.color = fixingTab ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        if (GameText.Button(new Rect(panel.center.x + 4f, panel.y + 38f, tabWide, 30f), "Fix the ship")) fixingTab = true;
        GUI.color = Color.white;
        if (fixingTab)
        {
            DrawFixing(new Rect(panel.x, panel.y + 40f + tabs, panel.width, panel.height - 40f - tabs));
            GUI.matrix = before;
            return;
        }

        for (int i = 0; i < recipes.Length; i++)
        {
            Recipe recipe = recipes[i];
            float left = panel.x + 10f + (i % across) * column;
            float top = panel.y + tabs + (i / across) * block;
            GUI.DrawTexture(new Rect(left + 16f, top + 46f, 56f, 56f), recipe.Tool.Icon.texture, ScaleMode.ScaleToFit);
            GameText.Label(new Rect(left + 86f, top + 46f, column - 96f, 56f), recipe.Tool.Name + "\n" + recipe.Use, lineStyle);
            int line = 0;
            if (recipe.Upgrades != null)
            {
                var row = new Rect(left + 16f, top + 112f, column - 32f, 34f);
                bool have = inventory.Tools.Contains(recipe.Upgrades);
                GUI.DrawTexture(new Rect(row.x + 8f, row.y + 2f, 30f, 30f), recipe.Upgrades.Icon.texture, ScaleMode.ScaleToFit);
                GameText.Label(new Rect(row.x + 50f, row.y, row.width - 50f, row.height), recipe.Upgrades.Name + (have ? "   (yours)" : "   (needed)"), have ? lineStyle : missingStyle);
                line = 1;
            }

            for (int n = 0; n < recipe.Needs.Length; n++) DrawNeed(new Rect(left + 16f, top + 112f + (n + line) * 38f, column - 32f, 34f), recipe.Needs[n].Item, recipe.Needs[n].Count);

            GUI.enabled = CanMake(recipe);
            if (GameText.Button(new Rect(left + 70f, top + block - 46f, column - 140f, 36f), recipe.Upgrades != null ? "Upgrade" : "Craft")) Make(recipe);
            GUI.enabled = true;
        }
        GUI.matrix = before;
    }

    // What fixing the ship takes: each thing and how many. Worked out when first needed, once everything is made.
    (Item Item, int Count)[] shipNeeds;
    (Item Item, int Count)[] ShipNeeds
    {
        get
        {
            if (shipNeeds != null) return shipNeeds;
            var blocks = planet.GetComponent<Mining>();
            shipNeeds = new[]
            {
                (CrystalShard, ShipCrystalShards), (blocks.IceItem, ShipIceBlocks), (gatherables.ShardItem, ShipIceShards),
                (gatherables.ClumpItem, ShipAlgaeClumps), (blocks.StoneItem, ShipStoneBlocks), (planet.GetComponent<IceBoss>().OrbItem, ShipOrbs),
                (Battery, 1),
            };
            return shipNeeds;
        }
    }

    // The ship's tab: what fixing it takes, and the button to fix it.
    void DrawFixing(Rect area)
    {
        float left = area.center.x - 190f;
        GameText.Label(new Rect(left, area.y + 10f, 380f, 30f), "The ship", titleStyle);
        if (ShipFixed)
        {
            GameText.Label(new Rect(left, area.y + 50f, 380f, 34f), "Fixed!", lineStyle);
            return;
        }
        GameText.Label(new Rect(left, area.y + 46f, 380f, 34f), "Broken and smoking. Fixing it takes:", lineStyle);
        (Item Item, int Count)[] needs = ShipNeeds;
        for (int i = 0; i < needs.Length; i++) DrawNeed(new Rect(left, area.y + 82f + i * 36f, 380f, 34f), needs[i].Item, needs[i].Count);
        GUI.enabled = CanFixShip;
        if (GameText.Button(new Rect(left + 60f, area.y + 92f + needs.Length * 36f, 260f, 36f), "Fix the ship")) FixShip();
        GUI.enabled = true;
    }

    bool CanFixShip
    {
        get
        {
            if (ShipFixed) return false;
            foreach ((Item item, int count) in ShipNeeds)
            {
                if (item == null || inventory.CountOf(item) < count) return false;
            }
            return true;
        }
    }

    // Uses up everything the ship needs and fixes it: the ship is hidden in smoke, and is whole when it clears (see
    // CrashedShip). Returns false if there is not enough.
    public bool FixShip()
    {
        if (!CanFixShip) return false;
        foreach ((Item item, int count) in ShipNeeds) inventory.Remove(item, count);
        ShipFixed = true;
        Open = false;
        hud.Say("The ship is fixed!", 4f);
        return true;
    }

    // One line of the recipe: the item's picture, its name, and how many the player has out of how many are needed.
    void DrawNeed(Rect row, Item item, int needed)
    {
        int have = inventory.CountOf(item);
        GUI.DrawTexture(new Rect(row.x + 8f, row.y + 2f, 30f, 30f), item.Icon.texture, ScaleMode.ScaleToFit);
        GameText.Label(new Rect(row.x + 50f, row.y, row.width - 50f, row.height), item.Name + "   " + have + " / " + needed, have >= needed ? lineStyle : missingStyle);
    }
}
