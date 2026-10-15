using System.Collections.Generic;
using UnityEngine;

// The ice boss: Taivan's picture of a tall tower of ice with a ball on top and ice pieces set in its sides
// (Assets/Resources/Art/IceBoss). Stepping onto either pole's ice cap, it rises out of the ice a little way to one side,
// four times as tall as the player.
//
// Every five seconds (but not while any of its pieces are still flying), the ball on top breaks and every ice piece
// in its sides comes flying straight at where the player is. Each one that hits takes 5 health. A shadow on the
// ground right under each flying piece shows where it is. When they are all gone, the ball and the pieces are
// whole again, ready for the next time.
//
// It is beaten with 30 hits of the ice axe, from close by. It drops nothing yet. Once beaten, it does not come back to
// that ice cap while the game is being played. It only acts in the top-down view, and not in the caves.
public class IceBoss : MonoBehaviour
{
    // It is this many times as tall as the player.
    const float TimesTaller = 4f;
    // It rises this many squares to one side of the player (and one square toward the pole), so all of it is on the
    // screen, taking this many seconds to push up out of the cracked ice, and starts throwing this long after it
    // has risen.
    const float RisesBeside = 6f, RiseSeconds = 1.8f, FirstThrowAfter = 2f;
    // Before it rises, the ice cracks open where it will come up, spreading out over this many seconds; and the
    // cracks fade away over this many seconds once it has risen. The view shakes this hard while it all happens.
    const float CrackSeconds = 1.4f, CracksFadeSeconds = 3f, ShakeHard = 0.06f;
    // The cracks are drawn in this many steps as they spread.
    const int CrackSteps = 10;
    // How long between throws, how much a hit takes, how fast the pieces fly (squares a second), and how far past
    // the player they carry on before they melt away.
    const float ThrowEvery = 5f, PieceDamage = 5f, PieceSpeed = 7f, FliesPast = 10f;
    // A piece hits the player if it comes this close to where they stand, while they are near the ground. It flies
    // down to this height above the ground, about the middle of the player.
    const float HitsWithin = 0.45f, PieceEndsAt = 0.45f;
    // How many hits of the ice axe it takes, how close the player must be, and how often they can swing.
    const int Hits = 30;
    const float AxeReach = 3f, SwingSeconds = 0.45f;
    // If the player goes further than this away, it sinks back into the ice until they come again.
    const float LeavesBeyond = 70f;
    // Ice pieces smaller than this many pixels in the picture are its other marks (its eye and the like), not pieces.
    const int SmallestPiece = 35;
    // The pieces are small in the picture, so while they fly they are drawn this many times as big, to be seen.
    const float PieceScale = 2f;
    const int Order = 60;

    class Piece
    {
        public Transform At;
        public SpriteRenderer Picture, Shadow;
        // Where it is over the ground, which way it flies, how high it is drawn above the ground, and how fast it
        // comes down; and how far it has flown and has to fly.
        public Vector2 Ground, Heading;
        public float Lift, Drop, Flown, Reach;
    }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Health health;
    Inventory inventory;
    CraftingNook nook;
    Hud hud;
    Camera cam;

    Sprite whole, broken, ball;
    Sprite[] pieceSprites;
    Vector2[] pieceOffsets;
    Sprite[] ballBits;
    Sprite dot, oval;
    float pixelsPerUnit;
    // The picture, and its lowest and highest rows, for drawing it part way out of the ice as it rises.
    Texture2D bossTexture;
    int bossBottom, bossTop;
    readonly Dictionary<int, Sprite> risingSprites = new Dictionary<int, Sprite>();
    Sprite[] crackSprites;
    SpriteRenderer cracks;
    float nextChunkAt;

    GameObject body;
    SpriteRenderer picture;
    Transform barBack, barFill;
    readonly List<Piece> flying = new List<Piece>();
    readonly List<(Transform at, Vector2 velocity, float born)> bits = new List<(Transform, Vector2, float)>();
    // Where it stands, as positions in the game go; whether it is up, since when, and its health.
    Vector2 foot;
    bool up;
    float roseAt, nextThrowAt, nextSwingAt, hurtUntil;
    int hitsLeft;
    bool upNorth;
    bool beatenNorth, beatenSouth;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, Health health, Inventory inventory, CraftingNook nook, Hud hud, Camera cam, Sprite square)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.health = health;
        this.inventory = inventory;
        this.nook = nook;
        this.hud = hud;
        this.cam = cam;
        dot = square;
        crackSprites = MakeCracks();
        // A soft oval for the shadows.
        oval = PlanetOne.PaintSprite(32, 16, new Vector2(0.5f, 0.5f), 32f, (x, y) =>
        {
            float dx = (x + 0.5f - 16f) / 16f, dy = (y + 0.5f - 8f) / 8f;
            return dx * dx + dy * dy <= 1f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        });
        if (!CutPicture()) return;

        // It stands in the way: nobody walks through it.
        System.Func<int, int, bool> before = planet.Obstacle;
        planet.Obstacle = (x, y) => (before != null && before(x, y)) || Blocks(x, y);
    }

    // Cuts Taivan's picture into its parts, without changing any of its pixels: the whole tower; the tower with its
    // ball gone and its pieces filled in with the ice around them (as it is while they fly); the ball; and each piece.
    bool CutPicture()
    {
        var texture = Resources.Load<Texture2D>("Art/IceBoss");
        if (texture == null) return false;
        int wide = texture.width, tall = texture.height;
        Color32[] pixels = PlanetOne.ReadPixels(texture);
        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < wide && y < tall && pixels[y * wide + x].a > 0;

        // The lowest and highest rows with anything in them, and the gap between the ball (at the top) and the tower.
        int bottom = tall, top = -1;
        for (int y = 0; y < tall; y++)
        {
            for (int x = 0; x < wide; x++)
            {
                if (!Solid(x, y)) continue;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }
        }
        int ballFrom = top;
        while (ballFrom > bottom && RowHasAny(pixels, wide, ballFrom - 1)) ballFrom--;

        // The ice itself is its commonest colour.
        var counts = new Dictionary<Color32, int>();
        foreach (Color32 c in pixels)
        {
            if (c.a == 0) continue;
            counts.TryGetValue(c, out int n);
            counts[c] = n + 1;
        }
        Color32 ice = default;
        int most = 0;
        foreach (KeyValuePair<Color32, int> entry in counts)
        {
            if (entry.Value > most) { most = entry.Value; ice = entry.Key; }
        }

        // The pieces: patches of other colours set inside the ice, not touching its edge, big enough to be pieces.
        var patch = new int[pixels.Length];
        var pieces = new List<List<int>>();
        var stack = new Stack<int>();
        for (int start = 0; start < pixels.Length; start++)
        {
            int sx = start % wide, sy = start / wide;
            if (patch[start] != 0 || !Solid(sx, sy) || pixels[start].Equals(ice) || sy >= ballFrom) continue;
            var cells = new List<int>();
            bool edge = false;
            patch[start] = -1;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int at = stack.Pop();
                cells.Add(at);
                int ax = at % wide, ay = at / wide;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = ax + dx, ny = ay + dy;
                        if (!Solid(nx, ny)) { edge = true; continue; }
                        int next = ny * wide + nx;
                        if (patch[next] != 0 || pixels[next].Equals(ice)) continue;
                        patch[next] = -1;
                        stack.Push(next);
                    }
                }
            }
            if (!edge && cells.Count >= SmallestPiece) pieces.Add(cells);
        }

        // It is drawn four times as tall as the player, standing on its lowest row.
        bossTexture = texture;
        bossBottom = bottom;
        bossTop = top;
        pixelsPerUnit = (top - bottom + 1) / (PlayerController.Height * TimesTaller);
        int middle = wide / 2;
        var pivot = new Vector2(0.5f, bottom / (float)tall);
        whole = Sprite.Create(texture, new Rect(0, 0, wide, tall), pivot, pixelsPerUnit);
        whole.texture.filterMode = FilterMode.Point;

        var brokenPixels = (Color32[])pixels.Clone();
        for (int y = ballFrom; y < tall; y++)
        {
            for (int x = 0; x < wide; x++) brokenPixels[y * wide + x] = new Color32(0, 0, 0, 0);
        }
        foreach (List<int> cells in pieces)
        {
            foreach (int at in cells) brokenPixels[at] = ice;
        }
        broken = MakeSprite(brokenPixels, wide, tall, new RectInt(0, 0, wide, tall), pivot);

        // The ball, and its four quarters, which fly apart when it breaks.
        var ballBox = BoxOf(pixels, wide, 0, ballFrom, wide, tall);
        ball = MakeSprite(pixels, wide, tall, ballBox, new Vector2(0.5f, 0.5f));
        ballBits = new Sprite[4];
        for (int i = 0; i < 4; i++)
        {
            int halfWide = ballBox.width / 2, halfTall = ballBox.height / 2;
            var quarter = new RectInt(ballBox.x + (i % 2) * halfWide, ballBox.y + (i / 2) * halfTall, i % 2 == 0 ? halfWide : ballBox.width - halfWide, i / 2 == 0 ? halfTall : ballBox.height - halfTall);
            ballBits[i] = MakeSprite(pixels, wide, tall, quarter, new Vector2(0.5f, 0.5f));
        }

        // Each piece on its own, and where it sits on the tower, from the foot.
        pieceSprites = new Sprite[pieces.Count];
        pieceOffsets = new Vector2[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            var only = new Color32[pixels.Length];
            int x0 = wide, x1 = 0, y0 = tall, y1 = 0;
            foreach (int at in pieces[i])
            {
                only[at] = pixels[at];
                x0 = Mathf.Min(x0, at % wide); x1 = Mathf.Max(x1, at % wide);
                y0 = Mathf.Min(y0, at / wide); y1 = Mathf.Max(y1, at / wide);
            }
            var box = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            pieceSprites[i] = MakeSprite(only, wide, tall, box, new Vector2(0.5f, 0.5f));
            pieceOffsets[i] = new Vector2((x0 + x1 + 1) / 2f - middle, (y0 + y1 + 1) / 2f - bottom) / pixelsPerUnit;
        }
        Debug.Log("Ice boss: " + pieces.Count + " ice pieces found in the picture");
        return pieces.Count > 0;
    }

    static bool RowHasAny(Color32[] pixels, int wide, int y)
    {
        for (int x = 0; x < wide; x++)
        {
            if (pixels[y * wide + x].a > 0) return true;
        }
        return false;
    }

    static RectInt BoxOf(Color32[] pixels, int wide, int fromX, int fromY, int toX, int toY)
    {
        int x0 = toX, x1 = fromX, y0 = toY, y1 = fromY;
        for (int y = fromY; y < toY; y++)
        {
            for (int x = fromX; x < toX; x++)
            {
                if (pixels[y * wide + x].a == 0) continue;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }
        }
        return new RectInt(x0, y0, Mathf.Max(1, x1 - x0 + 1), Mathf.Max(1, y1 - y0 + 1));
    }

    Sprite MakeSprite(Color32[] pixels, int wide, int tall, RectInt box, Vector2 pivot)
    {
        var cut = new Color32[box.width * box.height];
        for (int y = 0; y < box.height; y++)
        {
            for (int x = 0; x < box.width; x++) cut[y * box.width + x] = pixels[(box.y + y) * wide + box.x + x];
        }
        var texture = new Texture2D(box.width, box.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(cut);
        texture.Apply();
        if (box.width == wide && box.height == tall) return Sprite.Create(texture, new Rect(0, 0, wide, tall), pivot, pixelsPerUnit);
        return Sprite.Create(texture, new Rect(0, 0, box.width, box.height), pivot, pixelsPerUnit);
    }

    // True for the squares its base stands on, so they cannot be walked into.
    bool Blocks(int x, int y)
    {
        if (!up) return false;
        Vector2Int at = planet.Wrap(Vector2Int.FloorToInt(foot));
        return y == at.y && Mathf.Abs(x - at.x) <= 1;
    }

    void Update()
    {
        if (whole == null) return;
        bool topDown = !mining.Active && !planet.InCave;
        if (!up)
        {
            if (topDown) TryToRise();
            return;
        }

        // Too far away, or gone into a cave: it sinks back into the ice.
        Vector2 at = player.GroundPosition;
        Vector2 near = planet.Nearest(foot, at);
        if (planet.InCave || Vector2.Distance(near, at) > LeavesBeyond)
        {
            Sink();
            return;
        }
        foot = near;
        body.SetActive(topDown);
        Place();
        MoveBits();
        if (!topDown)
        {
            ClearPieces();
            return;
        }

        if (Risen >= 1f)
        {
            if (flying.Count == 0 && Time.time >= nextThrowAt) Throw();
            MovePieces();
            TakeSwings();
        }
    }

    // Stepping onto an ice cap that has not beaten it yet brings it up out of the ice ahead.
    void TryToRise()
    {
        Vector2 at = player.GroundPosition;
        if (!planet.OnIceCap(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y), out bool north)) return;
        if (north ? beatenNorth : beatenSouth) return;
        upNorth = north;
        // Beside the player, east or west, and a square toward the pole: up the map in the north, down it in the south.
        foot = at + new Vector2(Random.value < 0.5f ? -RisesBeside : RisesBeside, north ? 1f : -1f);
        up = true;
        roseAt = Time.time;
        nextThrowAt = Time.time + CrackSeconds + RiseSeconds + FirstThrowAfter;
        hitsLeft = Hits;
        Build();
        hud.Say("Something rises out of the ice!", 3f);
    }

    void Build()
    {
        body = new GameObject("Ice Boss");
        body.transform.SetParent(planet.TopDownRoot, false);
        picture = body.AddComponent<SpriteRenderer>();
        picture.sprite = whole;
        picture.spriteSortPoint = SpriteSortPoint.Pivot;
        // Its health, as a bar over its head.
        barBack = MakeBar("Health Bar", new Color(0.08f, 0.09f, 0.12f, 0.85f), Order + 2);
        barFill = MakeBar("Health", new Color(0.84f, 0.2f, 0.22f, 1f), Order + 3);
        // The cracks in the ice where it comes up, drawn on the ice round its foot.
        var crackObject = new GameObject("Cracks");
        crackObject.transform.SetParent(body.transform, false);
        crackObject.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        cracks = crackObject.AddComponent<SpriteRenderer>();
        cracks.spriteSortPoint = SpriteSortPoint.Pivot;
        // A shadow round its foot, drawn on the ice just behind it.
        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(body.transform, false);
        shadow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        shadow.transform.localScale = new Vector3(whole.bounds.size.x * 0.95f, 0.7f, 1f);
        var shade = shadow.AddComponent<SpriteRenderer>();
        shade.sprite = oval;
        shade.color = new Color(0.05f, 0.1f, 0.2f, 0.3f);
        shade.spriteSortPoint = SpriteSortPoint.Pivot;
    }

    Transform MakeBar(string name, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(body.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = dot;
        renderer.color = color;
        renderer.sortingOrder = order;
        return go.transform;
    }

    // Puts it where it stands, rising out of the ice to begin with, with its health bar over it.
    void Place()
    {
        float risen = Risen;
        float height = PlayerController.Height * TimesTaller;
        body.transform.position = new Vector3(foot.x, foot.y + planet.TopAt(Mathf.FloorToInt(foot.x), Mathf.FloorToInt(foot.y)) * PlanetOne.HeightScale, 0f);
        ShowRising(risen);
        picture.color = Time.time < hurtUntil ? new Color(1f, 0.55f, 0.55f) : Color.white;
        float barWide = 2.4f, filled = barWide * hitsLeft / Hits;
        barBack.localPosition = new Vector3(0f, height + 0.35f, 0f);
        barBack.localScale = new Vector3(barWide + 0.08f, 0.2f, 1f);
        barFill.localPosition = new Vector3(-(barWide - filled) / 2f, height + 0.35f, 0f);
        barFill.localScale = new Vector3(filled, 0.12f, 1f);
        barBack.gameObject.SetActive(risen >= 1f);
        barFill.gameObject.SetActive(risen >= 1f);
    }

    // How far it has risen out of the ice: 0 while the ice is still cracking and when it starts to come up, 1 once it
    // is all the way out.
    float Risen => Mathf.Clamp01((Time.time - roseAt - CrackSeconds) / RiseSeconds);

    // The ice cracks open, then it pushes up out of the cracks, the part still under the ice unseen, with chunks of
    // ice thrown up round it and the view shaking. Then the cracks slowly fade.
    void ShowRising(float risen)
    {
        float since = Time.time - roseAt;
        float spread = Mathf.Clamp01(since / CrackSeconds);
        cracks.sprite = crackSprites[Mathf.Min(CrackSteps - 1, Mathf.FloorToInt(spread * CrackSteps))];
        float fade = 1f - Mathf.Clamp01((since - CrackSeconds - RiseSeconds) / CracksFadeSeconds);
        cracks.enabled = fade > 0f;
        cracks.color = new Color(1f, 1f, 1f, fade);

        bool rising = risen < 1f;
        CameraFollow.Shake = rising && body.activeSelf ? ShakeHard * (since < CrackSeconds ? 0.5f : 1f) : 0f;
        if (!rising)
        {
            if (picture.sprite != broken) picture.sprite = whole;
            return;
        }
        // How many rows of the picture are out of the ice, from the top down.
        int rows = Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, risen) * (bossTop - bossBottom + 1));
        picture.sprite = rows > 0 ? RisingSprite(rows) : null;
        if (risen > 0f && Time.time >= nextChunkAt)
        {
            nextChunkAt = Time.time + 0.06f;
            ThrowChunk();
        }
    }

    // The top part of the picture, a number of rows tall, standing on the ice: the rest is still under it.
    Sprite RisingSprite(int rows)
    {
        if (risingSprites.TryGetValue(rows, out Sprite sprite)) return sprite;
        int from = bossTop - rows + 1;
        sprite = Sprite.Create(bossTexture, new Rect(0, from, bossTexture.width, bossTexture.height - from), new Vector2(0.5f, 0f), pixelsPerUnit);
        risingSprites[rows] = sprite;
        return sprite;
    }

    // A chunk of ice thrown up from round its foot as it breaks out.
    void ThrowChunk()
    {
        SpriteRenderer chunk = MakeFlyingThing("Ice Chunk", dot, Order + 1);
        float size = Random.Range(0.1f, 0.22f);
        chunk.transform.localScale = new Vector3(size, size, 1f);
        chunk.color = Random.value < 0.5f ? new Color32(206, 235, 248, 255) : new Color32(146, 202, 232, 255);
        float side = Random.Range(-1f, 1f);
        chunk.transform.position = body.transform.position + new Vector3(side * whole.bounds.size.x * 0.5f, 0.1f, 0f);
        bits.Add((chunk.transform, new Vector2(side * 2.2f + Random.Range(-0.6f, 0.6f), Random.Range(2.5f, 4.5f)), Time.time));
    }

    // The cracks, drawn in steps as they spread: jagged lines running out from the middle across the ice, dark
    // blue with a pale edge. Drawn flatter than they are wide, since the ice is seen at a slant.
    Sprite[] MakeCracks()
    {
        const int wide = 112, tall = 56, perUnit = 24;
        var rand = new System.Random(7);
        // Each crack is a line of points from the middle outward, with a few branching off part way.
        var lines = new List<List<Vector2>>();
        for (int i = 0; i < 9; i++)
        {
            float angle = i * Mathf.PI * 2f / 9f + (float)rand.NextDouble() * 0.5f;
            var line = new List<Vector2> { Vector2.zero };
            Vector2 at = Vector2.zero;
            int length = 6 + rand.Next(5);
            for (int step = 0; step < length; step++)
            {
                angle += ((float)rand.NextDouble() - 0.5f) * 0.9f;
                at += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.11f;
                line.Add(at);
                if (step == length / 2 && rand.NextDouble() < 0.6)
                {
                    var branch = new List<Vector2> { at };
                    Vector2 b = at;
                    float turn = angle + (rand.NextDouble() < 0.5 ? 0.9f : -0.9f);
                    for (int k = 0; k < 3; k++)
                    {
                        b += new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)) * 0.09f;
                        branch.Add(b);
                    }
                    lines.Add(branch);
                }
            }
            lines.Add(line);
        }
        var sprites = new Sprite[CrackSteps];
        for (int stage = 0; stage < CrackSteps; stage++)
        {
            // How far out from the middle the cracks have reached, as a share of the whole.
            float reach = (stage + 1f) / CrackSteps;
            sprites[stage] = PlanetOne.PaintSprite(wide, tall, new Vector2(0.5f, 0.5f), perUnit, (x, y) =>
            {
                // From the middle, in shares of the half-width, with the height stretched back out.
                var p = new Vector2((x + 0.5f - wide / 2f) / (wide / 2f), (tall / 2f - y - 0.5f) / (wide / 2f) * 2f);
                float nearest = float.MaxValue;
                foreach (List<Vector2> line in lines)
                {
                    for (int k = 1; k < line.Count; k++)
                    {
                        if (line[k].magnitude > reach) break;
                        nearest = Mathf.Min(nearest, DistanceToSegment(p, line[k - 1], line[k]));
                    }
                }
                float pixel = 1f / (wide / 2f);
                if (nearest < pixel * 1.1f) return new Color32(52, 104, 150, 255);
                if (nearest < pixel * 2.1f) return new Color32(226, 244, 252, 255);
                return new Color32(0, 0, 0, 0);
            });
        }
        return sprites;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 along = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, along) / Mathf.Max(0.0001f, along.sqrMagnitude));
        return Vector2.Distance(p, a + along * t);
    }

    // The ball breaks, and every piece comes flying at where the player is.
    void Throw()
    {
        picture.sprite = broken;
        Vector2 target = player.GroundPosition;
        float height = PlayerController.Height * TimesTaller;
        for (int i = 0; i < 4; i++)
        {
            Transform bit = MakeFlyingThing("Ball Piece", ballBits[i], Order + 1).transform;
            bit.position = body.transform.position + new Vector3(0f, height - ball.bounds.size.y / 2f, 0f);
            bits.Add((bit, new Vector2(i % 2 == 0 ? -1.4f : 1.4f, i / 2 == 0 ? -0.6f : 1.6f), Time.time));
        }
        for (int i = 0; i < pieceSprites.Length; i++)
        {
            var piece = new Piece();
            SpriteRenderer shape = MakeFlyingThing("Ice Piece", pieceSprites[i], Order);
            shape.transform.localScale = new Vector3(PieceScale, PieceScale, 1f);
            piece.At = shape.transform;
            piece.Picture = shape;
            // Its shadow, a dark oval on the ground right under it.
            piece.Shadow = MakeFlyingThing("Ice Piece Shadow", oval, 0);
            piece.Shadow.color = new Color(0.05f, 0.1f, 0.2f, 0.35f);
            float shadowWide = Mathf.Max(0.3f, pieceSprites[i].bounds.size.x * PieceScale);
            piece.Shadow.transform.localScale = new Vector3(shadowWide, shadowWide * 0.7f, 1f);
            piece.Ground = foot + new Vector2(pieceOffsets[i].x, -0.05f);
            piece.Lift = pieceOffsets[i].y;
            Vector2 toward = target - piece.Ground;
            float distance = Mathf.Max(0.5f, toward.magnitude);
            piece.Heading = toward / distance;
            piece.Drop = (piece.Lift - PieceEndsAt) / (distance / PieceSpeed);
            piece.Reach = distance + FliesPast;
            flying.Add(piece);
        }
    }

    SpriteRenderer MakeFlyingThing(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(planet.TopDownRoot, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    void MovePieces()
    {
        Vector2 at = player.GroundPosition;
        for (int i = flying.Count - 1; i >= 0; i--)
        {
            Piece piece = flying[i];
            float step = PieceSpeed * Time.deltaTime;
            piece.Ground += piece.Heading * step;
            piece.Flown += step;
            float before = piece.Lift;
            piece.Lift = Mathf.Max(PieceEndsAt, piece.Lift - piece.Drop * Time.deltaTime);
            // Drawn as high above its shadow as it is above the ground, pointing the way it flies.
            piece.At.position = new Vector3(piece.Ground.x, piece.Ground.y + piece.Lift, 0f);
            Vector2 drawnMove = piece.Heading * step + new Vector2(0f, piece.Lift - before);
            piece.At.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(drawnMove.y, drawnMove.x) * Mathf.Rad2Deg - 90f);
            piece.Shadow.transform.position = new Vector3(piece.Ground.x, piece.Ground.y, 0f);

            bool hits = Vector2.Distance(piece.Ground, at) < HitsWithin && player.FeetHeight < PieceEndsAt + 0.6f;
            if (hits) health.Hurt(PieceDamage);
            if (hits || piece.Flown > piece.Reach)
            {
                Destroy(piece.At.gameObject);
                Destroy(piece.Shadow.gameObject);
                flying.RemoveAt(i);
            }
        }
        // When the last piece is gone the ball and the pieces are whole again, and the next throw is five seconds off.
        if (flying.Count == 0 && picture.sprite == broken)
        {
            picture.sprite = whole;
            nextThrowAt = Time.time + ThrowEvery;
        }
    }

    // The quarters of the broken ball fly apart, fall and fade.
    void MoveBits()
    {
        for (int i = bits.Count - 1; i >= 0; i--)
        {
            (Transform at, Vector2 velocity, float born) = bits[i];
            float age = Time.time - born;
            if (age > 0.7f)
            {
                Destroy(at.gameObject);
                bits.RemoveAt(i);
                continue;
            }
            velocity.y -= 9f * Time.deltaTime;
            bits[i] = (at, velocity, born);
            at.position += (Vector3)(velocity * Time.deltaTime);
            at.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 1f - age / 0.7f);
        }
    }

    void ClearPieces()
    {
        foreach (Piece piece in flying)
        {
            Destroy(piece.At.gameObject);
            Destroy(piece.Shadow.gameObject);
        }
        flying.Clear();
        if (picture != null && picture.sprite == broken) picture.sprite = whole;
    }

    // The ice axe: a click on it, from close enough, is one hit. Holding the button keeps swinging.
    void TakeSwings()
    {
        if (Time.time < nextSwingAt || inventory.Equipped != nook.Axe || health.Down) return;
        if (!GameInput.LeftClickPressed() && !GameInput.LeftClickHeld()) return;
        Vector2 point = cam.ScreenToWorldPoint(GameInput.PointerPosition());
        if (!picture.bounds.Contains(new Vector3(point.x, point.y, picture.bounds.center.z))) return;
        nextSwingAt = Time.time + SwingSeconds;
        if (Vector2.Distance(player.GroundPosition, foot) > AxeReach)
        {
            hud.Say("Get closer to hit it", 1.5f);
            return;
        }
        Hit();
    }

    // One hit with the ice axe. The last one shatters it.
    public void Hit()
    {
        if (!up) return;
        hitsLeft--;
        hurtUntil = Time.time + 0.12f;
        if (hitsLeft > 0) return;
        if (upNorth) beatenNorth = true;
        else beatenSouth = true;
        hud.Say("It shatters!", 3f);
        Sink();
    }

    // Takes it away: beaten, or left behind.
    void Sink()
    {
        CameraFollow.Shake = 0f;
        ClearPieces();
        up = false;
        if (body != null) Destroy(body);
    }

    // For checking: whether it is up, where it stands, how many hits it has left, and how many pieces are flying.
    public bool Up => up;
    public Vector2 Foot => foot;
    public int HitsLeft => hitsLeft;
    public int Flying => flying.Count;
    public int PieceCount => pieceSprites != null ? pieceSprites.Length : 0;
}
