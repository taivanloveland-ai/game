using System.Collections.Generic;
using UnityEngine;

// Items lying loose in the world. When the player comes close they slide to the player and go into the inventory.
public class Pickups : MonoBehaviour
{
    // How fast a drop slides to the player, and how close counts as picked up.
    const float PullSpeed = 7f;
    const float TakeDistance = 0.35f;
    // A fresh drop sits still for a moment so you see it land before it flies to you.
    const float PullDelay = 0.35f;
    // How close the player has to be for a thing knocked loose from the ground to start sliding over, and how big it is drawn.
    const float GroundPullRadius = 2f;
    const float GroundScale = 0.8f;

    class Dropped
    {
        public Item Item;
        // Where it is in the world as seen from above, and how high up it is in blocks.
        public Vector2 Position;
        public float Level;
        public float PullRadius;
        public float BornAt;
        public SpriteRenderer Renderer;
    }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    DayNight dayNight;
    Inventory inventory;
    readonly List<Dropped> drops = new List<Dropped>();

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, DayNight dayNight, Inventory inventory)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.dayNight = dayNight;
        this.inventory = inventory;
    }

    // Drops something that was standing on the ground, at ground level.
    public void Drop(Item item, Vector2 position)
    {
        float ground = planet.TopAt(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        Drop(item, position, ground, GroundPullRadius, GroundScale);
    }

    // Drops something at a chosen height, such as a block mined out of a wall. It slides to the player from up to
    // pullRadius blocks away, in any direction.
    public void Drop(Item item, Vector2 position, float level, float pullRadius, float scale)
    {
        var go = new GameObject("Dropped " + item.Name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = item.Icon;
        drops.Add(new Dropped { Item = item, Position = position, Level = level, PullRadius = pullRadius, BornAt = Time.time, Renderer = renderer });
    }

    void Update()
    {
        bool side = mining.Active;
        Vector2 playerPosition = player.GroundPosition;
        // Drops are pulled toward the player's middle, not their feet.
        float playerLevel = player.FeetHeight + PlayerController.Height / 2f;
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            Dropped drop = drops[i];
            // In the side-scroller only the drops lying in the row or column on screen are shown.
            bool visible = !side || mining.Shows(Vector2Int.FloorToInt(drop.Position));
            drop.Renderer.enabled = visible;
            if (!visible) continue;

            float bob = 0.06f + Mathf.Sin((Time.time - drop.BornAt) * 5f) * 0.05f;
            if (side)
            {
                drop.Renderer.transform.position = new Vector3(mining.Along(drop.Position), drop.Level + bob, 0f);
                drop.Renderer.sortingOrder = MiningMode.ObjectSortingOrder;
                drop.Renderer.color = dayNight.WorldTint;
            }
            else
            {
                drop.Renderer.transform.position = new Vector3(drop.Position.x, drop.Position.y + drop.Level * PlanetOne.HeightScale + bob, 0f);
                drop.Renderer.sortingOrder = 50;
                drop.Renderer.color = Color.white;
            }

            if (Time.time - drop.BornAt < PullDelay) continue;
            float across = Vector2.Distance(playerPosition, drop.Position), up = playerLevel - drop.Level;
            if (Mathf.Sqrt(across * across + up * up) > drop.PullRadius) continue;
            if (across <= TakeDistance && Mathf.Abs(up) <= PlayerController.Height)
            {
                inventory.Add(drop.Item);
                Destroy(drop.Renderer.gameObject);
                drops.RemoveAt(i);
                continue;
            }
            drop.Position = Vector2.MoveTowards(drop.Position, playerPosition, PullSpeed * Time.deltaTime);
            drop.Level = Mathf.MoveTowards(drop.Level, playerLevel, PullSpeed * Time.deltaTime);
        }
    }
}
