using System.Collections.Generic;
using UnityEngine;

// The maps. A mini map in the top right corner shows the land around the player, with markers for the player and
// the ship. Everything the mini map has ever shown is remembered, and pressing M opens a world map of all of it.
// Both maps are top-down, whichever view the game is in. Ferns and ice shards are not shown.
//
// Both maps zoom with the mouse wheel when the mouse is over them, and can be dragged around with the left button.
// Under the mini map, "Back to me" puts it back on the player once it has been moved; on the world map, "Whole map"
// shows all of it again.
//
// Notes (Taivan's request of 2026-10-19): double-click a spot on the world map and a dot appears there with a box
// under it to type in; Enter, Escape or a click somewhere else finishes. Double-click a dot or its words to change
// them, and right-click a dot to take it away. The notes show on the mini map too, and each planet keeps its own.
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
    // (14 until 2026-10-20: in full screen the very edge of the screen can be cut off, hiding the ship's dot when it
    // was pushed to the mini map's right side.)
    const float Margin = 30f;
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
    // Newly explored squares are painted here first and then copied into the picture on the graphics card, one
    // square at a time. Sending the whole picture over again each time was far too slow for a picture this big.
    const int BrushSize = 64;
    Texture2D brush;
    readonly Color32[] brushPixels = new Color32[BrushSize * BrushSize];
    readonly Vector2Int[] brushTargets = new Vector2Int[BrushSize * BrushSize];
    bool[,] explored;
    int mapWidth, mapHeight;
    // While the player is in a cave, the mouth they went in by. The mini map goes dark then, and on the world map
    // the player's marker waits at the mouth.
    public Vector2? CaveMouth;
    // The cave mouths the player has found, marked on both maps as a frost cave.
    readonly List<Vector2> caves = new List<Vector2>();
    static readonly Color CaveMark = new Color32(150, 214, 245, 255);
    GUIStyle caveStyle;
    Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
    GUIStyle titleStyle;

    class Note
    {
        public Vector2 At;
        public string Words = "";
    }
    readonly List<Note> notes = new List<Note>();
    // The note being typed, if any.
    Note typing;
    bool focusTyping;
    static readonly Color NoteMark = new Color32(255, 110, 170, 255);
    const int LongestNote = 40;
    GUIStyle noteStyle, noteBoxStyle;
    // For checking: how many notes there are.
    public int NoteCount => notes.Count;

    // How many squares across the mini map is showing, and how far the spot in its middle is from the player, in squares.
    float miniSquares = MiniMapSquares;
    Vector2 miniOffset;
    // How far the world map is zoomed in, and the spot of the planet in its middle.
    float worldZoom = 1f;
    Vector2 worldMiddle;
    // Which map is being dragged, if either, and where the mouse was on the last frame.
    bool draggingMini, draggingWorld;
    Vector2 lastPointer;

    // WholeMap, if given, is the colour of every square of the planet: the map then starts fully drawn (creative mode).
    public void Setup(PlanetOne planet, PlayerController player, Vector2 ship, Color32[] wholeMap = null)
    {
        this.planet = planet;
        this.player = player;
        this.ship = ship;
        // The size of the planet's surface. (While the player is in a cave, the planet's own size is the cave's.)
        mapWidth = planet.width;
        mapHeight = planet.height;
        GameInput.PointerOverMap = PointerOverMap;
        worldMiddle = new Vector2(mapWidth / 2f, mapHeight / 2f);

        explored = new bool[mapWidth, mapHeight];
        // The picture repeats past its east and west edges, as the planet does, so a map can show the land across the join.
        picture = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
        Color32[] pixels = wholeMap;
        if (pixels != null)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++) explored[x, y] = true;
            }
            ExploredCount = mapWidth * mapHeight;
        }
        else
        {
            pixels = new Color32[mapWidth * mapHeight];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Unexplored;
        }
        picture.SetPixels32(pixels);
        picture.Apply();
        Explore();
    }

    void Update()
    {
        // Typing stops when the map is put away.
        if (!Open && typing != null) StopTyping();
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
                float scale = frame.width * worldZoom / mapWidth;
                Vector2 moved = pointer - lastPointer;
                worldMiddle += new Vector2(-moved.x, moved.y) / scale;
                worldMiddle.x = Mathf.Repeat(worldMiddle.x, mapWidth);
            }
        }
        else if (GameSettings.ShowMiniMap)
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
        if (!GameSettings.ShowMiniMap) return false;
        Rect frame = MiniMapFrame();
        var area = new Rect(frame.x - 4f, frame.y - 4f, frame.width + 8f, frame.height + (miniOffset != Vector2.zero ? 38f : 8f));
        return area.Contains(Pointer());
    }

    // Writes what a saved game keeps of the maps: the caves found, and which squares have been seen. (In creative
    // mode every square is seen from the start, so those are not written.)
    public void Write(System.IO.BinaryWriter w)
    {
        w.Write(caves.Count);
        foreach (Vector2 mouth in caves)
        {
            w.Write(mouth.x);
            w.Write(mouth.y);
        }
        bool all = ExploredCount >= mapWidth * mapHeight;
        w.Write(!all);
        if (all) return;
        // One bit for every square.
        var bits = new byte[(mapWidth * mapHeight + 7) / 8];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                if (!explored[x, y]) continue;
                int i = y * mapWidth + x;
                bits[i >> 3] |= (byte)(1 << (i & 7));
            }
        }
        w.Write(bits.Length);
        w.Write(bits);
    }

    // Reads them back from a saved game, and draws the squares that had been seen.
    public void Read(System.IO.BinaryReader r)
    {
        int found = r.ReadInt32();
        for (int i = 0; i < found; i++) MarkCave(new Vector2(r.ReadSingle(), r.ReadSingle()));
        if (!r.ReadBoolean()) return;
        byte[] bits = r.ReadBytes(r.ReadInt32());
        Color32[] pixels = picture.GetPixels32();
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int i = y * mapWidth + x;
                if (explored[x, y] || (i >> 3) >= bits.Length || (bits[i >> 3] & (1 << (i & 7))) == 0) continue;
                explored[x, y] = true;
                ExploredCount++;
                pixels[i] = planet.MapColorAt(x, y);
            }
        }
        picture.SetPixels32(pixels);
        picture.Apply();
    }

    // Writes the notes on the map, and reads them back.
    public void WriteNotes(System.IO.BinaryWriter w)
    {
        w.Write(notes.Count);
        foreach (Note note in notes)
        {
            w.Write(note.At.x);
            w.Write(note.At.y);
            w.Write(note.Words);
        }
    }

    public void ReadNotes(System.IO.BinaryReader r)
    {
        notes.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) notes.Add(new Note { At = new Vector2(r.ReadSingle(), r.ReadSingle()), Words = r.ReadString() });
    }

    // For checking: puts a note on the map, as a double click there and typing would.
    public void AddNote(Vector2 at, string words) => notes.Add(new Note { At = at, Words = words });

    void StopTyping()
    {
        // A note left with no words is taken away.
        if (typing != null && typing.Words.Trim().Length == 0) notes.Remove(typing);
        typing = null;
        GameInput.Typing = false;
        GUIUtility.keyboardControl = 0;
    }

    // Marks a cave mouth on the maps.
    public void MarkCave(Vector2 mouth)
    {
        if (!caves.Contains(mouth)) caves.Add(mouth);
    }

    // Where the player is within the planet, however many times they have gone round it.
    Vector2 PlayerOnPlanet()
    {
        if (CaveMouth.HasValue) return CaveMouth.Value;
        return planet.WrapPoint(player.GroundPosition);
    }

    // Remembers every square the mini map is showing right now.
    void Explore()
    {
        if (CaveMouth.HasValue) return;
        Vector2Int cell = Vector2Int.FloorToInt(player.GroundPosition);
        if (cell == lastCell) return;
        lastCell = cell;

        int count = 0;
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
                Color32 color = planet.MapColorAt(px, py);
                // Kept in the picture's own copy too, though only the graphics card's copy is drawn.
                picture.SetPixel(px, py, color);
                brushPixels[count] = color;
                brushTargets[count] = spot;
                count++;
            }
        }
        if (count == 0) return;
        if (SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None)
        {
            picture.Apply();
            return;
        }
        if (brush == null) brush = new Texture2D(BrushSize, BrushSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        brush.SetPixels32(brushPixels);
        brush.Apply();
        for (int i = 0; i < count; i++) Graphics.CopyTexture(brush, 0, 0, i % BrushSize, i / BrushSize, 1, 1, picture, 0, 0, brushTargets[i].x, brushTargets[i].y);
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
        float across = squares / mapWidth, down = squares * frame.height / frame.width / mapHeight;
        var part = new Rect(middle.x / mapWidth - across / 2f, middle.y / mapHeight - down / 2f, across, down);
        GUI.DrawTextureWithTexCoords(frame, picture, part);
    }

    // The map stops at the poles, so the spot in a map's middle is kept far enough from the top and bottom edges
    // that the map never shows past them.
    Vector2 KeepOffPoles(Rect frame, Vector2 middle, float squares)
    {
        float halfDown = squares * frame.height / frame.width / 2f;
        float y = halfDown * 2f >= mapHeight ? mapHeight / 2f : Mathf.Clamp(middle.y, halfDown, mapHeight - halfDown);
        return new Vector2(middle.x, y);
    }

    // Where a spot of the planet is drawn on a map, given the spot in the map's middle and its scale in pixels a
    // square. The nearest copy of the spot to the east or west is used, since the map repeats that way.
    Vector2 OnMap(Rect frame, Vector2 middle, float scale, Vector2 spot)
    {
        var away = new Vector2(Mathf.Repeat(spot.x - middle.x + mapWidth / 2f, mapWidth) - mapWidth / 2f, spot.y - middle.y);
        return new Vector2(frame.center.x + away.x * scale, frame.center.y - away.y * scale);
    }

    static Vector2 KeepInside(Rect frame, Vector2 spot)
    {
        return new Vector2(Mathf.Clamp(spot.x, frame.x + 9f, frame.xMax - 9f), Mathf.Clamp(spot.y, frame.y + 9f, frame.yMax - 9f));
    }

    void DrawMiniMap()
    {
        // The mini map can be turned off in the settings.
        if (!GameSettings.ShowMiniMap) return;
        Rect frame = MiniMapFrame();
        GUI.Box(new Rect(frame.x - 4f, frame.y - 4f, frame.width + 8f, frame.height + 8f), GUIContent.none);
        // In a cave the mini map goes dark: the way has to be remembered.
        if (CaveMouth.HasValue)
        {
            GUI.color = Unexplored;
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = Color.white;
            return;
        }
        Vector2 at = PlayerOnPlanet();
        Vector2 middle = KeepOffPoles(frame, at + miniOffset, miniSquares);
        DrawLand(frame, middle, miniSquares);

        // The ship's marker shows the shortest way home, and stays on the edge of the mini map when the ship is out of
        // range. So does the player's, when the map has been dragged away from them.
        float scale = MiniMapSize / miniSquares;
        DrawCaves(frame, middle, scale);
        DrawNotes(frame, middle, scale, false);
        DrawMark(KeepInside(frame, OnMap(frame, middle, scale, ship)), 8f, ShipMark);
        DrawMark(KeepInside(frame, OnMap(frame, middle, scale, at)), 7f, PlayerMark);

        if (miniOffset != Vector2.zero && GameText.Button(new Rect(frame.x, frame.yMax + 8f, frame.width, 22f), "Back to me")) miniOffset = Vector2.zero;
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
        GameText.Label(new Rect(frame.x, frame.y - 30f, frame.width, 26f), "World map    [M] close    scroll to zoom, drag to move    double-click to write a note", titleStyle);
        float squares = mapWidth / worldZoom;
        worldMiddle = KeepOffPoles(frame, worldMiddle, squares);
        DrawLand(frame, worldMiddle, squares);

        float scale = frame.width / squares;
        Vector2 shipSpot = OnMap(frame, worldMiddle, scale, ship), playerSpot = OnMap(frame, worldMiddle, scale, PlayerOnPlanet());
        DrawCaves(frame, worldMiddle, scale);
        DrawNotes(frame, worldMiddle, scale, true);
        if (frame.Contains(shipSpot)) DrawMark(shipSpot, 10f, ShipMark);
        DrawMark(KeepInside(frame, playerSpot), 9f, PlayerMark);

        bool moved = worldZoom != 1f || worldMiddle != new Vector2(mapWidth / 2f, mapHeight / 2f);
        if (moved && GameText.Button(new Rect(frame.x, frame.yMax + 10f, frame.width, 24f), "Whole map"))
        {
            worldZoom = 1f;
            worldMiddle = new Vector2(mapWidth / 2f, mapHeight / 2f);
        }
    }

    // The notes on a map: a pink dot with the words under it. On the world map they can be made, changed and taken
    // away with the mouse, and the one being typed has a box to type in.
    void DrawNotes(Rect frame, Vector2 middle, float scale, bool world)
    {
        if (noteStyle == null)
        {
            noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            noteBoxStyle = new GUIStyle(GUI.skin.textField) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        }
        Rect Words(Vector2 spot) => new Rect(spot.x - 90f, spot.y + 6f, 180f, 22f);
        Event e = Event.current;
        if (world && e.type == EventType.MouseDown && frame.Contains(e.mousePosition))
        {
            // Which note was clicked: its dot or its words.
            Note hit = null;
            foreach (Note note in notes)
            {
                Vector2 spot = OnMap(frame, middle, scale, note.At);
                if (Vector2.Distance(spot, e.mousePosition) <= 9f || (note != typing && note.Words.Length > 0 && Words(spot).Contains(e.mousePosition))) hit = note;
            }
            if (typing != null && hit != typing && !(typing != null && Words(OnMap(frame, middle, scale, typing.At)).Contains(e.mousePosition))) StopTyping();
            if (e.button == 1 && hit != null)
            {
                notes.Remove(hit);
                if (hit == typing) StopTyping();
                e.Use();
            }
            else if (e.button == 0 && e.clickCount == 2)
            {
                if (hit == null)
                {
                    // A spot of the planet: east and west go round, north and south stop at the poles.
                    Vector2 away = new Vector2(e.mousePosition.x - frame.center.x, frame.center.y - e.mousePosition.y) / scale;
                    Vector2 at = new Vector2(Mathf.Repeat(middle.x + away.x, mapWidth), Mathf.Clamp(middle.y + away.y, 0f, mapHeight - 0.01f));
                    hit = new Note { At = at };
                    notes.Add(hit);
                }
                typing = hit;
                focusTyping = true;
                GameInput.Typing = true;
                e.Use();
            }
        }
        // Enter or Escape finishes typing; an empty note is taken away.
        if (typing != null && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape))
        {
            if (typing.Words.Trim().Length == 0) notes.Remove(typing);
            StopTyping();
            e.Use();
        }
        foreach (Note note in notes.ToArray())
        {
            Vector2 spot = OnMap(frame, middle, scale, note.At);
            if (!frame.Contains(spot)) continue;
            DrawMark(spot, world ? 9f : 7f, NoteMark);
            if (world && note == typing)
            {
                GUI.SetNextControlName("Map Note");
                note.Words = GUI.TextField(Words(spot), note.Words, LongestNote, noteBoxStyle);
                if (focusTyping)
                {
                    GUI.FocusControl("Map Note");
                    focusTyping = false;
                }
                continue;
            }
            if (note.Words.Length == 0) continue;
            Rect label = Words(spot);
            if (!world) label = new Rect(spot.x - 60f, spot.y + 4f, 120f, 18f);
            GUI.color = MarkEdge;
            GameText.Label(new Rect(label.x + 1f, label.y + 1f, label.width, label.height), note.Words, noteStyle);
            GUI.color = Color.white;
            GameText.Label(label, note.Words, noteStyle);
        }
    }

    // The frost caves the player has found that are on a map: a pale blue marker with the name beside it.
    void DrawCaves(Rect frame, Vector2 middle, float scale)
    {
        if (caveStyle == null) caveStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        foreach (Vector2 mouth in caves)
        {
            Vector2 spot = OnMap(frame, middle, scale, mouth);
            if (!frame.Contains(spot)) continue;
            DrawMark(spot, 7f, CaveMark);
            var label = new Rect(spot.x + 7f, spot.y - 11f, 150f, 22f);
            GUI.color = MarkEdge;
            GameText.Label(new Rect(label.x + 1f, label.y + 1f, label.width, label.height), "Frost Cave", caveStyle);
            GUI.color = Color.white;
            GameText.Label(label, "Frost Cave", caveStyle);
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
