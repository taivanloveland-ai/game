# Game context

Running record of what Taivan wants for the game, what has been built, and what is still undecided. Updated as work goes on.

## Roles

- Taivan directs the design. Claude writes the code.
- The game must be different from his brother's game. His brother is also building a 2D side-scroller, and Taivan does not want to copy it.

## The game

- **Setting:** not Earth. An alien solar system, with its own invented metals and materials (not iron, copper or gold).
- **Player:** a man whose spaceship crashed. He starts with nothing.
- **Story so far:** he was flying to the planet closest to the sun and crashed on the way. The planet he crashed on is cold. The full backstory is not worked out yet.
- **Progression:** he repairs the ship and flies to planet 2, then goes on to the next planet, and the next.
- **Four planets** (current thinking, 2026-10-08), and the view changes by planet:
  1. Planet 1, the cold crash planet: top-down
  2. Planet 2: top-down
  3. Planet 3: side-scrolling
  4. Planet 4: side-scrolling
- **Planets are very different from each other,** with different temperatures. Some materials appear on more than one planet; others are unique to a planet.
- **Materials are collected by the player.**
- **Space suit and air:** the space suit itself is warm, and the air is breathable (said 2026-10-09). Claude's reading, not confirmed: this is about the cold crash planet, so there is no oxygen to manage and the suit protects him from the cold.

## Look and feel decisions

- **Engine:** Unity (decided 2026-10-09).
- **Art for now:** the character is just a rectangle. Everything else is simple coloured shapes too.
- **Top-down view is angled,** so you can see the edges of the cliffs (asked 2026-10-09).
- **Ice is a little slippery** (asked 2026-10-09).
- **No ore or crystals in the world for now.** Taivan asked for the placeholder ones to be taken away (2026-10-09). Reason not given.

## Built so far

- Unity project at the repo root, for editor 6000.6.5f1. Open `Assets/Scenes/PlanetOne` and press Play.
- Planet 1 is a 160 x 160 tile world that is generated when the game starts: snow, slippery ice lakes, raised rock with cliff faces on its south edges, and a rock wall around the map.
- A grey crashed ship sits in the middle of the map. The player is an orange rectangle that starts just below it and walks with WASD or the arrow keys. The camera follows the player.
- Checked by running the game in the background on 2026-10-09: the world builds and collisions work. The slippery ice was compiled but its feel has not been checked by Claude.
- Code: `Assets/Scripts/PlanetOne.cs` builds the world, `PlayerController.cs` handles movement, `CameraFollow.cs` moves the camera, and `Assets/Editor/ProjectSetup.cs` creates the scene if it is missing.

## Build approach (Claude's plan)

- Inventory, materials, crafting, the ship and saving are built once and shared by every planet. Only movement, the world layout and how things are drawn change between a top-down planet and a side-scrolling one.
- The cold crash planet is finished before any other planet is started.

## Claude's reading (not confirmed)

- The crash planet is cold because it is far from the sun. Each planet after it is closer to the sun and warmer, ending at the innermost planet he was heading for.

## Inspirations mentioned

- Terraria (2D side-scroller)
- Stardew Valley (top-down)

## Open questions

- What is his brother's game like (genre, setting, main mechanic)? Needed so this one can go a different way.
- What are the alien materials, and what does each one do?
- How are materials collected (mining deposits, picking things up, digging the ground)?
- If the suit keeps him warm, what is the danger on the cold planet? Can the suit be too weak for the coldest areas?
- Are there creatures or enemies?
- What does repairing the ship need?
- Does the ship need repairing or upgrading again on later planets?
- What makes each of the four planets different (temperature, materials, dangers)?
- Is planet 4 the innermost planet he was heading for, and the end of the game?
- Why was he going to the innermost planet?
- What kind of side-scroller are planets 3 and 4 (building, exploring, combat, jumping challenges)?
- Tone of the world (dangerous, mysterious, cozy, silly).
- Art style, and where the art comes from.
- Where the finished game should run (Windows PC, browser, phone).
