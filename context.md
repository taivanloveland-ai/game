# Game context

Running record of what Taivan wants for the game, what has been built, and what is still undecided. Updated as work goes on.

## Roles

- Taivan directs the design. Claude writes the code.
- The game must be different from his brother's game. His brother is also building a 2D side-scroller, and Taivan does not want to copy it.

## Taivan's shorthand

- **sc** = the side-scroller view (mining mode on planet 1).
- **td** = the top-down view.

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

## Sky, day and night (asked 2026-10-09)

- **Story:** the planets orbit a binary star system where the other star is a black hole.
- In the side-scroller you can see the **sun in the daytime** and the **black hole at night**.
- His picks from the options:
  - **Day length:** 2 minutes for a full day and night, for now, for testing purposes. Expect this to be lengthened later.
  - **Black hole:** tilted disc. A black disc with a thin ring and a flat glowing band across its middle.
  - **Sun:** red and dim.
  - **Night:** everything gets darker, in both sc and top-down.

## Plants and inventory (asked 2026-10-09)

- An **alien plant**, 50% of the player's height.
- **Right click** it when at most a block away and it drops; you can then pick it up into your inventory.
- The **inventory has no limit**. It just gets bigger with more items.
- His picks from the options:
  - **Right click:** you click on the plant itself with the mouse.
  - **Views:** plants show and can be harvested in both td and sc.
  - **Pick up:** the dropped plant is pulled toward you when you are close.
  - **Inventory on screen:** only a hotbar, and it is for tools. Press Tab to see what items you have.

## Plants as rectangles, ice shards, and click-to-collect (asked 2026-10-09)

- **Plants are just green rectangles.** Later the same day he changed this: the plant is a **bluish green fiddlehead fern** (a stalk with a curled spiral tip), still half the player's height, named "Fiddlehead fern". He then drew his own fern (a green spiral on a stem) and asked for it to be fixed up a lot so it looks professional and matches the ice shard's style. Claude redrew it from his sketch as a smooth teal fiddlehead with a dark edge and a lit and a shaded side, stored at `Assets/Resources/Art/Fern.png`. On 2026-10-10 he asked for it to be just as pixelated as the ice shard, so it was redrawn with hard pixel edges, four flat colours, and the same number of picture pixels per block as his shard picture (41 by 71 pixels). Later that day he asked for it to be **bigger and less ugly around the edges**: it now stands three quarters of the player's height (this replaces the original "50% of the player's height"), drawn at 61 by 108 pixels with a thinner, slightly lighter edge and smoother pixel outlines. It is used for the standing plant, the dropped item and the inventory (the standing-plant part is Claude's choice, not confirmed).
- **Sharp shards of ice**, found on the ice, are picked up like the plants. (He first said thin sharp rocks, then changed it to ice shards.) They are **long, as though they are the pick on a pickaxe**, and they **look like part of the ice** (same blues as the ice, no dark outline). **After you right click one, it looks like the picture Taivan supplied** (a blue diagonal shard), stored at `Assets/Resources/Art/IceShardItem.png`. This is the first real art in the game. He replaced the first picture with a paler one, then asked for it to stand out a bit more but look about the same, so Claude added a thin darker blue edge to his picture. He said it looked the same and asked for the item's colours to stand out more on the ground, so it is now his shape with a pale, near-white blue inside and a dark navy edge. His untouched picture is in git history (commit c268eaa).
- **Right clicking** a plant or shard makes you **move until you are close enough to collect it**. If it is unavailable, right clicking does nothing.
- For the walk he first picked "finds a way and jumps", then changed it: **just walk in a straight line**. It still jumps onto raised ground by itself (Claude kept that; not confirmed).
- **W and S in sc get an animation**, so changing rows is not so much faster than walking in. He first asked for "like a fade"; Claude made the whole screen fade to black, and he changed it: instead of the whole screen blacking out, **the blocks shift forward, get brighter, and become the front blocks**.

## Taivan's own art (in `Assets/Resources/Art`)

- `IceShardItem.png`: the loose ice shard (see above).
- `Fern.png`: the fiddlehead fern, redrawn by Claude from his sketch (see above).
- `SnowBlock.png`: a white square with faint grey streaks, supplied 2026-10-10 "for the snow blocks". It is used for the top of snow tiles in td and for snow blocks in sc (both views confirmed 2026-10-10: "make the snow in the top down too"). It is shown exactly as he drew it: one picture per block, same way up, same colours. Claude's first version tinted and flipped it, and through an import problem it did not show in td at all; he objected ("the snow isnt what i made!"). Lesson: use his art unaltered unless he asks for changes.
- `IceBlock.png`: a bright blue square with pale diagonal streaks, supplied 2026-10-10 as "ice for sc and td". Used the same way as the snow picture: the top of ice tiles in td and ice blocks in sc, exactly as drawn. The standing ice shards were recoloured to this picture's blues so they still look like part of the ice.
- Art sent so far is 128 by 128 pixels with hard pixel edges.

### Art frustration and fixes (2026-10-10)

- He said he was frustrated: the fern did not look like it belonged, and the ice shard sprite, the spike and the ice block sprite were ugly. Claude's diagnosis was that the pieces were made separately in clashing styles (different pixel sizes, outlines and shading on some things only, pictures repeating in every square, bright colours next to muted ones).
- He then asked for three fixes, plus taller ferns:
  - **Fern looks like it is actually growing there:** it now comes up out of a small mound of snow, casts a soft shadow on the ground, has a young frond beside the tall one, and its edge is a soft teal instead of near-black. The tall frond is about the player's height.
  - **No outline on the loose ice shard:** it is his shape in a pale blue with no edge.
  - **Ice blocks look good:** his `IceBlock.png` is no longer used (it is in git history). Ice is drawn by the game as a clear blue with a few short pale glints and the odd crack, picked at random per square so nothing repeats; raised ice is paler. In sc, ice goes from that blue to a deeper blue with depth.
- Not yet addressed: he also called the standing spike ugly. It was only recoloured to match the new ice.

## Look and feel decisions

- **Engine:** Unity (decided 2026-10-09).
- **Art for now:** the character is just a rectangle. Everything else is simple coloured shapes too.
- **Top-down view is angled,** so you can see the edges of the cliffs (asked 2026-10-09).
- **Ice is a little slippery** (asked 2026-10-09).
- **Walking in snow is a little slower,** only a little (asked 2026-10-09). Currently 90% of full speed; ice is full speed.
- **Deeper blocks are gradually shaded in the side-scroller, and after the first 1.5 blocks it turns black** (asked 2026-10-09). This is for mining mode only, not the top-down view. It took three tries: Claude first shaded the low ground in top-down, then made a gentle shade that never reached black; Taivan corrected both.
- **No ore or crystals in the world for now.** Taivan asked for the placeholder ones to be taken away (2026-10-09). Reason not given.

## Built so far

- Unity project at the repo root, for editor 6000.6.5f1. Open `Assets/Scenes/PlanetOne` and press Play.
- Planet 1 is a 160 x 160 tile world that is generated when the game starts: snow, slippery ice lakes, raised rock with cliff faces on its south edges, and a rock wall around the map.
- A grey crashed ship sits in the middle of the map. The player is an orange rectangle that starts just below it and walks with WASD or the arrow keys. The camera follows the player.
- Every spot has a height. Ground is level 0 or, in patches (about a fifth of the walkable land), level 1. Rocks and the ship stand 2 above the highest ground next to them. In top-down, higher ground is drawn shifted up the screen with a cliff face on its south side.
- Top-down jump: Space, 1.5 blocks high, with a shadow on the ground. You need it to get onto level 1; walking off an edge drops you back down.
- Mining mode: R shows the player's row from the side, with three faded rows behind it, snow 4 or 5 blocks deep over stone, ice as a bowl up to 4 blocks deep, and a 1.5 block jump (Space). W or the up arrow steps to the row behind and S or the down arrow to the row in front, when nothing there is in the way. Each step takes a quarter of a second, about the time it takes to walk one block in td: stepping back, the rows behind slide forward and brighten, the nearest one becoming the front row while the old front row fades away; stepping forward is the reverse. Holding the key keeps stepping. To make the slide visible, each row further back is drawn 0.2 blocks higher than the one in front of it. Ice is slippery and snow is a little slow there too. No mining yet.
- Mining mode darkness: the ground is lit where it touches open air and fades to black 1.5 blocks in. Shading only applies to a block if there is a block in front of it in the next row forward (asked 2026-10-09); a block with nothing in front stays fully lit and lets light into its neighbours.
- Day and night: a 2 minute cycle starting just after sunrise. In sc the red sun arcs across a dusky purple sky by day; at night the sky goes black with stars and the black hole arcs across. At night the sc ground and player are tinted dark blue, and top-down gets a dark blue wash.
- Things to collect: about 370 fiddlehead ferns on the snow (2.5% of snow squares, bluish green, three quarters of the player's height) and about 120 ice shards on the ice (3% of ice squares, thin pale spikes, placeholder name "Ice shard"). Neither grows back.
- Collecting: right click one. Within a block it drops at once; otherwise the player walks to it in a straight line, jumping onto raised ground, then it drops. If a rock or the ship is on that line, the click does nothing. In sc the walk is along the row only. Pressing a movement key or jump cancels the walk. A drop sits for a third of a second, then slides to the player from up to 2 blocks away and goes into the inventory.
- Inventory: stacks per kind of item with no limits. On screen there is a 5-slot tool hotbar (empty, no tools exist yet) and an Items panel opened with Tab that grows a row at a time.
- Checked by running the game in the background on 2026-10-09: the world builds; in top-down a ledge blocks walking but a jump gets you up, rocks stay blocked, and the jump peaks at 1.5 blocks; R switches views both ways; row stepping works and is refused when blocked. Collecting works in td and sc: a click on empty ground does nothing, and a click on a plant 7 blocks away, a plant up on raised ground, an ice shard, and a plant along the row in sc each walk the player over and end with the item in the inventory. Real key presses and mouse clicks, how the hotbar and Items panel look, the feel of the ice, and how smoothly it runs have not been checked by Claude.
- Code: `Assets/Scripts/PlanetOne.cs` builds the world, `MiningMode.cs` builds the side view, `PlayerController.cs` handles movement, `DayNight.cs` runs the sky and night, `Gatherables.cs` holds plants, ice shards and the walk-to-collect, `Pickups.cs` the dropped items, `Inventory.cs` and `Hud.cs` the inventory and its display, `Assets/Editor/ArtImport.cs` sets up any picture dropped into `Assets/Resources/Art` (see-through background kept, no compression), `GameInput.cs` reads the keyboard, `CameraFollow.cs` moves the camera, and `Assets/Editor/ProjectSetup.cs` creates the scene if it is missing.

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
