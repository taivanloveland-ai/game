using UnityEngine;

// The maps. A mini map in the top right corner shows the land around the player, with markers for the player and
// the ship. Everything the mini map has ever shown is remembered, and pressing M opens a world map of all of it.
// Both maps are top-down, whichever view the game is in. Ferns and ice shards are not shown.
//
// Both maps zoom with the mouse wheel when the mouse is over them, and can be dragged around with the left button.
// Under the mini map, "Back to me" puts it back on the player once it has been moved; on the world map, "Whole map"
// shows all of it again.
//
// The map of the planet is twice as wide as it is tall, like a flat map of the Earth. Going east or west, the maps
// carry straight on past the edge of the picture and show the land from its opposite side. The top and bottom edges
// are the North and South Poles: the maps stop there, and someone walking over a pole comes back down on the far
// side of the map, half way along.
public class WorldMap : MonoBehaviour
{
    // How many squares across the mini map shows to begin with, and how big it is on screen.
    const int MiniMapSquares = 40;
    const float MiniMapSize = 190f;
    const float Margin = 14f;
    // The mini map can be zoomed to show between this few and this many squares across.
    const float FewestSquares = 10f, MostSquares = 400f;
    // The world map can be zoomed in this many times at most.
    const float MostWorldZoom = 16f;
    // How much one notch of the mouse wheel zooms a map.
    const float ZoomStep = 1.25f;

    static readonly Color32 Unexplored = new Color32(12, 16, 26, 255);
    static readonly Color PlayerMark = new Color32(240, 138, 46, 255);
    static readonly Color ShipMark = new Color32(255, 226, 96, 255);
    static readonly Color MarkEdge = new Color32(20, 24, 34, 255);

    // True while the world map is showing.
    public bool Open { get; private set; }
    // How many squares of the world have been on the mini map so far.
    public int ExploredCount { get; private set; }
    // The world map picture: one pixel per square, dark where the player has not been.
    public Texture2D Picture => picture;

    PlanetOne planet;
    PlayerController player;
    Vector2 ship;
    Texture2D picture;
    bool[,] explored;
    Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
    GUIStyle titleStyle;

    // How many squares across the mini map is showing, and how far the spot in its middle is from the player, in squares.
    float miniSquares = MiniMapSquares;
    Vector2 miniOffset;
    // How far the world map is zoomed in, and the spot of the planet in its middle.
    float worldZoom = 1f;
    Vector2 worldMiddle;
    // Which map is being dragged, if either, and where the mouse was on the last frame.
    bool draggingMini, draggingWorld;
    Vector2 lastPointer;

    public void Setup(PlanetOne planet, PlayerController player, Vector2 ship)
    {
        this.planet = planet;
        this.player = player;
        this.ship = ship;
        GameInput.PointerOverMap = PointerOverMap;
        worldMiddle = new Vector2(planet.width / 2f, planet.height / 2f);

        explored = new bool[planet.width, planet.height];
        // The picture repeats past its east and west edges, as the planet does, so a map can show the land across the join.
        picture = new Texture2D(planet.width, planet.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
        var pixels = new Color32[planet.width * planet.height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Unexplored;
        picture.SetPixels32(pixels);
        picture.Apply();
        Explore();
    }

    void Update()
    {
        if (GameInput.MapPressed()) Open = !Open;
        if (Open && GameInput.CancelPressed()) Open = false;
        Explore();
        HandleMouse();
    }

    // The mouse on screen, measured from the top left, as the maps are laid out.
    static Vector2 Pointer()
    {
        Vector2 pointer = GameInput.PointerPosition();
        return new Vector2(pointer.x, Screen.height - pointer.y);
    }

    // Zooming with the wheel and dragging with the left button, on whichever map is under the mouse.
    void HandleMouse()
    {
        Vector2 pointer = Pointer();
        float wheel = GameInput.MouseWheel();
        bool held = GameInput.MouseButtonHeld();
        if (!held) draggingMini = draggingWorld = false;

        if (Open)
        {
            Rect frame = WorldFrame();
            if (wheel != 0f && frame.Contains(pointer)) worldZoom = Mathf.Clamp(worldZoom * (wheel > 0f ? ZoomStep : 1f / ZoomStep), 1f, MostWorldZoom);
            if (GameInput.MouseButtonPressed() && frame.Contains(pointer)) draggingWorld = true;
            else if (draggingWorld)
            {
                // The land follows the mouse, so the middle moves the other way. Up the screen is north.
                float scale = frame.width * worldZoom / planet.width;
                Vector2 moved = pointer - lastPointer;
                worldMiddle += new Vector2(-moved.x, moved.y) / scale;
                worldMiddle.x = Mathf.Repeat(worldMiddle.x, planet.width);
            }
        }
        else
        {
            Rect frame = MiniMapFrame();
            if (wheel != 0f && frame.Contains(pointer)) miniSquares = Mathf.Clamp(miniSquares * (wheel > 0f ? 1f / ZoomStep : ZoomStep), FewestSquares, MostSquares);
            if (GameInput.MouseButtonPressed() && frame.Contains(pointer)) draggingMini = true;
            else if (draggingMini)
            {
                float scale = MiniMapSize / miniSquares;
                Vector2 moved = pointer - lastPointer;
                miniOffset += new Vector2(-moved.x, moved.y) / scale;
            }
        }
        lastPointer = pointer;
    }

    // True while the mouse is over a map or dragging one, or the world map is open, so clicks and the wheel go to
    // the map and not to the world or the hotbar.
    bool PointerOverMap()
    {
        if (Open || draggingMini || draggingWorld) return true;
        Rect frame = MiniMapFrame();
        var area = new Rect(frame.x - 4f, frame.y - 4f, frame.width + 8f, frame.height + (miniOffset != Vector2.zero ? 38f : 8f));
        return area.Contains(Pointer());
    }

    // Where the player is within the planet, however many times they have gone round it.
    Vector2 PlayerOnPlanet()
    {
        return planet.WrapPoint(player.GroundPosition);
    }

    // Remembers every square the mini map is showing right now.
    void Explore()
    {
        Vector2Int cell = Vector2Int.FloorToInt(player.GroundPosition);
        if (cell == lastCell) return;
        lastCell = cell;

        bool changed = false;
        int half = MiniMapSquares / 2;
        for (int y = cell.y - half; y < cell.y + half; y++)
        {
            for (int x = cell.x - half; x < cell.x + half; x++)
            {
                Vector2Int spot = planet.Wrap(x, y);
                int px = spot.x, py = spot.y;
                if (explored[px, py]) continue;
                explored[px, py] = true;
                ExploredCount++;
                picture.SetPixel(px, py, planet.MapColorAt(px, py));
                changed = true;
            }
        }
        if (changed) picture.Apply();
    }

    void OnGUI()
    {
        DrawMiniMap();
        if (Open) DrawWorldMap();
    }

    static Rect MiniMapFrame()
    {
        return new Rect(Screen.width - MiniMapSize - Margin, Margin, MiniMapSize, MiniMapSize);
    }

    static Rect WorldFrame()
    {
        // Twice as wide as it is tall, like the planet.
        float width = Mathf.Min(Screen.width * 0.9f, Screen.height * 0.8f * 2f);
        return new Rect((Screen.width - width) / 2f, (Screen.height - width / 2f) / 2f + 12f, width, width / 2f);
    }

    // Draws the part of the world picture around a spot of the planet into a frame on screen, a number of squares
    // across. The picture repeats, so the land across the join shows too.
    void DrawLand(Rect frame, Vector2 middle, float squares)
    {
        float across = squares / planet.width, down = squares * frame.height / frame.width / planet.height;
        var part = new Rect(middle.x / planet.width - across / 2f, middle.y / planet.height - down / 2f, across, down);
        GUI.DrawTextureWithTexCoords(frame, picture, part);
    }

    // The map stops at the poles, so the spot in a map's middle is kept far enough from the top and bottom edges
    // that the map never shows past them.
    Vector2 KeepOffPoles(Rect frame, Vector2 middle, float squares)
    {
        float halfDown = squares * frame.height / frame.width / 2f;
        float y = halfDown * 2f >= planet.height ? planet.height / 2f : Mathf.Clamp(middle.y, halfDown, planet.height - halfDown);
        return new Vector2(middle.x, y);
    }

    // Where a spot of the planet is drawn on a map, given the spot in the map's middle and its scale in pixels a
    // square. The nearest copy of the spot to the east or west is used, since the map repeats that way.
    Vector2 OnMap(Rect frame, Vector2 middle, float scale, Vector2 spot)
    {
        var away = new Vector2(Mathf.Repeat(spot.x - middle.x + planet.width / 2f, planet.width) - planet.width / 2f, spot.y - middle.y);
        return new Vector2(frame.center.x + away.x * scale, frame.center.y - away.y * scale);
    }

    static Vector2 KeepInside(Rect frame, Vector2 spot)
    {
        return new Vector2(Mathf.Clamp(spot.x, frame.x + 5f, frame.xMax - 5f), Mathf.Clamp(spot.y, frame.y + 5f, frame.yMax - 5f));
    }

    void DrawMiniMap()
    {
        Rect frame = MiniMapFrame();
        GUI.Box(new Rect(frame.x - 4f, frame.y - 4f, frame.width + 8f, frame.height + 8f), GUIContent.none);
        Vector2 at = PlayerOnPlanet();
        Vector2 middle = KeepOffPoles(frame, at + miniOffset, miniSquares);
        DrawLand(frame, middle, miniSquares);

        // The ship's marker shows the shortest way home, and stays on the edge of the mini map when the ship is out of
        // range. So does the player's, when the map has been dragged away from them.
        float scale = MiniMapSize / miniSquares;
        DrawMark(KeepInside(frame, OnMap(frame, middle, scale, ship)), 8f, ShipMark);
        DrawMark(KeepInside(frame, OnMap(frame, middle, scale, at)), 7f, PlayerMark);

        if (miniOffset != Vector2.zero && GUI.Button(new Rect(frame.x, frame.yMax + 8f, frame.width, 22f), "Back to me")) miniOffset = Vector2.zero;
    }

    void DrawWorldMap()
    {
        if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };

        // Dim the game behind the map.
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect frame = WorldFrame();
        GUI.Box(new Rect(frame.x - 6f, frame.y - 34f, frame.width + 12f, frame.height + 40f), GUIContent.none);
        GUI.Label(new Rect(frame.x, frame.y - 30f, frame.width, 26f), "World map    [M] close    scroll to zoom, drag to move", titleStyle);
        float squares = planet.width / worldZoom;
        worldMiddle = KeepOffPoles(frame, worldMiddle, squares);
        DrawLand(frame, worldMiddle, squares);

        float scale = frame.width / squares;
        Vector2 shipSpot = OnMap(frame, worldMiddle, scale, ship), playerSpot = OnMap(frame, worldMiddle, scale, PlayerOnPlanet());
        if (frame.Contains(shipSpot)) DrawMark(shipSpot, 10f, ShipMark);
        DrawMark(KeepInside(frame, playerSpot), 9f, PlayerMark);

        bool moved = worldZoom != 1f || worldMiddle != new Vector2(planet.width / 2f, planet.height / 2f);
        if (moved && GUI.Button(new Rect(frame.x, frame.yMax + 10f, frame.width, 24f), "Whole map"))
        {
            worldZoom = 1f;
            worldMiddle = new Vector2(planet.width / 2f, planet.height / 2f);
        }
    }

    // A small square marker with a dark edge so it shows up on snow.
    static void DrawMark(Vector2 centre, float size, Color color)
    {
        GUI.color = MarkEdge;
        GUI.DrawTexture(new Rect(centre.x - size / 2f - 1.5f, centre.y - size / 2f - 1.5f, size + 3f, size + 3f), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(centre.x - size / 2f, centre.y - size / 2f, size, size), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
