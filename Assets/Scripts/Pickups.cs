using System.Collections.Generic;
using UnityEngine;

// Items lying on the ground. When the player comes close they slide to the player and go into the inventory.
public class Pickups : MonoBehaviour
{
    // How close the player has to be for a drop to start sliding over, how fast it slides, and how close counts as picked up.
    const float PullRadius = 2f;
    const float PullSpeed = 7f;
    const float TakeDistance = 0.35f;
    // A fresh drop sits still for a moment so you see it land before it flies to you.
    const float PullDelay = 0.35f;
    // The player has to be at about the same height as the drop to pull it.
    const float HeightReach = 1.6f;

    class Dropped
    {
        public Item Item;
        // Where it lies in the world as seen from above.
        public Vector2 Position;
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

    public void Drop(Item item, Vector2 position)
    {
        var go = new GameObject("Dropped " + item.Name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = item.Icon;
        drops.Add(new Dropped { Item = item, Position = position, BornAt = Time.time, Renderer = renderer });
    }

    void Update()
    {
        bool side = mining.Active;
        Vector2 playerPosition = player.GroundPosition;
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            Dropped drop = drops[i];
            int x = Mathf.FloorToInt(drop.Position.x), row = Mathf.FloorToInt(drop.Position.y);
            // In the side-scroller only the drops lying in the row on screen are shown.
            bool visible = !side || row == mining.Row;
            drop.Renderer.enabled = visible;
            if (!visible) continue;

            float ground = planet.GroundAt(x, row);
            float bob = 0.06f + Mathf.Sin((Time.time - drop.BornAt) * 5f) * 0.05f;
            if (side)
            {
                drop.Renderer.transform.position = new Vector3(drop.Position.x, ground + bob, 0f);
                drop.Renderer.sortingOrder = MiningMode.ObjectSortingOrder;
                drop.Renderer.color = dayNight.WorldTint;
            }
            else
            {
                drop.Renderer.transform.position = new Vector3(drop.Position.x, drop.Position.y + ground * PlanetOne.HeightScale + bob, 0f);
                drop.Renderer.sortingOrder = 50;
                drop.Renderer.color = Color.white;
            }

            if (Time.time - drop.BornAt < PullDelay) continue;
            float distance = Vector2.Distance(playerPosition, drop.Position);
            if (distance > PullRadius || Mathf.Abs(player.FeetHeight - ground) > HeightReach) continue;
            if (distance <= TakeDistance)
            {
                inventory.Add(drop.Item);
                Destroy(drop.Renderer.gameObject);
                drops.RemoveAt(i);
                continue;
            }
            drop.Position = Vector2.MoveTowards(drop.Position, playerPosition, PullSpeed * Time.deltaTime);
        }
    }
}
