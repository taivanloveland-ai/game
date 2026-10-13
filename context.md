# Game context

Running record of what Taivan wants for the game, what has been built, and what is still undecided. Updated as work goes on.

## Roles

- Taivan directs the design. Claude writes the code.
- The game must be different from his brother's game. His brother is also building a 2D side-scroller, and Taivan does not want to copy it.

## How work is delivered (asked 2026-10-09 and 2026-10-10)

- Each request gets its own git branch, made away from the folder Unity has open, so his game does not change while Claude is still working.
- When a request is done and checked, the branch is merged into `main`, `main` is pushed to GitHub, and the new version is loaded into his open Unity editor.
- **Never interrupt his game** (asked 2026-10-11: "whenever youre working on something, and im playing, the came crashes. make it so it doesnt crash, and if i want the update, i stop and press play again."). While the game is playing, the editor now picks up no changed files at all, and Unity is set to wait until play stops before recompiling. When he stops, everything that changed loads, and pressing Play again plays the new version. Claude also no longer brings the Unity window to the front. (`Assets/Editor/ExternalRefresh.cs`.)
- **Full screen** (asked 2026-10-11: "make it fullscreen"): pressing Play in the editor now makes the Game view fill the whole Unity window (Unity's "Play Maximized"), so with the Unity window maximised the game fills the screen. A built copy of the game was already set to open full screen. (`Assets/Editor/PlayMaximized.cs`.)

## Taivan's shorthand

- **sc** = the side-scroller view (mining mode on planet 1).
- **td** = the top-down view.
- **poup** = "please open up project": open the game project in the Unity editor (added 2026-10-11).

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
- If you mine down, you can see the blocks behind you. (Mining was added on 2026-10-10; see "Mining" below.)
- In mining mode you can jump **1.5 blocks**. The grey rocks are **2 blocks tall**, so you cannot jump over them.
- Taivan wants to be given options when there are design choices to make. His picks for mining mode:
  - **Underground:** 4 or 5 layers of snow, with stone under the snow and ice for now. Where there is ice, it should look like a river or lake that was there and froze.
  - **Behind you:** the next few rows further back are drawn faded behind your row.
    - On 2026-10-10 he asked that in sc the back blocks not be "stark lines" but a gradient, so it looks aesthetically pleasing. Built as 10 rows behind (was 3), each only a sliver higher than the one in front and a little dimmer and bluer, so they blend into a smooth fade from near-white to slate blue. The exact colours and the number of rows were Claude's choice.
  - **Surface:** flat ground. Snow and ice are at the same height, with rocks standing 2 tall on top.

## Column view with G (asked 2026-10-10)

- He asked: "if you press g, instead of switching to sc row, its sc column that looks the same but is directionally different." So **G** opens the side-scroller along the **column** of blocks the player is on (north to south), where **R** opens it along the row (west to east). It looks and plays the same: walking, jumping, W and S, mining, collecting.
- His picks:
  - **Facing:** north is on the right. You look west, so D walks north and A walks south. W steps into the column behind (one square west), S into the column in front (one square east).
  - **Switching:** R always means the row view and G the column view. Pressing the other key while in sc turns the view 90 degrees on the spot. Pressing the key of the view you are in goes back to td.
  - **Sky:** his own answer: in the column view "you cannot see the sun or black hole and the day continues". Stars still show at night and the sky still changes colour; keeping the stars was Claude's reading, not confirmed.

    - **Changed later on 2026-10-10:** "in the sc mode instead of seeing the sun and black hole on the north/south, see them on the east/west." Claude read this as swapping the two views: the sun and black hole now show in the column view (G, looking west) and are out of sight in the row view (R, looking north). Not confirmed; it could also have meant something about where they rise and set.

## Black hole picture, obstacles, held mining (asked 2026-10-10)

- **Black hole:** he gave a reference picture (a black disc with a bright ring, a long tilted streak of glowing matter across the front, a thick bright arc over the top and a thinner ring below, in oranges and yellows) and asked for it to be "as pixelated as the snow". The game now paints its own black hole after that picture at 128 pixels per block. The reference picture itself is not in the game.
- **Ferns and ice spikes are not walkable.** Each one blocks its whole square, one block tall, in td and sc. You walk round it or jump onto or over it (Claude's choice that jumping works). Nothing grows on the squares right round the start, so the player cannot begin stuck.
- **Hold the mouse to mine**, "but make sure its not too quick": holding the left button hits once every 0.4 seconds (Claude's number), so snow takes 0.4 s a block, ice 0.8 s and stone 2 s. Single clicks still hit as fast as you click.

- **Black hole turns through the night** (asked later on 2026-10-10): "make the black hole rotate throuout the night realistically so its level at midnight." Its disc now stays lined up with its path across the sky: tipped steeply up as it rises, level at midnight, tipped down as it sets.

  - **Changed again on 2026-10-10:** "instead of doors, make it a hole that you naturally make." His pick when asked: **show the real hole**. There is no special doorway drawing now. In td a wall's south face shows exactly the blocks that are there, and wherever a block is missing that exact space is see-through, whatever the shape (doors, windows, anything), with the floor inside showing through. Holes in the west, east and north sides still cannot be seen from above, so they show as a piece missing from the wall, as long as the wall carries on within 3 squares either side (so a roof edge that just sticks out is left alone; that rule is Claude's).
- **Stacks show each block** (asked 2026-10-10): "if i place snow on a stack of stone, the wholde stack turns into snow. whatever material is on top is the one that shows up ant i dont like it. fix it." In td the side of a stack was painted all in the top block's material. Now each block of the side is drawn as its own material; only the top face shows the top block. A tall wall's side is also drawn all the way down now (it used to stop after 4 blocks).
- **Mining in td** (asked later on 2026-10-10: "make it so you can mine in td"): with the pickaxe equipped, left clicking a square in td hits the top block of that square. Same reach (3 blocks), same hits (snow 1, ice 2, stone 5), and holding the button works too. Mining the top block of the square was Claude's choice, to mirror how building in td stacks on top.

- **Chunkier black hole, Terraria-style path** (asked later on 2026-10-10): "the black hole needs to be a bit more pixelated and the curve of its path needs to be like terraria." The black hole is now painted at 24 pixels per block with hard-edged pixels (it was 128 and smooth; 24 was Claude's pick for "a bit"). The sun and black hole both now cross the sky the way Terraria's sun and moon do: steadily from just past the left edge to just past the right, along a low, wide parabola that peaks mid-screen. The black hole still tips with its path and is level at midnight.

## Ice axe (asked 2026-10-10)

- He asked: "ice axe can be made out of 15 ice shards and 10 ferns it looks kinda like thors axe". It is a second recipe at the ship's workbench, beside the pickaxe. He then corrected the amounts ("for the axe fiddleheadx15 and shardx10"): it takes **15 ferns and 10 ice shards**. Its picture is shaped after Thor's axe: a long handle with a heavy ice head that is a broad curved blade on one side and a blunt hammer on the other.
- His picks:
  - **Use:** his own answer: "damages spiders and you are now able to cut down the thornam bushes." With the axe equipped, left click a spider to hit it. Thornam bushes can now only be collected with the axe equipped; without it you are told you need one.
  - **Fighting:** 5 hits to kill. Spiders have 50 health and the axe does 10. It reaches 2.2 blocks, swings about twice a second if the button is held, flashes the spider red and knocks it back half a block (reach, pace and knock-back are Claude's numbers).
  - **Drops:** his own answer: "silk and legs and fang". A dead spider leaves 1 spider silk, 2 spider legs and 1 spider fang (the counts are Claude's reading). Nothing uses those three items yet; their pictures are Claude's.
- Dead spiders do not come back.

## Health, spider damage and reflective ice (asked 2026-10-10)

- He asked: "make ice reflective and make spiders do damage and render correctly. they glitch behind some blocks."
- **Health.** His picks:
  - **Display:** a health bar (top left, under the line of text), 100 health.
  - **Bite:** his own answer: "8 damage per bite". A spider bites every 0.8 seconds, and several can bite at once.
  - **Death:** his own answer: "wake up at the ship, drop 1/2 a random stack of items on the ground where you died". The screen fades to black, you wake beside the ship with full health, and half of one randomly picked stack (rounded up) is left as one pile where you fell; walking back to it picks it all up. Tools are never dropped.
  - **Healing:** his own answer: "after a while you gain 5 health every second". "A while" is 5 seconds without being hurt (Claude's number).
  - Claude's additions: a brief red flash when hurt, the bar turns orange under 30 health, nothing can hurt you for 3 seconds after waking, and spiders lose interest once you have fallen.
- **Spiders drawn behind blocks (fixed).** In td a spider lies flat and reaches over the squares south of it, and those squares of flat ground were being drawn over its lower half. It now counts as being at its own southern edge when things are put in order, so the ground it lies on never covers it; only ground to the south that stands higher than it still does.
- **Reflective ice (td only).** The top of the ice is slightly see-through with a solid copy of the ice beneath, and reflections are drawn between the two, so they show on ice and nowhere else. The player, ferns, ice shards and thornam bushes are reflected upside down beneath their feet, squashed to 70% for the slanted view; a jumping player's reflection drops away below them. Spiders lie flat, so they have no reflection. Nothing changes in sc. (How the reflection works and looks was Claude's choice; he only said "make ice reflective".)

## Thornam bushes (asked 2026-10-10)

- He drew a bush (a tangle of teal vines with small olive thorns on a short brown trunk) and asked: "add these as thornam bushes that drop 3 thorns and 3 thornam bush vines and 1 thornam stick." His picture is used exactly as drawn, as `Assets/Resources/Art/ThornamBush.png`.
- His picks:
  - **Where:** his own answer: "pretty rare next to ice". They grow only on snow squares that are right next to ice.
  - **How many:** his own answer: "1 per 13", read as 1 bush for every 13 ferns. That is about 320 to 370 on the planet.
  - **Size:** player height (0.9 blocks tall; the picture is a little wider than a block).
- As built: left click collects one like a fern (you walk over if it is further off). It gives 3 thorns, 3 thornam bush vines and 1 thornam stick, which scatter a little and slide to you. A bush blocks its square like ferns and shards do, and does not grow back. The three items' pictures are Claude's, painted at the fine pixel scale in the bush's own colours. Nothing uses the items yet.

## Ice spiders (asked 2026-10-10)

- He drew a pale ice-blue spider seen from above (round body, small dark face mark, eight bent legs) and asked: "can you fix this up and make it look real good like a spider and add animation of it walking and make it fit in td and sc and symetrical". His drawing was the reference; the game paints its own spider after it at the fine pixel scale (128 per block), mirrored so both sides match. His picture file is not used in the game.
- His picks:
  - **Behaviour:** his own answer: "chases you also add biting animations." It wanders until you come within 7 squares, then chases you (giving up past 11), and bites over and over when it reaches you. A bite does nothing yet because there is no health.
  - **Where:** on the ice. They stay on the ice and will not follow you onto snow.
  - **Size:** about a block wide, legs and all. Later on 2026-10-10 he asked to "make the spider a bit bigger": it is now drawn 1.3 times that size, about 1.3 blocks across (1.3 was Claude's pick for "a bit").
- As built: about 400 to 450 on the planet (0.12% of ice squares; Claude's number), none within 14 squares of the start. They move at 2.4 squares a second chasing and 0.9 wandering (the player's normal speed is 3). In td they are seen from above and turn to face where they are going; in sc they are seen from the side and only the ones in the row or column on screen show. You can walk through them for now (Claude's choice). 8 walking pictures and 5 biting pictures for each view.
- **Spider hidden behind the player** (he sent a screenshot on 2026-10-10: "fix this problem"). In td a spider coming from the north walked up to the spot under the player's feet, so the player's picture, which stands up the screen from there, covered half of it. Now in td the spider goes for the edge of the player's picture and stops with its fangs just short of it, on whichever side it comes from, and it backs away if the player walks into it. (Claude read the screenshot this way; he did not say what the problem was.)

## Building and houses (asked 2026-10-10)

- **Longer hotbar** (asked later on 2026-10-10): the hotbar is 10 slots (keys 1 to 9, and 0 for the tenth), and the mouse wheel scrolls along it, going round at the ends. A block put in a slot keeps that slot when you run out: it is drawn faint with a 0, and the next ones you get are in the same slot, until you drag something else there or drag it off. His words: "if you put an item in a slot and run out then get the item again, teh item goes into that slot until it is switched with something."
- **Doorways must show in td** (asked later on 2026-10-10): he made a doorway in sc and in td "it looks like a wall but i can walk through it. thats a problem." Now a gap under the top of a wall shows in td: a dark opening in the wall's south face, or a dark notch cut into the west, east or north edge of its top when the way in is from that side. Dug-out cave mouths show the same way.
  - **Changed later on 2026-10-10:** "i want to be able to see through the door and it not be two black rectangles." Now a doorway in a south-facing wall is see-through: the floor inside shows through the opening, a little shaded. A doorway facing west, east or north is drawn as a gap cut out of the wall down to its floor, since the side of a wall cannot be seen from above. No more dark rectangles.

- He asked: "building. you can place the blocks if you drag them from your inventory to your hotbar. also make it so if someones building a house, when youre in it the whole roof is transparent and when youre out its opaque."
- **Hotbar:** blocks (snow, ice, stone) are dragged from the Items panel (Tab) onto a hotbar slot. The slot shows how many are left and empties when they run out. Slots can be dragged onto each other to swap; dragging a block off the bar takes it off (you still carry it). Tools cannot be pushed off the bar. Only blocks can go on the bar (Claude's choice).
- His picks:
  - **Placing:** left click with a block equipped. (Left click also still mines with the pickaxe equipped and collects ferns and shards.)
  - **Views:** you can build in sc and in td. In sc the block goes in the empty space you click. In td it is stacked on top of the square you click.
  - **Rules:** within 3 blocks of you, in an empty space touching another block or the ground, and not where you are standing.
  - **Roof:** his own answer: "when the player is covered and theres walls or if the roof is a square or rectangle, and its obviously a covering, make the whole thing transparent. but if there are different rooms, only make the room youre in transparent."
- How the roof rule is built (Claude's reading): in td, when you are under a block, the roof over every covered square you could walk to without leaving cover goes see-through, walls or no walls. A gap one square wide between walls counts as a doorway, and the room stops there, so the next room keeps its roof. The walls round the room fade as well and leave a darker footprint, so the room reads like a floor plan. Tunnels you dig work the same way.
- Blocks can be placed up to 10 blocks above the lowest ground (Claude's number). Placed blocks can be mined again and give the block back.

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
  - **Day length:** 2 minutes for a full day and night at first, for testing. On 2026-10-10 he said "day is 6 minutes": a full day and night now takes 6 minutes, 3 of day and 3 of night (Claude's reading; he may have meant 6 minutes of daylight).
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

- **Plants are just green rectangles.** Later the same day he changed this: the plant is a **bluish green fiddlehead fern** (a stalk with a curled spiral tip), still half the player's height, named "Fiddlehead fern". He then drew his own fern (a green spiral on a stem) and asked for it to be fixed up a lot so it looks professional and matches the ice shard's style. Claude redrew it from his sketch as a smooth teal fiddlehead with a dark edge and a lit and a shaded side, stored at `Assets/Resources/Art/Fern.png`. On 2026-10-10 he asked for it to be just as pixelated as the ice shard, so it was redrawn with hard pixel edges, four flat colours, and the same number of picture pixels per block as his shard picture (41 by 71 pixels). Later that day he asked for it to be **bigger and less ugly around the edges**: it now stands three quarters of the player's height (this replaces the original "50% of the player's height"), drawn at 61 by 108 pixels with a thinner, slightly lighter edge and smoother pixel outlines. It is used for the standing plant, the dropped item and the inventory (the standing-plant part is Claude's choice, not confirmed). The fern changed again after this: see "Art frustration and fixes" below for how it looks now.
- **Sharp shards of ice**, found on the ice, are picked up like the plants. (He first said thin sharp rocks, then changed it to ice shards.) They are **long, as though they are the pick on a pickaxe**, and they **look like part of the ice** (same blues as the ice, no dark outline). **After you right click one, it looks like the picture Taivan supplied** (a blue diagonal shard), stored at `Assets/Resources/Art/IceShardItem.png`. This is the first real art in the game. He replaced the first picture with a paler one, then asked for it to stand out a bit more but look about the same, so Claude added a thin darker blue edge to his picture. He said it looked the same and asked for the item's colours to stand out more on the ground, so it is now his shape with a pale, near-white blue inside and a dark navy edge. His untouched picture is in git history (commit c268eaa). He later asked for the outline to be taken away, so it is now his shape in pale blue with no edge.
- **Right clicking** a plant or shard makes you **move until you are close enough to collect it**. If it is unavailable, right clicking does nothing.
- For the walk he first picked "finds a way and jumps", then changed it: **just walk in a straight line**. It still jumps onto raised ground by itself (Claude kept that; not confirmed).
- **W and S in sc get an animation**, so changing rows is not so much faster than walking in. He first asked for "like a fade"; Claude made the whole screen fade to black, and he changed it: instead of the whole screen blacking out, **the blocks shift forward, get brighter, and become the front blocks**.

## Randomized world (asked 2026-10-10)

- **The world is randomized:** planet 1 is built from a new random seed every time the game starts, so the snow, ice, rocks, raised ground, ferns and shards are in different places each play. The ship and the open crash site are always in the middle.
- As built: a random world is re-picked (up to 20 times) if less than 80% of its snow and ice can be walked to from the crash site. The seed is printed in Unity's Console. On the Planet One object, unticking **Random World** and typing a **Seed** rebuilds that exact world.
- Nothing is saved between plays yet, so a new world also means an empty inventory.

## Temporary settings to undo later

- Nothing temporary right now. **Undone on 2026-10-12** ("undo temporaries", the second time that day): no starting pickaxe or blocks, walking speed **4**, and no test cave by the ship (`caveByCrashSite` in `PlanetOne.cs` is off).
- **Day length (asked 2026-10-10, not temporary):** "days are 24 minutes 12 day 12 night". `cycleSeconds` in `DayNight.cs` is 1440. Before this it was 6 minutes, then 1 minute for testing.

## Mining (asked 2026-10-10)

- **Mining!** The pickaxe can mine **snow, ice or stone**.
- His picks from the options:
  - **How:** left click to mine. Snow takes 1 hit, ice takes 2, stone takes 5.
  - **What you get:** a block item. Each mined block drops as a snow, ice or stone item that slides to you and stacks in the Items panel.
  - **Grey rocks:** yes, they are stone. The pickaxe mines them (5 hits) and they give stone.
  - **Reach:** 3 blocks.
- **A light blue highlight on the pickaxe when equipped** (asked 2026-10-10). As built: the hotbar has an equipped slot, shown with a light blue glow and frame. Number keys 1 to 5 equip a slot, and pressing the equipped slot's number again puts the tool away. Slot 1 is equipped to begin with. Mining needs the pickaxe equipped.
- As built: mining is done in sc only, and needs an ice pickaxe equipped. Cracks spread across a block as you hit it; hitting a different block starts the count again. Mined blocks stay gone for the rest of the play, in every row and both views. The ship and the very bottom layer of the ground cannot be mined.
- Through a hole you see the dimmer blocks of the row behind, and light reaches 1.5 blocks into the ground around it, as he asked on 2026-10-09.
- In td, a spot whose top block was mined is drawn lower, with a cliff face on the square north of it, and you can fall in. You can jump out of a hole 1 deep.
- **Switching to td keeps you in the hole** (asked 2026-10-10: "if you dig a hole then switch to td, it teleports you to the surface. fix it"). The top-down view now moves the player through the same blocks as the side-scroller, so you stay at your own height in a hole or in a tunnel with ground overhead, can walk along dug-out tunnels, bump your head on their roofs, and fall into holes. Down a hole the player is drawn at most one block below the ground and darker the deeper they are, so they stay visible.
- **If there is a block above the character, that block is transparent** (asked 2026-10-10). As built, in td: when the player is under a roof, the ground over them and over the open tunnel within 2 squares of them is drawn see-through, showing the darkened tunnel floor beneath and the player on their own square. Solid ground beside the tunnel stays as it is, so the shape of the tunnel shows. Claude took this to be about td, since in sc the player is always in view; not confirmed.
- If you mine the block a fern or ice shard stands on, it comes loose as if you had collected it.
- W and S row stepping now checks the actual blocks, so underground you can only step into a row that has been dug out where you are.

## Bigger planet that loops (asked 2026-10-10)

- **The map is 6 times larger,** and you can **walk up from your ship and make a full loop around the planet, as though it were a sphere.** On the map, if you get to the top and keep going up, you come in at the bottom.
- As built: the planet is 960 by 960 squares (6 times wider and 6 times taller than before; Claude's reading of "6x larger", not confirmed). It has no rock wall and no edges: it joins up with itself both ways, so going far enough north, south, east or west brings you back to the ship. A full loop is about three and a half minutes of walking.
- **"make 5 shards for every 8 ferns"** (asked 2026-10-10): the whole planet now holds exactly 5 ice shards for every 8 ferns, the amounts one pickaxe takes. Things are scattered as before (ferns on 0.4% of snow, shards on 0.9% of ice), then whichever there is too much of is thinned out at random. A test world had 4,776 ferns and 2,985 shards (597 sets). The ratio is across the planet, not in any one area.
- **Mountains up north** (asked 2026-10-10: "make there be a lot of high mountains up north"). His picks:
  - **Where:** far north only. A range on the far side of the planet from the ship, running east to west, about 720 squares (4 minutes' walk) away going either north or south. It is about 280 squares deep.
  - **Height:** up to 14 blocks, climbable. Every step on a mountain is exactly one block, so any peak can be climbed by jumping up it.
  - **Material:** grey rock (mines as stone) with a block of snow on top of anything 9 or more blocks tall.
  - As built: about a third of the range is mountain 4 or more blocks tall, with valleys of ordinary snow and ice between. Nothing grows on the mountains. The build limit went up from 10 to 20 so you can build on the peaks, and in td the camera now rises with the ground you stand on so you stay in view up high.
  - **Changed later on 2026-10-10:** "the mountains get gradually steeper." Slopes now start gentle and steepen toward the peak: the first step is 4 squares wide, the next three are 3 wide, then four steps 2 wide, and the last six blocks rise one block every square. Each step is still only one block high, so everything stays climbable (Claude kept that from his earlier pick). The mountains are broader to fit the longer slopes.
  - **Changed again on 2026-10-11:** "make the northern mountans much steeper closer to the top and heigher to go with that." Peaks now reach **24 blocks** (from 14). The foot is as before (4 squares a block, then 3, then 2), then a block a square up to 14, then **two blocks a square** up to 20 and **three blocks a square** to the peak. The steep tops cannot be climbed by jumping any more (a step of 2 or 3 is higher than the 1.5 block jump); you would have to dig or build steps (Claude's note; he did not say whether they should stay climbable). Snow caps start at 16. The build limit went up to 30 and more land is drawn above and below the player in td, so high peaks stay on screen.
  - **Changed again on 2026-10-11:** "make the mountain range cover the whole top part". The range now covers the whole northern part of the map: from about 100 squares north of the ship (under half a minute's walk) all the way up to the top edge of the world map, fading in over the first 40 squares and out over the last 40 before the edge, where the map joins its bottom edge. The land south of the ship (the bottom half of the map) has no mountains now. About half of the northern part is mountain 4 or more blocks tall, with snowy and icy valleys between.
  - **Changed again on 2026-10-11:** "make the mountains not start so close to the spawn. make them only in the top 1/4 of the map and make them so steep that theyre not climable without a pickaxe. like 3-5 blocks tall at the base and 2-4 blocks tall steps up to the to." Now the range is only in the **top quarter of the map** (from three quarters of the way up to the top edge, about 720 squares north of the ship at its nearest). Mountains rise in sheer terraces: a cliff **3 or 5 blocks** tall at the foot, then flat ledges each **2 or 4 blocks** above the last, up to peaks of **25**. A jump only reaches 1.5 blocks, so they cannot be climbed without digging steps with the pickaxe (or building them). Claude used only 3 or 5 and 2 or 4 (never 4 at the foot or 3 for a step) so every ledge is an odd height and two ledges side by side are always at least 2 blocks apart; with 4s and 3s mixed in, some neighbouring ledges came out 1 block apart and could be jumped. The gentle slopes from before are gone.
  - **Changed again on 2026-10-11, to look like the Himalayas:** "they need a little ramp up to the steepness. also they need to be not so flat and have a pointy top. Im planning on putting caves in the mountains so make good looking spots for them but were not starting with the caves yet. also allow some way to get up to a ledge sometimes. the mountains im going for the himalayas". His picks: peaks **about 40 blocks** tall, and for ways up: "not trails, but sometimes there happen to be a way just to the next ledge". As built: foothills ramp up a block every two squares (walkable with jumps) to about 6; above that the side rises a block a square, but between 8 and 20 it is cut into **flat ledges 4 blocks apart with sheer cliff faces** (the spots meant for cave mouths later); above 20 the top rises two or three blocks a square to a **pointed peak**, with peaks lined up along ridges. In small scattered patches the ledges are not cut, so the slope there happens to lead up to the next ledge. Snow caps start at 26. Still only in the top quarter of the map. The build limit went up to 48, and more is drawn above the player in td and sc so the peaks fit.
  - **Reworked again on 2026-10-11 from his pictures** ("use these as ideas for what the mountains should be like"): a sketch of one mountain (a long, gentle rise that gets steeper and steeper up to a narrow, pointed spike) and a photo of snowy Himalayan peaks above a rocky valley. Now each mountain rises from the valleys around it with that steepening profile: 3 squares a block at the foot, then 2, then 1, then 2, 3 and finally 6 blocks a square up a narrow spire. Peaks stand in rows along ridges, with saddles and snow and ice valleys between them, up to about 38 to 40 blocks. Snow tops everything from 22 blocks up. The ledges with cliff faces for caves are now between 10 and 22 blocks. Still only in the top quarter of the map.
  - **Changed again on 2026-10-11:** "mountains are smaller and steeper" (Claude read this as a request). Peaks now reach about **30 blocks** (from 40), mountains and the gaps between peaks are about a third narrower, and every part of the slope is steeper: 2 squares a block at the foot, then 1, then two blocks every three squares, then two, three and six blocks a square up the spire. Ledges for caves are between 8 and 20 blocks, and snow starts at 18.
- **"widen pull"** (asked 2026-10-10): a collected fern or shard now slides to the player from up to 3 blocks away (it was 2), so one collected from a block above or below no longer gets left on the ground.
- **"1.5x the size of the map"** (asked 2026-10-10): the planet is now 1440 by 1440 squares, one and a half times as wide and as tall as the 960 it was (Claude read "the map" as the planet, like the "6x larger" request, and 1.5x as each side; not confirmed). A full loop is now about 8 minutes of walking at the current speed. There are about 4,800 ferns and 4,200 ice shards on it.
- **"map size x2"** (asked 2026-10-11): the planet is now **2880 by 2880 squares**, twice as wide and twice as tall as the 1440 it was (Claude read "x2" per side, like the earlier "1.5x"; that is four times the area). A full loop is about 12 minutes of walking at 4 squares a second. A test world had about 14,000 ferns and 8,000 ice shards. Building the land was made to use all the computer's cores, so a new world still takes about 3 seconds to make.
- To make this work, only the land around the player is drawn (a little more than the screen shows), in both views, and the land's shapes are made so they meet without a seam at the join.
- The crafting nook, the mini map's ship marker (which points the shortest way home) and the world map all follow the loop. Ferns and shards kept the same rarity per square, so the bigger planet has about 2,100 ferns and 1,600 shards.

## A round world with poles, like the Earth (asked 2026-10-12)
- His words: "make  the world like the earth where on the maps its like a rectangly oval horizontal so when you get to the north pole, you come down at a different spot. for example when you go up from canada you end up near russia if you go in a straight line. i want to incorparate north and south poles."
- His picks: the map is **twice as wide as it is tall**: the planet is now **5760 by 2880 squares** (east to west doubled, north to south kept). Over a pole: **"Real compass, no flip"**: walking north over the North Pole brings you down the far side of the planet, half way round the map, now heading south, and north stays up the screen. To keep going in a straight line you switch from W to S. Holding W just takes you back over the pole. The South Pole works the same way.
- Poles, in his words: "the mountains surround a flat somewhat circle of ice. the mountains are a circle around it." Each pole is in the middle of a flat sheet of ice reaching about 120 squares from the pole (90 to 150, with a wavy edge), with a ring of the Himalaya-style mountains about 300 squares deep round it. On the flat map they show as a strip of ice along the top and bottom edges with a band of mountains next to each, the way Antarctica stretches along the bottom of a world map. The mountains have gaps and valleys to walk through to the ice. They used to be in the top quarter of the map only.
- Start, in his words: "you start in the southern hemisphere , right between the equator and the south pole". The ship is now half way between the equator and the South Pole, about 300 squares north of the southern mountain ring.
- Near a pole you can see across it: the land past the top of the screen is the far side of the planet, upside down. The mini map and world map stop at the poles and only wrap east to west. The world map is twice as wide as it is tall.
- A new world now takes about 8 seconds to make, since it is twice the size.

## Maps and rarer materials (asked 2026-10-10)

- **Materials are even more uncommon, especially the ferns.** Ferns are now on 0.4% of snow squares (about 50 to 80 in a world, down from about 190) and ice shards on 0.9% of ice squares (about 20 to 50, down from about 60).
- **A mini map in the top right corner,** and **pressing M opens a world map of what has been on your mini map.**
- His picks from the options:
  - **What the maps show:** the land, you and the ship. Ferns and ice shards are not shown.
  - **Mini map range:** about 40 squares across.
- As built: both maps are top-down in either view. The mini map keeps you in the middle; the ship's marker (yellow) sticks to the mini map's edge when the ship is out of range, pointing the way home. The world map is dark where you have not been. M or Escape closes it. Like everything else, what you have explored is not saved between plays.

## Crafting nook (asked 2026-10-10)

- A **little nook in the spaceship with a hanging hammer and wrench**, where you can craft with **8 ferns and 5 ice shards**.
- **Ice shards and ferns are each half as common** as before (now about 190 ferns and 60 shards).
- His picks from the options:
  - **What it makes:** an ice pickaxe, the first tool. It goes in the tool hotbar.
  - **Using it:** right click opens a menu. Right clicking the nook walks you over and opens a small crafting panel with the recipe, what you have, and a Craft button.
- As built: the nook is a framed dark recess in the middle of the ship's front, top-down only (it is not shown or usable in sc). The panel is titled "Ship workbench". It closes with its X, with Escape, or when you walk away. Crafting can be repeated; each pickaxe takes the next hotbar slot. The pickaxe is needed for mining.

## Taivan's own art (in `Assets/Resources/Art`)

- `IceShardItem.png`: the loose ice shard you pick up and carry. On 2026-10-11 he sent a new picture, "ice shards" (several small pale-blue shards scattered together), which replaced the old single shard. It is what a collected ice shard looks like on the ground, in the Items panel and at the workbench. Used exactly as drawn. (Claude read "ice shards" as this item picture, since the shard standing on the ice is now his icicle.)
- `Icicle.png`: supplied 2026-10-11 "for the icicle": a pale blue cone. It is now the ice shard standing on the ice (the loose shard you pick up is still `IceShardItem.png`). Used exactly as drawn, except that its top row is left out because it has a stray mark in one corner. Its main colour is exactly the ice colour (`#92CAE8`), so on the ice the cone almost disappears; He then asked: "can you make the icicles taller and look different than the ice?" and "still make the icicle look like its natural". So in the game the icicle is stretched to 1.6 times as tall (about 1.25 blocks; the 1.6 is Claude's pick) and recoloured to look like a real icicle: frosted white where it meets the ground, a clear, slightly see-through pale blue up its length, nearly white at the tip (it had faint rings at first, which he saw as horizontal stripes and asked to be fixed, so they were taken out), with his pale stripe kept as a bright glint and his shaded side as a deeper blue. The shadow that was briefly added is no longer used. His file itself is unchanged.
- `Fern.png`: the fiddlehead fern, redrawn by Claude from his sketch (see above).
- `SnowBlock.png`: a white square with faint grey streaks, supplied 2026-10-10 "for the snow blocks". It is used for the top of snow tiles in td and for snow blocks in sc (both views confirmed 2026-10-10: "make the snow in the top down too"). It is shown exactly as he drew it: one picture per block, same way up, same colours. Claude's first version tinted and flipped it, and through an import problem it did not show in td at all; he objected ("the snow isnt what i made!"). Lesson: use his art unaltered unless he asks for changes.
- `SnowBlock2.png`: a second snow picture, white with a few longer grey streaks, supplied 2026-10-11: "make it random between this sprite, the other sprite, and just a white block." He sent a redone version the same day ("redo new snow sprite"), which replaced the first.
- `SnowBlock3.png`: a third snow picture, supplied 2026-10-11: "replace with pure snow". It takes the place of the plain white look, so snow now picks at random between his three pictures, each about a third of the time, in td and sc. Plain white only comes back if this picture is missing. The picture had one see-through pixel in it; the game fills any see-through pixel in a snow picture with white (Claude's fix, the file itself is unchanged). Every block of snow now picks one of three looks at random (the same block always keeps its look): `SnowBlock.png`, `SnowBlock2.png`, or plain white, each about a third of the time. This is in td and sc. Both pictures are used exactly as drawn. ThornamBush.png: his thornam bush (see that section).
- `IceBlock.png` (no longer in the folder or the game): a bright blue square with pale diagonal streaks, supplied 2026-10-10 as "ice for sc and td". It was used like the snow picture for a short while, then he called it ugly and asked for the ice blocks to look good, so the game draws the ice itself now (see below). The picture is in git history.
- Art sent so far is 128 by 128 pixels with hard pixel edges.

### Art frustration and fixes (2026-10-10)

- He said he was frustrated: the fern did not look like it belonged, and the ice shard sprite, the spike and the ice block sprite were ugly. Claude's diagnosis was that the pieces were made separately in clashing styles (different pixel sizes, outlines and shading on some things only, pictures repeating in every square, bright colours next to muted ones).
- He then asked for three fixes, plus taller ferns:
  - **Fern looks like it is actually growing there:** it now comes up out of a small mound of snow, casts a soft shadow on the ground, has a young frond beside the tall one, and its edge is a soft teal instead of near-black. The tall frond is about the player's height.
  - **No outline on the loose ice shard:** it is his shape in a pale blue with no edge.
  - **Ice blocks look good:** his `IceBlock.png` is no longer used (it is in git history). Ice is drawn by the game as a clear blue with a few short pale glints and the odd crack, picked at random per square so nothing repeats; raised ice is paler. In sc, ice goes from that blue to a deeper blue with depth.
- The standing ice spike, which he had also called ugly, was redrawn at the fine pixel scale after he asked for "the ice spikes also": a smooth, gently curved spike about three quarters of a block tall, lit on the left, deeper blue on the right.
- **Pixel size to match:** he asked for the pickaxe and ice blocks to be "as pixely as the snow". His snow picture is 128 pixels across one block, so game-drawn art that sits beside his pictures is painted at that fine scale: the ice's glints and cracks are thin 3-pixel lines, and the ice pickaxe is a 128-pixel picture. Use this scale for new art unless he says otherwise.

## Look and feel decisions

- **Engine:** Unity (decided 2026-10-09).
- **Art for now:** the character is just a rectangle. Rocks, cliff faces and the ship are still simple coloured shapes. Snow, ice, ferns, ice shards and the pickaxe now have real art (see "Taivan's own art").
- **Top-down view is angled,** so you can see the edges of the cliffs (asked 2026-10-09).
- **Ice is a little slippery** (asked 2026-10-09). Made **a bit more slippery, and a bit faster to move on**, on 2026-10-11 ("make ice a bit more slippery and movement speed a bit faster on ice"): you speed up, turn and stop a third more slowly than before (grip 8, was 12), and your top speed on ice is 1.2 times your speed on dry ground (Claude's numbers for "a bit"). This applies in td and sc.
- **Walking in snow is a little slower,** only a little (asked 2026-10-09). Currently 90% of full speed; ice is full speed.
- **Player speed is 3/5 of the original** (asked 2026-10-10): 3 squares a second at a full walk, down from 5. Before settling on this he tried 5 times speed for a while to get around the big planet. He then asked for 4/5 and cancelled it ("nvm"), so it stays at 3/5.
- **Player speed is now 4/5 of the original, permanently** (asked 2026-10-11: "4/5 speed permanent"): 4 squares a second at a full walk, up from 3. (He had asked for 4/5 once before on 2026-10-10 and then said "nvm".)
- **Deeper blocks are gradually shaded in the side-scroller, and after the first 1.5 blocks it turns black** (asked 2026-10-09). This is for mining mode only, not the top-down view. It took three tries: Claude first shaded the low ground in top-down, then made a gentle shade that never reached black; Taivan corrected both.
- **No ore or crystals in the world for now.** Taivan asked for the placeholder ones to be taken away (2026-10-09). Reason not given.

## Built so far

(Summary as of 2026-10-11. The sections above have the details and his exact words.)

- Unity project at the repo root, for editor 6000.6.5f1. Open `Assets/Scenes/PlanetOne` and press Play. Nothing is saved between plays: each play is a new random world and an empty inventory.
- **The planet:** 5760 x 2880 squares, made at random each play, round like the Earth: it loops east to west, and walking over a pole brings you down the far side, half way round. Snow, slippery ice lakes with reflective ice in td, grey rocks, raised patches one block higher, and the crashed ship half way between the equator and the South Pole. The North Pole is a flat sheet of ice inside a ring of very steep Himalaya-like grey-rock mountains up to about 30 blocks tall, with snow caps. Six frost caves have their mouths dug into cliffs in those mountains, near the ice, on ledges that can only be reached by digging or building; each cave is a world of its own of stone tunnels and rooms. The South Pole is a snowy ice cap (snow over ice) surrounded by a freezing ocean about 150 squares wide that hurts to swim in. Music: his day and night themes, fading into each other at dusk and dawn.
- **Snow** uses his three snow pictures at random, block by block. **Ice** is drawn by the game in blues.
- **Views:** td is the main view. **F** switches to sc along the player's row, **R** to sc along the column (looking west, north on the right). (Until 2026-10-11 these were R and G: he asked "switch [R] with [F] and [G] with [R] in that order".) Ferns, icicles and thornam bushes standing on the rows behind are shown too, shaded like their row (asked the same day: "make plants and icicles visible in other layers in sc"). In sc, W and S step between rows; ten rows behind fade off as a gradient; underground is dark past 1.5 blocks from open air.
- **Moving:** WASD or arrows, 4 squares a second (a little slower on snow; slippery and a little faster on ice). Space jumps 1.5 blocks in both views. Ferns, icicles and bushes block their square.
- **Day and night:** 24 minutes, 12 of day and 12 of night. In the column view (R) the red sun and the black hole cross the sky on a Terraria-style arc; the black hole tips with its path and is level at midnight. The row view (F) looks north and shows only stars. Nights are tinted dark blue.
- **Things to collect** (left click; you walk over if they are further than a block): fiddlehead ferns on snow; ice shards, standing as his icicle (taller and recoloured to look natural) on ice, 5 for every 8 ferns; thornam bushes (his picture) on snow next to ice, 1 for every 13 ferns, which need the ice axe and drop 3 thorns, 3 thornam bush vines and 1 thornam stick.
- **Inventory:** Tab opens Items. The hotbar has 10 slots (1 to 9 and 0, or the mouse wheel); tools go there when made, and blocks can be dragged there to build with. A block that runs out keeps its slot.
- **Crafting** at the ship's workbench (right click the nook on the ship's front, td only): ice pickaxe from 8 ferns and 5 ice shards; ice axe (shaped like Thor's axe) from 15 ferns and 10 ice shards. The player starts with nothing.
- **Mining:** with the pickaxe, left click (or hold) a block within 3 blocks, in sc or td (td hits the top block of the square). Snow 1 hit, ice 2, stone and rock 5. Broken blocks drop as items.
- **Building:** with a block on the hotbar equipped, left click to place it, in sc or td, touching another block, within 3 blocks. Under a roof in td, the whole roof of the room you are in goes see-through; holes in walls show as real holes. Build limit 20 blocks.
- **Ice spiders** (his drawing, redone by Claude) live on the ice, about 450 of them. They chase you within 7 squares and bite for 8. With the ice axe they take 5 hits and leave spider silk, 2 spider legs and a spider fang.
- **Health:** 100, on a bar at the top left. After 5 seconds unhurt it comes back at 5 a second. At zero you wake at the ship and leave half of one random stack where you fell.
- **Maps:** a mini map in the top right; M opens a world map of everything seen. Scrolling over the mini map zooms it (10 to 400 squares across) and dragging it moves it; "Back to me" re-centres it (asked 2026-10-11: "you can zoom in with scrolling on your mini map and drag around"). Over the mini map the wheel zooms the map instead of moving along the hotbar. The first version did not work for him ("make the scroll map thing work"), so it was rebuilt to read the mouse wheel and button directly; the M world map now zooms (up to 16 times) and drags the same way, with a "Whole map" button to show all of it again.
- **Going underground in td** (asked 2026-10-11: "when you go underground in td, instead of just getting darker, go actually go down" and "when that happens, and you go down, the blocks covering you go transparent"): down a hole or tunnel the player is now drawn as deep as they really are (half a tile down the screen per block), no longer darkened, and the camera follows them down. The blocks drawn over the player turn see-through, with the land beneath shown cut through at the player's depth (open space as its floor, solid ground as a darker block). Tunnel floors are also drawn at their real depth now.
- **Not used yet:** thorns, thornam bush vines, thornam sticks, spider silk, legs and fangs.
- **Code:** `Assets/Scripts`. `PlanetOne.cs` builds the world and the td view and wires everything; `MiningMode.cs` is sc; `PlayerController.cs` movement; `DayNight.cs` the sky; `Gatherables.cs` ferns, shards and bushes; `Pickups.cs` loose items; `Inventory.cs` and `Hud.cs` the inventory and hotbar; `CraftingNook.cs` crafting; `Mining.cs` and `Building.cs`; `Spiders.cs`; `Health.cs`; `WorldMap.cs`; `GameInput.cs` all keys and mouse. His pictures are in `Assets/Resources/Art`.

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
- Where do the alien materials appear: inside the stone, in the ice, on the surface?
- If the suit keeps him warm, what is the danger on the cold planet? Can the suit be too weak for the coldest areas?
- What are the thorns, vines, sticks, spider silk, legs and fangs for?
- What does repairing the ship need?
- Does the ship need repairing or upgrading again on later planets?
- What makes each of the four planets different (temperature, materials, dangers)?
- Is planet 4 the innermost planet he was heading for, and the end of the game?
- Why was he going to the innermost planet?
- What kind of side-scroller are planets 3 and 4 (building, exploring, combat, jumping challenges)?
- Tone of the world (dangerous, mysterious, cozy, silly).
- Should the game be saved between plays, or start fresh each time?
- Where the finished game should run (Windows PC, browser, phone).

- **"my game is shaking for some reason"** (2026-10-12): after the planet doubled to 5760 x 2880, the map picture was sent to the graphics card whole (about 25 ms) every time the player stepped onto a new square, many times a second at the temporary 5x speed, so the game stuttered. Now only the newly seen squares are sent. If the shaking was at a pole instead: holding W there walks you over and back every step, as the 'real compass' choice works, which flips the view back and forth.

- **"when you can see most of the character, the transparency doesnt apply."** (2026-10-12): the blocks covering the player only turned see-through below ground. Now any block drawn over the player turns see-through, at any height: behind a wall, a mountain or a step up on the surface too, even when it only hides a little of them.

- **"so when i go to the north pole and go to where the two parts of the map where they connect and make the screen flip a lot and walk away the screen keeps shaking. fix it"** (2026-10-12): each trip over a pole moved the player's position half a planet further east and it never came back onto the map, so after many flips the numbers got so big the game lost exactness and everything shook. Now a pole crossing always lands back on the map, and anyone far round to the east or west is moved back a whole planet width (which looks exactly the same).

## Music (asked 2026-10-12)
- His words: "these are the night and day themes. put them into the game." (from his Downloads folder "hi"): **Planet 1 Day Theme.mp3** (about 8.5 minutes) and **Planet 1 Night Theme.mp3** (about 11 minutes), used exactly as he made them, in Assets/Resources/Music.
- His picks: **fade across at sunrise and sunset** (the day theme fades out as the night theme fades in, over 4 seconds, as the sky gets dark, and the other way at dawn), and **no mute key**. Each theme loops, and starts again from the beginning each time it comes back.

## Frost caves (asked 2026-10-12)
- His words: "okay. were putting caves in. make sure there is no possible way to get in without a pickaxe or building. So im thinking its a dark hole with icicles that when you walk into, the screen turns fades black then fades into the cave, which for now will be stone tunnels with no visible roof. the cave itself is technacally not part of the overworld, so any building/mining does not affect the overworld."
- His picks: mouths **high on mountain ledges**. "6 caves. make sure to make the mountains VERY steep". **Winding tunnels with rooms**. In caves, in his words: "td and sc work and the game itself zooms in 2x. the minimap goes dark. once you go in a cave, theres big text across the screen that says "frost cave" and it marks it on the map as frost cave".
- As built:
  - **6 caves**, 3 in the mountain ring round each pole, at least 900 squares apart. Each mouth is a dark arch with icicles in a cliff at least 3 blocks tall, above a ledge 12 to 22 blocks up.
  - Each mouth is checked when the world is made: no route of walking and jumping from the lowlands reaches it. A jump only climbs 1 block, but the check allows 2 to be safe, in case something like a fern is stood on. Only digging or building gets there.
  - Walking into the hole fades the screen to black (0.6 seconds) and then into the cave. You arrive in front of the hole in the cave's north wall; walking back into it takes you back out onto the ledge.
  - Inside: stone floor and rock walls 3 blocks tall (too tall to jump), with no roof drawn. Twisting tunnels 2 to 3 squares wide open into rooms now and then, with some dead ends, in an area 160 squares across.
  - Zoomed in twice as close, in both td and sc. The mini map goes dark. "FROST CAVE" comes up in big letters across the screen. The cave is marked "Frost Cave" on the mini map and world map from then on.
  - No sky, sun, black hole or night down there. No ferns, ice shards or spiders. The music carries on.
  - A cave is its own world: digging and building there does not change the land above. What you dig or build in a cave is still there when you come back. Items dropped in a cave stay in that cave.
  - Falling in a cave, you wake up at the ship as usual.
- **Mountains made VERY steep** (same request): a short ramp of a block a square at the foot, then 2 blocks a square, then 3, then from 14 blocks up 6 a square (sheer cliffs). Before, it was 2 squares a block at the foot, getting steeper.
- Not asked, Claude's choices (can change): the cave size, the 4-second name display, leaving by walking back into the same hole, and 3 caves per pole.


- **"cave name stays on map forever"** (2026-10-12): once a frost cave is found, its "Frost Cave" mark stays on the mini map and world map for the rest of the game. That is how it was built. The world is new each time Play is pressed, so marks do not carry over to the next play (there is no saving yet).

- **"make caves spawn very near the ice caps"** (2026-10-12): cave mouths are now on the inner side of the mountain rings, at most 70 squares out from the edge of a pole's ice sheet (in tests, 127 to 205 squares from the pole, with the ice edge at about 90 to 150). To fit all six that close, the least distance between caves went down from 900 to 500 squares.
- **"along with that, temporarily drop one right by the spawn"** (2026-10-12, TEMPORARY): a 7th cave mouth 7 squares east of the crash site, in a little rock cliff: a ledge 2 blocks up in front of a cliff 5 tall. Build one step to get onto the ledge and walk in. caveByCrashSite in PlanetOne.cs; turn it off to undo.

- **"make the cave entrances look like a hole with blocks"** (2026-10-12). Asked how, his answer: "not a doorway: a hole; a natural cave", then picked **into the cliff**. Now each mouth is really dug out of the cliff's blocks, in an uneven natural shape: 2 blocks tall (sometimes 3 in the middle, sometimes a block wider at one side), going 3 squares into the rock and narrowing, always under a roof of rock. Seen from above the opening is dark with icicles hanging from its top; from the side the dug-out blocks are simply missing. Walking 2 squares in goes through to the cave. The way out of a cave is a hole just like it, dug into the wall where you arrive. The old drawn arch is gone.

## South Pole ocean (asked 2026-10-12)
- His words: "i want snowy ice caps at the south pole with an ocean surrounding it"
- His picks: the **ocean replaces the southern mountains** (and their caves); the water is **freezing** (you can swim, but it slowly hurts you); about **150 squares wide**; the cap is **snow over ice**.
- As built: the South Pole's cap is flat, a block of snow on top with ice 4 blocks deep under it, and stone below that. About a fifth of it is patches of bare ice. Around it is the ocean, 150 squares wide (give or take 25, with a wavy far shore): dark cold blue water with ripples, 4 blocks deep, with stone under it, at the same height as the land. On the maps it is a dark blue band. You swim on top at half speed and can't jump. Every 1.5 seconds in the water costs 2 health, so crossing at the normal speed of 4 costs most of your health. "The water is freezing! Get out quickly." shows when you go in. Water cannot be mined, but blocks can be built on it as a bridge.
- All 6 frost caves are now in the northern mountains. They can now be 350 squares apart (it was 500) so all six fit near the ice. The North Pole is unchanged: a plain ice sheet in its ring of mountains.

- **Cave mouth shape from his sketch** (2026-10-12). He sent a tiny drawing of the opening. His words: "this is what i was thinking for the cave. 2 blocks wide at the top and 2x4 at the bottom and use the icicle sprite and make some smaller icicles too. the cave looks really inviting/ intimidationg." As built: every mouth (and the way out inside each cave) is now 4 blocks wide and 2 tall at the bottom with a part 2 wide and 1 tall on top in the middle, dug out of real blocks, nearly black inside. His icicle picture hangs upside down in it: a big one and two smaller ones on each of the two middle blocks, and smaller ones at the sides. Behind the opening the tunnel is 2 squares wide, then 1. To fit the shape, the ledge at each mouth is made 4 squares wide and the cliff behind is built up where it was too low; the walk-and-jump check is then run again and any mouth that could now be walked to is left out. Mouths are only put where nothing to the south stands in front of them on screen. Cave walls are now 4 blocks tall (they were 3) so the way out fits.
- **"undo temporaries"** (2026-10-12): off again. No starting pickaxe or blocks, walking speed 4, and no test cave by the ship.

- **"make it so when you harvest the ferns, only the big one drops."** (2026-10-12): his fern picture has a big fiddlehead, a small one, a patch of snow and a shadow. The fern still stands in the world as the whole picture. What comes loose when it is harvested, and shows in the inventory and hotbar, is now only the big fiddlehead, cut out of his picture exactly as drawn (nothing redrawn or recoloured).

- **"make it so the water is transparent and you sink and cannot swin due to the cold. what ideas do you have on why not blocks can be placed?"** (2026-10-12). The ocean's water is now see-through (the stone seabed and anyone in it show through) and is not solid: walking in, you sink at 2.5 blocks a second to the seabed 4 blocks down. Down there you can only wade at half speed and cannot jump, the cold still takes 2 health every 1.5 seconds, and the shore is a 4-block wall, so without digging or building a way out you fall and wake at the ship. "The water is too cold to swim in. You are sinking!" shows when you go in. This replaces swimming on the surface. He also asked for ideas for why blocks cannot be placed in the water; Claude offered some and is waiting for his pick. For now blocks can still be placed over the water as a bridge.

- **"make the hotbar disappear when the map is open"** (2026-10-12): while the world map (M) is open, the hotbar and its line of key hints are hidden. They come back when the map closes.

## Title screen, loading screen and creative mode (asked 2026-10-12)
- His words: "make a title screen with planet 1 in the background also balck hole and sun with creative mode option and survival mode and exit also add a loading screen"
- His picks: **no game name yet** (no title text). Creative mode, in his words: "infinite blocks and tools with a speed slider and map is full". Loading screen: the space scene with progress.
- As built:
  - **Title screen:** the game opens on Planet 1 seen from space (lit on the sun's side, dark on the other, with its ice, snow, rock, northern mountains and southern ocean), the black hole and the red sun, against the stars. Three buttons on the right: **Survival**, **Creative**, **Exit**. Exit stops play in the Unity editor and quits a built game.
  - **Loading screen:** the same scene, with "Loading..." and a line saying what is being made (Shaping the land, Raising the mountains, Freezing the lakes, Checking the way is clear, Digging the frost caves, Drawing the map, Waking up at the ship), and the black hole slowly turning. The world is now made after a mode is picked, away from the main thread, so the screen keeps moving. About 9 to 13 seconds; the last step holds the screen still for a few seconds.
  - **Survival:** the game as it was.
  - **Creative:** starts with the ice pickaxe and ice axe, and snow, ice and stone on the hotbar that never run out (no number shown). A **speed slider** at the top left under the health bar, from 1 to 40 squares a second. The **whole map** is drawn from the start, on the mini map and the world map. Things can still hurt you (he did not ask for that to change).
- No music on the title screen yet; his themes start when the game does. No way back to the title screen from the game yet.
- For Claude's automatic checks: starting Unity with `-skipTitle` goes straight into survival, and `-skipTitle -creative` into creative.

- **"make the planet spin a bit and the sun and black hole move a bit"** (2026-10-12): on the title and loading screens Planet 1 now turns slowly (once every 100 seconds, its lands moving west to east while the sunlit side stays put), and the sun and black hole drift a little in slow circles on opposite sides, as if circling each other. The black hole's disc rocks gently on the title screen and still turns steadily while loading.

- **"make a guaranteed cave right next to the northern caps exactly norht of the spawn"** (2026-10-12): one of the six frost caves is now always in the same place: due north of the crash site (the same column of the map, x 2880), in a tower of rock built 8 squares outside the edge of the North Pole's ice. Its ledge is at least 12 blocks up and at least 3 blocks above everything round it, so it still cannot be walked or jumped to. The other five are placed as before, keeping their distance from it. Claude kept the total at six (one guaranteed plus five); he did not say whether it should be an extra one.

- **"make huge patches of snow and or ice on the map"** (2026-10-12): the lowlands now have huge patches, several hundred squares across: snowfields that are nearly all snow, and ice fields that are nearly all ice, with only the odd rock in them. Between them is the usual mix of snow, ice lakes and rock. Roughly a quarter of the lowlands is snowfield and a quarter ice field (Claude's choice; the first try covered nearly everything, so it was toned down). The crash site is always on snow. Also: two spare cave spots are now picked, so a world still gets all six caves if shaping one opens a way up to it.

- **"the frost cave is very ugly. make it prettier"** (2026-10-12). Asked which part: **the outside**; the inside "well work on that later". The guaranteed cave north of the crash site used to be in a plain square tower of rock. Now it is a natural-looking crag about 15 squares across with a ragged edge, rising in tiers: a low skirt of rock, a shoulder, and uneven peaks in the middle tall enough for snow, with the mouth set into its south face and the rock stepping down in front so the mouth shows. It still cannot be walked or jumped onto: the skirt is at least 3 blocks above everything round it and the ledge at least 3 above the skirt. The inside of the caves is still to be made prettier (his ideas to pick from were icy walls and floor, icicles and ice pillars, glowing crystals, frozen pools).

- **"undo large ice and snow places"** (2026-10-12): the huge snowfields and ice fields are gone. The lowlands are back to the even mix of snow, ice lakes and rock they had before. (The two spare cave spots added at the same time are kept.)

## Algae biome (asked 2026-10-13)
- His words: "make an algae biome somewhere around the equator"
- His picks: shape, "one big region but either east or west of  the spawn depending on world sed"; colour, "teal green glowing at night"; ground, **algae over snow and ice**; things, "Algae block, ferns and clumps of algae and blocks".
- As built:
  - One big region on the equator, about 550 to 600 squares across with a wavy edge, centred 1000 squares east or west of the crash site's column (the seed decides which) and about 720 squares north of the ship. It shows on the maps in teal-green.
  - There, the top block of the snow and ice is a new **algae block**: teal-green with lighter and darker flecks. The usual snow, ice and stone are under it. Grey rocks in the region stay rock. Ice under algae is no longer slippery, since the algae is what you walk on.
  - **At night the algae glows**: in the top-down view it stays bright while everything else goes dark. (Not yet in the side-scroller view.)
  - Mining it gives an **Algae** block (as quick to mine as snow), which can be placed like any block. Creative mode has it on the hotbar.
  - Ferns still grow there. **Algae clumps** grow there too (about 1 square in 80): dark green mounds with bright tips, collected like ferns, giving an "Algae clump" item. No ice shards there. Nothing is made from clumps yet.

## Pause screen, settings and save slots (asked 2026-10-13)
- His words: "make a pause screen when you press esc. the options are, from top to bottom: [Resume] [Save]  [Settings] which include music volume and other settingsy things [Save and Quit] also make it since saving is an option so that there are 5 save slots you can start on creative and survival."
- His picks: **5 slots for each mode** (ten in all); settings also have **Show mini map, Zoom, Full screen**; Save and Quit goes **back to the title screen**.
- As built:
  - **Esc** pauses the game (everything stands still and the keys do nothing) and shows, top to bottom: **Resume, Save, Settings, Save and Quit**. Esc again resumes. If the world map, the items list or the workbench is open, Esc closes that instead, as before.
  - **Settings:** music volume (0 to 100%, heard straight away), zoom (0.7x to 1.8x of the usual view; a cave is always twice as close again), mini map on or off, full screen on or off (only does anything in a built copy of the game). They are kept on the computer and are the same for every slot.
  - **Title screen:** Survival and Creative each open their own list of **5 slots**. An empty slot has **New game**. A used one shows when it was saved and has **Continue** and **Delete** (Delete has to be pressed twice). **Back** returns to the three buttons.
  - A new game is saved in its slot as soon as it starts. **Save** saves it again; **Save and Quit** saves and returns to the title screen. There is no automatic saving while playing.
  - **What a save keeps:** the world (its seed, and every block dug or built, in the caves too), where the player is (in a cave too), their health, everything carried and the hotbar, the time of day, what has been harvested, which spiders have been killed, loose items on the ground, the squares the map has seen and the caves found, and creative mode's speed. Spiders go back to where they started.
  - Save files are in the game's own data folder on the computer (`AppData\LocalLow\DefaultCompany\game\saves`), a few kilobytes each.
- For Claude's automatic checks: `-skipTitle -slot 3` plays in slot 3, and adding `-load` carries on the game saved there.

- **Title theme** (2026-10-13). His words: "the title theme to play at the beggininnggg", with his file TitleTheme;D.mp3 from his Documents folder (about 4 and a half minutes). It is in the game unchanged as Assets/Resources/Music/Title Theme.mp3 (only the file's name is different). It plays, looping, on the title screen and the loading screen, and fades away over 3 seconds when the game begins, while the day or night theme fades in over 4. It follows the music volume setting. It plays again after Save and Quit.

- **"you have to name the save before opening it"** (2026-10-13): pressing New game on an empty slot now asks for a name first: a box to type in (up to 18 letters), with Start and Cancel. Start only works once something has been typed; Enter starts it too. The name is kept in the save and shown in the slot list with when it was saved. Saves made before this have no name and show as "Saved game".

- **Game font** (2026-10-13). His words: "for now put this font in as all fonts", with his file Show font game from his Documents folder (a TrueType font with no file ending; it has all the letters, numbers and usual symbols). It is in the game unchanged as Assets/Resources/Fonts/GameFont.ttf, and all the writing on screen uses it: title screen, slots, loading, pause and settings, hotbar, messages, health, maps, workbench. "For now": he may swap it later; replacing that one file changes it everywhere.

- **"zoom out the cave a bit"** (2026-10-13): a cave is now seen 1.5 times as close as the surface (it was 2 times), so about a third more of the cave fits on screen. It still follows the Zoom setting.

## Weather (asked 2026-10-13)
- His words: "weather 1/2 chance in a day that a cloud of snow snows down about the size of teh algae biome that moves across the planet in random direction. (you cant actually see the cloud but you can see the snow falling.) 1/10 chance of hail which damages you if not under a block. damage is rapid hits of hail from 1-5 damage each. moves and acts the same otherwise like snow."
- His picks: a cloud is **aimed to pass near you**; snow is **just the look**.
- As built:
  - At the start of each day (and when a game starts) the weather is rolled: a **1 in 2** chance of a snow cloud and a **1 in 10** chance of a hail cloud. The two rolls are separate, so now and then a day has both.
  - A cloud is about 560 squares across (the algae biome's size). It arrives at some point in the first 60% of the day, its edge 150 squares from the player, and drifts in a straight line in a random direction at walking speed (4 squares a second), its middle passing within half its radius of where the player was. So it takes about 40 seconds to reach you and a couple of minutes to pass over if you stand still. It is gone after drifting 2400 squares.
  - The cloud is never drawn. Under it, snowflakes fall across the screen (soft blue-grey and white, so they show on snow) with a faint whiteness; they thin out at the cloud's edge. Snow does nothing else.
  - Hail looks like fast blue streaks. Out in the open it hits every 0.5 to 1.1 seconds for **1 to 5** health each (about 3 to 5 health a second), with "Hail! Get under a block." the first time. A block anywhere over the player's head stops it. Standing in a full hail cloud with no cover would kill from full health in about 25 to 30 seconds.
  - No weather in the caves. Weather is not kept in a saved game: loading rolls the day again.
