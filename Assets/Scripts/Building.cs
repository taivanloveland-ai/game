using UnityEngine;

// Building with blocks. With a block equipped on the hotbar, left clicking puts one down. In the side-scroller it goes
// in the empty space that was clicked; top-down, it is stacked on top of the square that was clicked.
// A block has to touch another block or the ground, and be within reach.
public class Building : MonoBehaviour
{
    // A block can be placed if it is within this many blocks of the player: the same reach as mining.
    const float Reach = 3f;

    public enum Result { NoBlock, Busy, Taken, TooFar, TooHigh, InTheWay, Floating, Placed }

    PlanetOne planet;
    PlayerController player;
    MiningMode view;
    Inventory inventory;
    Hud hud;
    Gatherables gatherables;
    Camera cam;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode view, Inventory inventory, Hud hud, Gatherables gatherables, Camera cam)
    {
        this.planet = planet;
        this.player = player;
        this.view = view;
        this.inventory = inventory;
        this.hud = hud;
        this.gatherables = gatherables;
        this.cam = cam;
    }

    void Update()
    {
        if (GameInput.LeftClickPressed()) ClickAt(cam.ScreenToWorldPoint(GameInput.PointerPosition()));
    }

    // Handles a left click at a point on screen (given in world units): places one of the equipped block there.
    public Result ClickAt(Vector2 point)
    {
        Item item = inventory.Equipped;
        if (item == null || !item.Placeable || inventory.CountOf(item) == 0) return Result.NoBlock;
        // A click on a fern or ice shard collects it instead.
        if (view.Stepping || gatherables.ThingUnder(point)) return Result.Busy;

        Vector2Int square;
        int level, along = 0;
        if (view.Active)
        {
            along = Mathf.FloorToInt(point.x);
            level = Mathf.FloorToInt(point.y);
            square = view.Cell(along);
            if (planet.SideBlockAt(square.x, square.y, level) != PlanetOne.SideBlock.Air) return Result.Taken;
            if (Vector2.Distance(player.transform.position, new Vector2(along + 0.5f, level + 0.5f)) > Reach + 0.5f) return Result.TooFar;
        }
        else
        {
            square = planet.SquareAt(point);
            level = planet.TopAt(square.x, square.y);
            bool near = Vector2.Distance(player.GroundPosition, new Vector2(square.x + 0.5f, square.y + 0.5f)) <= Reach + 0.5f;
            if (!near || Mathf.Abs(level + 0.5f - (player.FeetHeight + PlayerController.Height / 2f)) > Reach + 0.5f) return Result.TooFar;
        }

        if (level >= PlanetOne.BuildLimit)
        {
            hud.Say("That is too high to build on");
            return Result.TooHigh;
        }
        if (player.Occupies(square, level)) return Result.InTheWay;
        if (gatherables.HasThing(square) && level == planet.GroundAt(square.x, square.y))
        {
            hud.Say("Something is growing there");
            return Result.InTheWay;
        }
        if (!Touching(square, level)) return Result.Floating;
        if (!planet.Place(square.x, square.y, level, item.Block)) return Result.Taken;

        inventory.Remove(item, 1);
        if (view.Active) view.RedrawBlock(along, level);
        return Result.Placed;
    }

    // True if any of the six spaces next to a space holds a block.
    bool Touching(Vector2Int square, int level)
    {
        return Solid(square.x, square.y, level - 1) || Solid(square.x, square.y, level + 1) ||
            Solid(square.x - 1, square.y, level) || Solid(square.x + 1, square.y, level) ||
            Solid(square.x, square.y - 1, level) || Solid(square.x, square.y + 1, level);
    }

    bool Solid(int x, int y, int level)
    {
        return planet.SideBlockAt(x, y, level) != PlanetOne.SideBlock.Air;
    }
}
