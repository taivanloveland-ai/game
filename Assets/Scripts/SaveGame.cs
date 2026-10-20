using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

// Saved games. There are five slots for survival mode and five for creative mode, each a file on the computer.
//
// A world is always made the same way from its seed, so a save does not hold the whole world. It holds the seed and
// everything that has changed since the world was made: the blocks dug and built (in the caves too), where the
// player is, their health and what they carry, the time of day, what has been harvested, which spiders have been
// killed, what is lying loose on the ground, and what the maps have seen.
public class SaveGame : MonoBehaviour
{
    public const int Slots = 5;
    // Changed whenever the layout of the file changes. A file with another number is not read.
    // (Version 1 had no name, versions 1 and 2 had no day count, versions 1 to 3 had no boots, and versions 1 to 4
    // had no cave crystals, versions 1 to 5 had not fixed the ship, versions 1 to 6 had no battery, and versions 1 to 7
    // had only Planet 1; their saves can still be read.)
    // Since version 8 the save holds both planets: which one the player is on, and for each planet what has changed
    // there (the blocks dug and built, what has been harvested, the spiders killed, what lies loose on the ground, what
    // the map has seen, and the crystals mined), the planet being played first.
    const int Version = 8;
    // The longest name a save can be given.
    public const int LongestName = 18;

    // What the title screen shows about a slot.
    public class Summary
    {
        public bool Creative;
        public int Seed;
        public System.DateTime Saved;
        // The name the player gave it. Empty for a save from before saves had names.
        public string Name = "";
    }

    // A saved game read from its file, ready to be played.
    public class Loaded
    {
        public bool Creative;
        public int Seed;
        public string Name = "";
        public int Version;
        // Which planet the player is on: 0 for Planet 1, 1 for the rocky planet.
        public int Planet;
        // Everything after the first few lines of the file.
        public byte[] Rest;
    }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    DayNight dayNight;
    CraftingNook nook;
    Inventory inventory;
    Pickups pickups;
    Gatherables gatherables;
    Health health;
    Spiders spiders;
    WorldMap map;
    Caves caves;
    int slot;
    bool creative;
    string saveName;
    // Every kind of item in the game, by name, for putting a saved inventory back.
    readonly Dictionary<string, Item> items = new Dictionary<string, Item>();
    // What has changed on the planet the player is not on, as it was saved, kept to be saved again.
    byte[] otherPlanet = new byte[0];

    // The slot this game is saved in, counting from 0, or -1 if it has none.
    public int Slot => slot;
    public bool Creative => creative;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, DayNight dayNight, Inventory inventory, Pickups pickups, Gatherables gatherables,
        CraftingNook nook, Mining pickaxeWork, Health health, Spiders spiders, WorldMap map, Caves caves, int slot, bool creative, string saveName)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.dayNight = dayNight;
        this.nook = nook;
        this.inventory = inventory;
        this.pickups = pickups;
        this.gatherables = gatherables;
        this.health = health;
        this.spiders = spiders;
        this.map = map;
        this.caves = caves;
        this.slot = slot;
        this.creative = creative;
        this.saveName = saveName ?? "";
        foreach (Item item in new[]
        {
            nook.Pickaxe, nook.Axe, nook.ThornAxe, nook.CrystalAxe, nook.ThornPickaxe, nook.CrystalPickaxe, nook.CrystalShard, nook.Battery, pickaxeWork.SnowItem, pickaxeWork.IceItem, pickaxeWork.StoneItem, pickaxeWork.AlgaeItem,
            gatherables.PlantItem, gatherables.ShardItem, gatherables.ClumpItem, gatherables.ThornItem, gatherables.VineItem, gatherables.StickItem,
            spiders.SilkItem, spiders.LegItem, spiders.FangItem, nook.Cleats, planet.GetComponent<IceBoss>()?.OrbItem,
        })
        {
            if (item != null) items[item.Name] = item;
        }
    }

    Item Find(string name)
    {
        return items.TryGetValue(name, out Item item) ? item : null;
    }

    // The automatic checks are started with -testSaves, and then keep their saves in a folder of their own, so they
    // can never touch the player's.
    static readonly bool TestSaves = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testSaves") >= 0;

    // Where a slot's file is kept.
    public static string PathFor(bool creative, int slot)
    {
        return Path.Combine(Application.persistentDataPath, TestSaves ? "saves-test" : "saves", (creative ? "creative" : "survival") + (slot + 1) + ".sav");
    }

    // What is in a slot, or null if it is empty or cannot be read.
    public static Summary Peek(bool creative, int slot)
    {
        string path = PathFor(creative, slot);
        if (!File.Exists(path)) return null;
        try
        {
            using (var r = new BinaryReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress)))
            {
                int version = r.ReadInt32();
                if (version < 1 || version > Version) return null;
                var summary = new Summary { Creative = r.ReadBoolean(), Seed = r.ReadInt32(), Saved = new System.DateTime(r.ReadInt64()) };
                if (version >= 2) summary.Name = r.ReadString();
                return summary;
            }
        }
        catch (System.Exception problem)
        {
            Debug.LogWarning("Could not read the saved game in " + path + ": " + problem.Message);
            return null;
        }
    }

    // Reads a slot's saved game, or gives null if it is empty or cannot be read.
    public static Loaded Load(bool creative, int slot)
    {
        if (Peek(creative, slot) == null) return null;
        using (var r = new BinaryReader(new GZipStream(File.OpenRead(PathFor(creative, slot)), CompressionMode.Decompress))) return Read(r);
    }

    static Loaded Read(BinaryReader r)
    {
        int version = r.ReadInt32();
        var loaded = new Loaded { Version = version, Creative = r.ReadBoolean(), Seed = r.ReadInt32() };
        r.ReadInt64();
        if (version >= 2) loaded.Name = r.ReadString();
        if (version >= 8) loaded.Planet = r.ReadInt32();
        using (var rest = new MemoryStream())
        {
            r.BaseStream.CopyTo(rest);
            loaded.Rest = rest.ToArray();
        }
        return loaded;
    }

    public static void Delete(bool creative, int slot)
    {
        string path = PathFor(creative, slot);
        if (File.Exists(path)) File.Delete(path);
    }

    // Saves the game in its slot. Returns false if it has no slot, or the file could not be written.
    public bool Save()
    {
        if (slot < 0) return false;
        return WriteFile(w => Write(w, PlanetOne.Rocky ? 1 : 0, player.GroundPosition, player.FeetHeight, planet.CaveIndex, PlanetChanges(), otherPlanet));
    }

    // Saves the game as it will be on the other planet, just landed there: the player in front of the ship's
    // workbench, and the two planets' changes swapped round. Gives it back to be carried on with straight away (and
    // writes it in the slot, if the game has one).
    public Loaded Travel(bool toRocky)
    {
        byte[] leaving = PlanetChanges();
        System.Action<BinaryWriter> write = w => Write(w, toRocky ? 1 : 0, PlanetOne.StartSpot(toRocky), 0f, -1, otherPlanet, leaving);
        if (slot >= 0) WriteFile(write);
        using (var memory = new MemoryStream())
        {
            using (var w = new BinaryWriter(memory, System.Text.Encoding.UTF8, true)) write(w);
            memory.Position = 0;
            using (var r = new BinaryReader(memory)) return Read(r);
        }
    }

    // What has changed on the planet being played.
    byte[] PlanetChanges()
    {
        using (var memory = new MemoryStream())
        {
            using (var w = new BinaryWriter(memory))
            {
                planet.WriteChanges(w);
                gatherables.Write(w);
                spiders.Write(w);
                pickups.Write(w);
                map.Write(w);
                planet.GetComponent<CaveCrystals>().Write(w);
                map.WriteNotes(w);
            }
            return memory.ToArray();
        }
    }

    bool WriteFile(System.Action<BinaryWriter> write)
    {
        try
        {
            string path = PathFor(creative, slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Written beside the real file first, so a save that goes wrong part way never spoils the one before.
            string fresh = path + ".new";
            using (var w = new BinaryWriter(new GZipStream(File.Create(fresh), System.IO.Compression.CompressionLevel.Fastest))) write(w);
            if (File.Exists(path)) File.Delete(path);
            File.Move(fresh, path);
            return true;
        }
        catch (System.Exception problem)
        {
            Debug.LogError("Could not save the game: " + problem);
            return false;
        }
    }

    // The whole save: which planet the player is on, where they are there, and the changes to the planet they are on
    // and to the other one.
    void Write(BinaryWriter w, int planetOn, Vector2 ground, float feet, int cave, byte[] here, byte[] there)
    {
        w.Write(Version);
        w.Write(creative);
        w.Write(planet.seed);
        w.Write(System.DateTime.Now.Ticks);
        w.Write(saveName);
        w.Write(planetOn);

        // The player: where they are, in which land, how they are, and the time of day.
        w.Write(ground.x);
        w.Write(ground.y);
        w.Write(feet);
        w.Write(cave);
        w.Write(health.Current);
        w.Write(dayNight.time);
        w.Write(dayNight.Day);
        w.Write(player.speed);

        // What they carry.
        w.Write(inventory.Stacks.Count);
        foreach (Inventory.Stack stack in inventory.Stacks)
        {
            w.Write(stack.Item.Name);
            w.Write(stack.Count);
        }
        w.Write(inventory.Tools.Count);
        foreach (Item tool in inventory.Tools) w.Write(tool.Name);
        foreach (Item held in inventory.Hotbar) w.Write(held != null ? held.Name : "");
        w.Write(inventory.EquippedSlot);
        w.Write(inventory.Boots != null ? inventory.Boots.Name : "");

        w.Write(here.Length);
        w.Write(here);
        w.Write(there.Length);
        w.Write(there);
        w.Write(nook.ShipFixed);
        w.Write(planet.GetComponent<SouthPoleBattery>().Taken);
    }

    // Puts back what has changed on the planet being played.
    void ReadPlanet(BinaryReader r, int version)
    {
        planet.ReadChanges(r);
        gatherables.Read(r);
        spiders.Read(r);
        pickups.Read(r, Find);
        map.Read(r);
        if (version >= 5) planet.GetComponent<CaveCrystals>().Read(r);
        // The notes on the map, if any were saved with this planet (since 2026-10-19).
        if (version >= 8 && r.BaseStream.Position < r.BaseStream.Length) map.ReadNotes(r);
    }

    // Puts a saved game back, once its world has been made again from its seed and the game set going.
    public void Apply(Loaded loaded)
    {
        using (var r = new BinaryReader(new MemoryStream(loaded.Rest)))
        {
            var ground = new Vector2(r.ReadSingle(), r.ReadSingle());
            float feet = r.ReadSingle();
            int cave = r.ReadInt32();
            float healthThen = r.ReadSingle();
            dayNight.time = r.ReadSingle();
            if (loaded.Version >= 3) dayNight.Day = r.ReadInt32();
            player.speed = r.ReadSingle();

            inventory.Stacks.Clear();
            inventory.Tools.Clear();
            int stacks = r.ReadInt32();
            for (int i = 0; i < stacks; i++)
            {
                Item item = Find(r.ReadString());
                int count = r.ReadInt32();
                if (item != null) inventory.Add(item, count);
            }
            int tools = r.ReadInt32();
            for (int i = 0; i < tools; i++)
            {
                Item tool = Find(r.ReadString());
                if (tool != null) inventory.Tools.Add(tool);
            }
            for (int i = 0; i < inventory.Hotbar.Length; i++) inventory.Hotbar[i] = Find(r.ReadString());
            inventory.EquippedSlot = r.ReadInt32();
            inventory.Boots = loaded.Version >= 4 ? Find(r.ReadString()) : null;

            if (loaded.Version >= 8)
            {
                // The planet being played (nothing yet, the first time there), then the other one, kept as it is.
                byte[] here = r.ReadBytes(r.ReadInt32());
                otherPlanet = r.ReadBytes(r.ReadInt32());
                if (here.Length > 0)
                {
                    using (var changes = new BinaryReader(new MemoryStream(here))) ReadPlanet(changes, loaded.Version);
                }
            }
            else ReadPlanet(r, loaded.Version);
            nook.ShipFixed = loaded.Version >= 6 && r.ReadBoolean();
            planet.GetComponent<SouthPoleBattery>().Taken = loaded.Version >= 7 && r.ReadBoolean();

            // Last, the player goes back where they were: into their cave first, if they were in one.
            health.Restore(healthThen);
            if (cave >= 0) caves.EnterNow(cave);
            player.PlaceAt(ground, feet);
        }
    }
}
