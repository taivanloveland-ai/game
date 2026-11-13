using UnityEngine;
using UnityEngine.Tilemaps;

// A tile that keeps switching between pictures, as Taivan's water blocks do (asked 2026-11-12: "the first 2 sprites
// are an animation of a water block, the second two are an animation, etc."). The tilemap plays it by itself.
public class AnimatedBlockTile : Tile
{
    // The pictures in turn, and how many it shows a second.
    public Sprite[] Frames;
    public float FramesPerSecond = 1.5f;

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (Frames == null || Frames.Length < 2) return false;
        tileAnimationData.animatedSprites = Frames;
        tileAnimationData.animationSpeed = FramesPerSecond;
        tileAnimationData.animationStartTime = 0f;
        return true;
    }

    public static AnimatedBlockTile Make(Color color, Sprite[] frames, bool isSolid)
    {
        var tile = ScriptableObject.CreateInstance<AnimatedBlockTile>();
        tile.sprite = frames[0];
        tile.Frames = frames;
        tile.color = color;
        tile.colliderType = isSolid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        return tile;
    }
}
