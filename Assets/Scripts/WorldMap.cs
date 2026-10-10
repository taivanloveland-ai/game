using UnityEngine;

// The maps. A mini map in the top right corner shows the land around the player, with markers for the player and
// the ship. Everything the mini map has ever shown is remembered, and pressing M opens a world map of all of it.
// Both maps are top-down, whichever view the game is in. Ferns and ice shards are not shown.
//
// The planet has no edges, so the mini map carries straight on past the edge of the world map picture and shows
// the land from its opposite side. On the world map, going off the top brings your marker back in at the bottom.
public class WorldMap : MonoBehaviour
{
    // How many squares across the mini map shows, and how big it is on screen.
    const int MiniMapSquares = 40;
    const float MiniMapSize = 190f;
    const float Margin = 14f;

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

    public void Setup(PlanetOne planet, PlayerController player, Vector2 ship)
    {
        this.planet = planet;
        this.player = player;
        this.ship = ship;

        explored = new bool[planet.width, planet.height];
        picture = new Texture2D(planet.width, planet.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
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
    }

    // Where the player is within the planet, however many times they have gone round it.
    Vector2 PlayerOnPlanet()
    {
        Vector2 at = player.GroundPosition;
        return new Vector2(Mathf.Repeat(at.x, planet.width), Mathf.Repeat(at.y, planet.height));
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
                int px = planet.WrapX(x), py = planet.WrapY(y);
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

    void DrawMiniMap()
    {
        var frame = new Rect(Screen.width - MiniMapSize - Margin, Margin, MiniMapSize, MiniMapSize);
        GUI.Box(new Rect(frame.x - 4f, frame.y - 4f, frame.width + 8f, frame.height + 8f), GUIContent.none);
        GUI.color = (Color)Unexplored;
        GUI.DrawTexture(frame, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // The whole world picture is drawn inside the frame, shifted so the player sits in the middle; the frame cuts
        // off everything outside the squares around them. Near an edge of the picture, copies of it are drawn
        // alongside so the mini map shows the land that lies across the join.
        float scale = MiniMapSize / MiniMapSquares;
        Vector2 at = PlayerOnPlanet();
        float worldWidth = planet.width * scale, worldHeight = planet.height * scale;
        float left = MiniMapSize / 2f - at.x * scale, top = MiniMapSize / 2f - (planet.height - at.y) * scale;
        GUI.BeginGroup(frame);
        for (int copyY = -1; copyY <= 1; copyY++)
        {
            for (int copyX = -1; copyX <= 1; copyX++)
            {
                var copy = new Rect(left + copyX * worldWidth, top + copyY * worldHeight, worldWidth, worldHeight);
                if (copy.xMax > 0f && copy.x < MiniMapSize && copy.yMax > 0f && copy.y < MiniMapSize) GUI.DrawTexture(copy, picture);
            }
        }
        // The ship's marker shows the shortest way home, and stays on the edge of the mini map when the ship is out of range.
        Vector2 toShip = planet.Nearest(ship, at) - at;
        var shipSpot = new Vector2(MiniMapSize / 2f + toShip.x * scale, MiniMapSize / 2f - toShip.y * scale);
        shipSpot.x = Mathf.Clamp(shipSpot.x, 5f, MiniMapSize - 5f);
        shipSpot.y = Mathf.Clamp(shipSpot.y, 5f, MiniMapSize - 5f);
        DrawMark(shipSpot, 8f, ShipMark);
        DrawMark(new Vector2(MiniMapSize / 2f, MiniMapSize / 2f), 7f, PlayerMark);
        GUI.EndGroup();
    }

    void DrawWorldMap()
    {
        if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };

        // Dim the game behind the map.
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float size = Mathf.Min(Screen.width, Screen.height) * 0.8f;
        var frame = new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f + 12f, size, size);
        GUI.Box(new Rect(frame.x - 6f, frame.y - 34f, frame.width + 12f, frame.height + 40f), GUIContent.none);
        GUI.Label(new Rect(frame.x, frame.y - 30f, frame.width, 26f), "World map    [M] close", titleStyle);
        GUI.DrawTexture(frame, picture);

        float scaleX = size / planet.width, scaleY = size / planet.height;
        Vector2 at = PlayerOnPlanet();
        DrawMark(new Vector2(frame.x + ship.x * scaleX, frame.y + (planet.height - ship.y) * scaleY), 10f, ShipMark);
        DrawMark(new Vector2(frame.x + at.x * scaleX, frame.y + (planet.height - at.y) * scaleY), 9f, PlayerMark);
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
