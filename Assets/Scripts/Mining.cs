using UnityEngine;

// Mining with the pickaxe, in the side-scroller. Left clicking a block within reach hits it: snow breaks in 1 hit,
// ice in 2 and stone in 5. A broken block drops as an item that slides to the player. Grey rocks count as stone.
public class Mining : MonoBehaviour
{
    // A block can be mined if it is within this many blocks of the player.
    const float Reach = 3f;
    const int SnowHits = 1, IceHits = 2, StoneHits = 5;
    // A mined block slides to the player from anywhere within reach, and is drawn small as a loose item.
    const float DropPullRadius = Reach + 1.5f;
    const float DropScale = 0.35f;
    // Pictures here are painted at the same fine scale as the snow picture.
    const int FinePixels = 128;
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
    public Item IceItem { get; private set; }
    public Item StoneItem { get; private set; }

    PlanetOne planet;
    PlayerController player;
    MiningMode view;
    Inventory inventory;
    Hud hud;
    Pickups pickups;
    CraftingNook nook;
    Camera cam;

    // The block being worked on (its square of the world, then its level) and how many times it has been hit.
    // Hitting a different block starts again.
    Vector3Int damaged;
    int hits;
    SpriteRenderer cracks;
    Sprite[] crackSprites;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode view, Inventory inventory, Hud hud, Pickups pickups, CraftingNook nook, Camera cam)
    {
        this.planet = planet;
        this.player = player;
        this.view = view;
        this.inventory = inventory;
        this.hud = hud;
        this.pickups = pickups;
        this.nook = nook;
        this.cam = cam;

        SnowItem = new Item("Snow", planet.SnowSprite != null ? planet.SnowSprite : planet.Square);
        IceItem = new Item("Ice", planet.IceSprite != null ? planet.IceSprite : planet.IceSideSprites[0]);
        StoneItem = new Item("Stone", PlanetOne.PaintSprite(FinePixels, FinePixels, new Vector2(0.5f, 0.5f), FinePixels, true, StonePixel));

        crackSprites = new Sprite[CrackStages];
        for (int stage = 0; stage < CrackStages; stage++)
        {
            int lines = (stage + 1) * 2;
            crackSprites[stage] = PlanetOne.PaintSprite(FinePixels, FinePixels, new Vector2(0.5f, 0.5f), FinePixels, true, (x, y) => CrackPixel(x, y, lines));
        }
        var go = new GameObject("Cracks");
        go.transform.SetParent(view.Root, false);
        cracks = go.AddComponent<SpriteRenderer>();
        cracks.sortingOrder = MiningMode.ObjectSortingOrder;
        cracks.enabled = false;
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
        // Cracks only show on the block being worked on, when the row or column it belongs to is on screen.
        var square = new Vector2Int(damaged.x, damaged.y);
        cracks.enabled = hits > 0 && view.Active && view.Shows(square);
        if (cracks.enabled) cracks.transform.position = new Vector2(view.Along(square) + 0.5f, damaged.z + 0.5f);
        if (GameInput.LeftClickPressed()) ClickAt(cam.ScreenToWorldPoint(GameInput.PointerPosition()));
    }

    static int HitsFor(PlanetOne.SideBlock block)
    {
        return block == PlanetOne.SideBlock.Snow ? SnowHits : block == PlanetOne.SideBlock.Ice ? IceHits : StoneHits;
    }

    Item ItemFor(PlanetOne.SideBlock block)
    {
        return block == PlanetOne.SideBlock.Snow ? SnowItem : block == PlanetOne.SideBlock.Ice ? IceItem : StoneItem;
    }

    // Handles a left click at a point on screen (given in world units): one hit with the pickaxe on the block there.
    public Result ClickAt(Vector2 point)
    {
        if (!view.Active || view.Stepping) return Result.NotMining;
        int x = Mathf.FloorToInt(point.x), level = Mathf.FloorToInt(point.y);
        // The square of the world the clicked block is in.
        Vector2Int square = view.Cell(x);
        PlanetOne.SideBlock block = planet.SideBlockAt(square.x, square.y, level);
        if (block == PlanetOne.SideBlock.Air) return Result.Nothing;
        if (inventory.Equipped != nook.Pickaxe)
        {
            hud.Say(inventory.Tools.Contains(nook.Pickaxe) ? "Equip the pickaxe to mine: press its number" : "You need a pickaxe to mine");
            return Result.NoPickaxe;
        }
        var centre = new Vector2(x + 0.5f, level + 0.5f);
        if (Vector2.Distance(player.transform.position, centre) > Reach + 0.5f) return Result.TooFar;
        if (block == PlanetOne.SideBlock.Ship || level <= -PlanetOne.GroundDepth)
        {
            hud.Say("That can't be mined");
            return Result.CannotBreak;
        }

        var target = new Vector3Int(square.x, square.y, level);
        if (hits == 0 || target != damaged) hits = 0;
        damaged = target;
        hits++;
        int needed = HitsFor(block);
        if (hits < needed)
        {
            cracks.transform.position = centre;
            cracks.sprite = crackSprites[Mathf.Clamp(Mathf.CeilToInt(hits * CrackStages / (float)needed) - 1, 0, CrackStages - 1)];
            return Result.Hit;
        }

        hits = 0;
        planet.Mine(square.x, square.y, level);
        view.RemoveBlock(x, level);
        pickups.Drop(ItemFor(block), new Vector2(square.x + 0.5f, square.y + 0.5f), level + 0.3f, DropPullRadius, DropScale);
        return Result.Broke;
    }
}
