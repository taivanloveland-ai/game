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

## Mining mode (asked 2026-10-09)

- Pressing **R** on planet 1 switches the world to a 2D side-scroller. This is called **mining mode**. Pressing R again goes back to top-down.
- The side view shows the row of blocks the player is standing on.
- If you mine down (not possible yet), you can see the blocks behind you.
- In mining mode you can jump **1.5 blocks**. The grey rocks are **2 blocks tall**, so you cannot jump over them.
- Taivan wants to be given options when there are design choices to make. His picks for mining mode:
  - **Underground:** 4 or 5 layers of snow, with stone under the snow and ice for now. Where there is ice, it should look like a river or lake that was there and froze.
  - **Behind you:** the next few rows further back are drawn faded behind your row.
  - **Surface:** flat ground. Snow and ice are at the same height, with rocks standing 2 tall on top.

## Heights, jumping and changing rows (asked 2026-10-09)

- In mining mode, **W** takes you into the row behind you, if it is available.
- You can **jump in top-down mode** too.
- There are **higher levels of snow and ice**, so jumping is more useful.
- His picks from the options:
  - **Terrain:** the ground is a block higher for a while in some places and not in others. You have to fall back down to ground level. (Two levels: ground and one block up.)
  - **S key:** S takes you to the row in front. Jump is Space, since W is now used.
  - **Rocks:** never climbable. A rock is always 2 blocks taller than the highest ground touching it.
  - **Ice:** frozen steps too. Ice can be on either level and can drop a block along its length.

## Look and feel decisions

- **Engine:** Unity (decided 2026-10-09).
- **Art for now:** the character is just a rectangle. Everything else is simple coloured shapes too.
- **Top-down view is angled,** so you can see the edges of the cliffs (asked 2026-10-09).
- **Ice is a little slippery** (asked 2026-10-09).
- **Walking in snow is a little slower,** only a little (asked 2026-10-09). Currently 90% of full speed; ice is full speed.
- **No ore or crystals in the world for now.** Taivan asked for the placeholder ones to be taken away (2026-10-09). Reason not given.

## Built so far

- Unity project at the repo root, for editor 6000.6.5f1. Open `Assets/Scenes/PlanetOne` and press Play.
- Planet 1 is a 160 x 160 tile world that is generated when the game starts: snow, slippery ice lakes, raised rock with cliff faces on its south edges, and a rock wall around the map.
- A grey crashed ship sits in the middle of the map. The player is an orange rectangle that starts just below it and walks with WASD or the arrow keys. The camera follows the player.
- Every spot has a height. Ground is level 0 or, in patches (about a fifth of the walkable land), level 1. Rocks and the ship stand 2 above the highest ground next to them. In top-down, higher ground is drawn shifted up the screen with a cliff face on its south side.
- Top-down jump: Space, 1.5 blocks high, with a shadow on the ground. You need it to get onto level 1; walking off an edge drops you back down.
- Mining mode: R shows the player's row from the side, with three faded rows behind it, snow 4 or 5 blocks deep over stone, ice as a bowl up to 4 blocks deep, and a 1.5 block jump (Space). W or the up arrow steps to the row behind and S or the down arrow to the row in front, when nothing there is in the way. Ice is slippery and snow is a little slow there too. No mining yet.
- Checked by running the game in the background on 2026-10-09: the world builds; in top-down a ledge blocks walking but a jump gets you up, rocks stay blocked, and the jump peaks at 1.5 blocks; R switches views both ways; row stepping works and is refused when blocked. Real key presses, the feel of the ice, and how smoothly it runs have not been checked by Claude.
- Code: `Assets/Scripts/PlanetOne.cs` builds the world, `MiningMode.cs` builds the side view, `PlayerController.cs` handles movement, `GameInput.cs` reads the keyboard, `CameraFollow.cs` moves the camera, and `Assets/Editor/ProjectSetup.cs` creates the scene if it is missing.

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
- Mining itself is not built yet. How does it work in mining mode: what tool, which blocks can be dug, how long it takes, and what you get from snow, ice and stone?
- Where do the alien materials appear: inside the stone, in the ice, on the surface?
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
