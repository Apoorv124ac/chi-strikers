# Chi Strikers

Superpower street football. Mohalla United, a broke neighbourhood team whose five players each come from a different Indian martial art, take on the rich and ruthless Titan FC in 5-a-side matches where a full Chi bar turns a shot into a fire comet.

The look follows a clean 2D sports-cartoon style: flat two-tone colours, dark ink outlines, slim 6.5-head athletes with mitt hands and chunky sneakers, a glossy cyan indoor court that mirrors the players, amber stands full of tiny fans, and a side-on camera that pans with the play. Everything (players, arena, effects, sounds) is built in code, so the project needs no art or audio files.

## Open it

1. Install **Unity 6.3 LTS (6000.3.28f1)** from Unity Hub. Newer Unity 6 versions also work.
2. Get this repo onto your PC with GitHub Desktop (**File → Clone**) or **Code → Download ZIP**.
3. In Unity Hub choose **Add → Add project from disk** and pick the folder.
4. If Unity asks to enable the new Input System backends, click **Yes** (Unity restarts).
5. The game scene builds itself on first open. If you don't see it, use **Chi Strikers → Open Game Scene**. Then press **Play**.

## Modes

The title screen plays a CPU-vs-CPU match in the background.

- **Play vs CPU:** you are Mohalla United.
- **2 Players: Versus:** Player 1 is Mohalla United, Player 2 is Titan FC.
- **2 Players: Co-op vs CPU:** both players on Mohalla United.

Matches are two 3-minute halves. A draw goes to golden goal.

## Controls

| | Player 1 keyboard | Gamepad | Player 2 keyboard |
|---|---|---|---|
| Move | W A S D | Left stick | Arrow keys |
| Sprint | Shift | RT | Right Shift |
| Pass / tackle | J | A | Num 1 or `,` |
| Shoot (hold for power) / slide tackle | K | B | Num 2 or `.` |
| Lob / switch player | L | X (LB also switches) | Num 3 or `/` |
| **Special move** (full Chi) | Space | Y or RB | Num 0 or Right Ctrl |
| Pause | Esc | Start | |

The first gamepad joins Player 1 and the second joins Player 2. Holding shoot past the white mark on the power bar skies the ball.

## Chi and special moves

Each player has a Chi bar. It fills slowly on its own and quickly from completed passes, tackles, interceptions and saves. With a full bar, press Special. The game slows down, the camera swings in, the move's name punches onto the screen, and then:

| Hero | Martial art | Special | What it does |
|---|---|---|---|
| Veer (striker) | Kalaripayattu | **Agni Strike** | Spin-jump volley. The ball becomes a fire comet that rips a trench in the floor and blasts through up to 3 players. |
| Tara (winger) | Mallakhamb | **Sky Step** | Pops the ball up, runs three steps up the air, hangs in slow motion, flips and volleys it down. Flies over defenders. |
| Kabir (playmaker) | Gatka | **Chakra Strike** | The ball spins like a chakram, goes wide, then bends around everyone toward the far post. |
| Bhola (defender) | Pehlwani | **Akhada Wall** | Slams the floor: a shockwave launches nearby opponents and wins the ball. For 3.5 s he blocks any shot, even specials. Works without the ball. |
| Guru Dada (keeper) | Yoga | **Thousand Hands** | Triggers by itself when a special comes at him with full Chi: golden hands stop the ball dead. |

Titan FC have the same five mechanics with their own look: Rex's **Rail Shot**, Nova's **Prism Volley**, Silas's **Vortex Curve**, Goliath's **Titan Slam** and Atlas's **Iron Gate**. Their specials trail black smoke.

Only specials beat specials. A keeper without Thousand Hands or Iron Gate needs at least half a Chi bar to have a 50% chance to punch a special away; otherwise the keeper is blasted into the net.

## Code map

```
Assets/ChiStrikers/
  Resources/ChiToon.shader   flat two-tone cel shading + constant-width ink outline
  Editor/ChiStrikersEditor.cs  builds the scene on first open, menu items
  Scripts/
    Match.cs     entry point: builds everything, match flow, human control, ball contacts, saves
    Player.cs    movement, dribbling, passes, shots, tackles, Chi
    Brain.cs     CPU players: formation, pressing, passing, shooting, specials, goalkeeping
    Specials.cs  the special-move cinematics
    Ball.cs      ball physics (boards, posts, crossbar, net), curve and specials; Pitch sizes
    Body.cs      the procedural cartoon athlete, its animation, and its floor reflection
    Arena.cs     glossy court, lines, goals and nets, ad boards, stands and crowd, LED scoreboard
    Fx.cs        particles, shockwave rings, floor cracks, after-images, golden hands, ball auras
    CameraRig.cs side-on pan camera, special close-ups, ball follow, shake
    HUD.cs       scoreboard, Chi cards, banners, kinetic special titles, title / pause / help screens
    Defs.cs      teams, heroes, kits and specials
    Controls.cs  keyboard + gamepad input (Input System)
    Sfx.cs       synthesised sounds (kicks, whistle, crowd, booms)
    Util.cs      materials, meshes, helpers
```

To tune things quickly: team rosters, kit colours and per-hero stats (Speed, Shot, Tackle, Keeping) are in `Defs.cs`; half length is `Match.HalfLength`; camera framing is at the top of `CameraRig.cs`.
