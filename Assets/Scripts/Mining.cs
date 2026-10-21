using UnityEngine;

// Mining with the pickaxe. Left clicking a block within reach hits it: in the side-scroller the block clicked, and
// top-down the top block of the square clicked. Snow breaks in 1 hit,
// ice in 2 and stone in 5. A broken block drops as an item that slides to the player. Grey rocks count as stone.
public class Mining : MonoBehaviour
{
    // A block can be mined if it is within this many blocks of the player.
    const float Reach = 3f;
    const int SnowHits = 1, IceHits = 2, StoneHits = 5;
    // With the mouse button held down, the pickaxe hits once every this many seconds.
    const float HeldHitSeconds = 0.4f;
    // A mined block slides to the player from anywhere within reach, and is drawn small as a loose item.
    const float DropPullRadius = Reach + 1.5f;
    const float DropScale = 0.35f;
    // Pictures here are painted at the same fine scale as the snow picture.
    const int FinePixels = 128;
    // How many stages of cracks are drawn in code, if Taivan's picture of them is missing.
    const int CrackStages = 4;

    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    static readonly Color32 CrackColor = new Color32(18, 26, 40, 215);
    static readonly Color32 StoneColor = new Color32(70, 80, 96, 255), StoneSpeck = new Color32(88, 99, 116, 255), StoneDark = new Color32(56, 65, 80, 255);
    // The cracks that spread across a block as it is hit, as lines from (x1, y1) to (x2, y2) on the block's picture.
    // Each stage of damage adds the next two.
    static readonly float[][] CrackLines =
    {
        new[] { 64f, 64f, 40f, 30f }, new[] { 64f, 64f, 96f, 44f },
        new[] { 64f, 64f, 84f, 104f }, new[] { 64f, 64f, 28f, 90f },
        new[] { 40f, 30f, 18f, 36f }, new[] { 96f, 44f, 114f, 22f },
        new[] { 84f, 104f, 106f, 116f }, new[] { 28f, 90f, 12f, 114f },
    };

    public enum Result { NotMining, NoPickaxe, Nothing, TooFar, CannotBreak, Hit, Broke }

    public Item SnowItem { get; private set; }
    // The rocky planet's ground, dug: sand (Taivan, 2026-10-20). Placed, it is a block of the ground as it is where it
    // is put: sand on the rocky planet, snow on the frost planet.
    public Item SandItem { get; private set; }
    public Item IceItem { get; private set; }
    public Item StoneItem { get; private set; }
    public Item AlgaeItem { get; private set; }
    // Set by things that take a click with the pickaxe for themselves (the cave crystals): true for a click they took.
    public System.Func<Vector2, bool> ClickTaken;

    PlanetOne planet;
    PlayerController player;
    MiningMode view;
    Inventory inventory;
    Hud hud;
    Pickups pickups;
    CraftingNook nook;
    Gatherables gatherables;
    Camera cam;

    // The block being worked on (its square of the world, then its level) and how many times it has been hit.
    // Hitting a different block starts again.
    Vector3Int damaged;
    int hits;
    float nextHeldHit;
    SpriteRenderer cracks;
    Sprite[] crackSprites;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode view, Inventory inventory, Hud hud, Pickups pickups, CraftingNook nook, Gatherables gatherables, Camera cam)
    {
        this.planet = planet;
        this.player = player;
        this.view = view;
        this.inventory = inventory;
        this.hud = hud;
        this.pickups = pickups;
        this.nook = nook;
        this.gatherables = gatherables;
        this.cam = cam;

        SnowItem = new Item("Snow", planet.SnowIcon != null ? planet.SnowIcon : planet.Square, PlanetOne.SideBlock.Snow);
        SandItem = new Item("Sand", planet.SandIcon != null ? planet.SandIcon : planet.Square, PlanetOne.SideBlock.Snow);
        IceItem = new Item("Ice", planet.IceSprite != null ? planet.IceSprite : planet.IceSideSprites[0], PlanetOne.SideBlock.Ice);
        AlgaeItem = new Item("Algae", planet.AlgaeLookSprite(0) ?? PlanetOne.PaintSprite(16, 16, new Vector2(0.5f, 0.5f), 16, (x, y) => PlanetOne.AlgaePixel(0, x, y)), PlanetOne.SideBlock.Algae);
        StoneItem = new Item("Stone", PlanetOne.PaintSprite(FinePixels, FinePixels, new Vector2(0.5f, 0.5f), FinePixels, true, StonePixel), PlanetOne.SideBlock.Stone);

        // Taivan's cracks, from the first little one to the biggest. Drawn in code if his picture is missing.
        crackSprites = CrackPictures();
        if (crackSprites == null)
        {
            crackSprites = new Sprite[CrackStages];
            for (int stage = 0; stage < CrackStages; stage++)
            {
                int lines = (stage + 1) * 2;
                crackSprites[stage] = PlanetOne.PaintSprite(FinePixels, FinePixels, new Vector2(0.5f, 0.5f), FinePixels, true, (x, y) => CrackPixel(x, y, lines));
            }
        }
        var go = new GameObject("Cracks");
        go.transform.SetParent(transform, false);
        cracks = go.AddComponent<SpriteRenderer>();
        cracks.sortingOrder = MiningMode.ObjectSortingOrder;
        cracks.enabled = false;
    }

    // Taivan's picture of the cracks in Art/BlockCracks: square frames side by side, smallest first, each cut out as
    // its own sharp picture one block across, without changing a pixel. Null if the picture is missing.
    static Sprite[] CrackPictures()
    {
        var sheet = Resources.Load<Texture2D>("Art/BlockCracks");
        if (sheet == null) return null;
        Color32[] pixels = PlanetOne.ReadPixels(sheet);
        int size = sheet.height, count = sheet.width / size;
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            var cut = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) cut[y * size + x] = pixels[y * sheet.width + i * size + x];
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(cut);
            texture.Apply();
            frames[i] = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return frames;
    }

    // Which stage of cracks shows after a number of hits on a block that breaks after `needed`: the first stage after
    // the first hit and the last one just before it breaks, with the rest spread between. A block that breaks on its
    // second hit shows the middle stage.
    int CrackStage(int hits, int needed)
    {
        int last = crackSprites.Length - 1;
        if (needed <= 2) return last / 2;
        return Mathf.Clamp(Mathf.RoundToInt((hits - 1) * last / (float)(needed - 2)), 0, last);
    }

    // Plain stone with a few lighter and darker flecks.
    static Color32 StonePixel(int x, int y)
    {
        int h = (x / 8 * 73856093) ^ (y / 8 * 19349663);
        h = (h ^ (h >> 13)) * 0x5bd1e995;
        int roll = (h >> 15) & 31;
        return roll == 0 ? StoneSpeck : roll == 1 ? StoneDark : StoneColor;
    }

    // See-through except along the first `lines` crack lines.
    static Color32 CrackPixel(int x, int y, int lines)
    {
        var p = new Vector2(x + 0.5f, y + 0.5f);
        for (int i = 0; i < lines; i++)
        {
            var a = new Vector2(CrackLines[i][0], CrackLines[i][1]);
            var b = new Vector2(CrackLines[i][2], CrackLines[i][3]);
            float along = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
            if (Vector2.Distance(p, a + (b - a) * along) <= 1.6f) return CrackColor;
        }
        return Clear;
    }

    void Update()
    {
        // Cracks only show on the block being worked on. In the side-scroller that is when the row or column it
        // belongs to is on screen. Top-down they are drawn on the top of its square, which is where it was clicked.
        var square = new Vector2Int(damaged.x, damaged.y);
        cracks.enabled = hits > 0 && (!view.Active || view.Shows(square));
        if (cracks.enabled)
        {
            cracks.transform.position = view.Active
                ? new Vector2(view.Along(square) + 0.5f, damaged.z + 0.5f)
                : new Vector2(square.x + 0.5f, square.y + 0.5f + (damaged.z + 1) * PlanetOne.HeightScale);
        }
        // A click is one hit. Holding the button down keeps hitting whatever the mouse is over, at a steady pace.
        bool pressed = GameInput.LeftClickPressed();
        bool held = !pressed && GameInput.LeftClickHeld() && Time.time >= nextHeldHit && nook.IsPickaxe(inventory.Equipped);
        if (!pressed && !held) return;
        // The upgraded pickaxes swing faster.
        nextHeldHit = Time.time + HeldHitSeconds / nook.PickaxeSwings(inventory.Equipped);
        ClickAt(cam.ScreenToWorldPoint(GameInput.PointerPosition()));
    }

    static int HitsFor(PlanetOne.SideBlock block)
    {
        // Algae is as soft as snow.
        return block == PlanetOne.SideBlock.Snow || block == PlanetOne.SideBlock.Algae ? SnowHits : block == PlanetOne.SideBlock.Ice ? IceHits : StoneHits;
    }

    Item ItemFor(PlanetOne.SideBlock block)
    {
        return block == PlanetOne.SideBlock.Snow ? (PlanetOne.Rocky ? SandItem : SnowItem) : block == PlanetOne.SideBlock.Ice ? IceItem : block == PlanetOne.SideBlock.Algae ? AlgaeItem : StoneItem;
    }

    // Handles a left click at a point on screen (given in world units): one hit with the pickaxe on the block there.
    public Result ClickAt(Vector2 point)
    {
        if (view.Stepping) return Result.NotMining;
        bool side = view.Active;
        // The square of the world the clicked block is in, and its level. Top-down it is the top block of the square.
        int x = Mathf.FloorToInt(point.x), level;
        Vector2Int square;
        if (side)
        {
            level = Mathf.FloorToInt(point.y);
            square = view.Cell(x);
        }
        else
        {
            // A click on a fern or ice shard collects it instead.
            if (gatherables.ThingUnder(point)) return Result.Nothing;
            if (ClickTaken != null && ClickTaken(point)) return Result.Nothing;
            square = planet.SquareAt(point);
            level = planet.TopAt(square.x, square.y) - 1;
        }
        PlanetOne.SideBlock block = planet.SideBlockAt(square.x, square.y, level);
        if (block == PlanetOne.SideBlock.Air) return Result.Nothing;
        if (!nook.IsPickaxe(inventory.Equipped))
        {
            // With a block in hand the click is for building, so there is nothing to say about mining. Top-down,
            // clicks on the ground are too common to comment on each one.
            if (side && (inventory.Equipped == null || !inventory.Equipped.Placeable)) hud.Say(inventory.Tools.Exists(nook.IsPickaxe) ? "Equip the pickaxe to mine: press its number" : "You need a pickaxe to mine");
            return Result.NoPickaxe;
        }
        if (side)
        {
            if (Vector2.Distance(player.transform.position, new Vector2(x + 0.5f, level + 0.5f)) > Reach + 0.5f) return Result.TooFar;
        }
        else
        {
            bool near = Vector2.Distance(player.GroundPosition, new Vector2(square.x + 0.5f, square.y + 0.5f)) <= Reach + 0.5f;
            if (!near || Mathf.Abs(level + 0.5f - (player.FeetHeight + PlayerController.Height / 2f)) > Reach + 0.5f) return Result.TooFar;
        }
        if (block ==PlanetOne.SideBlock.Ship || level <= -PlanetOne.GroundDepth)
        {
            hud.Say("That can't be mined");
            return Result.CannotBreak;
        }

        var target = new Vector3Int(square.x, square.y, level);
        if (hits == 0 || target != damaged) hits = 0;
        damaged = target;
        hits++;
        // The crystal pickaxe breaks things in fewer hits.
        int needed = nook.PickaxeHits(inventory.Equipped, HitsFor(block));
        if (hits < needed)
        {
            cracks.sprite = crackSprites[CrackStage(hits, needed)];
            return Result.Hit;
        }

        hits = 0;
        planet.Mine(square.x, square.y, level);
        if (side) view.RedrawBlock(x, level);
        pickups.Drop(ItemFor(block), new Vector2(square.x + 0.5f, square.y + 0.5f), level + 0.3f, DropPullRadius, DropScale);
        return Result.Broke;
    }
}
