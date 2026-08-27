# Multiplayer — Multifaction Fork

Unofficial fork of [rwmt/Multiplayer](https://github.com/rwmt/Multiplayer) focused on multifaction play, where every player runs their own colony and faction on the same planet.

> [!IMPORTANT]
> **About AI** — This build was made to be used inside a group of friends, so I never worried about how heavily AI was involved: it's built pair-programming with AI (Claude), *a lot of it*. We just wanted our own personal adventure to work, and in a way we got exactly that. Everything still gets reviewed and play-tested in our real sessions before it lands — but if AI-assisted mods aren't something you want to use, that's a fair position, and the official mod is [right here](https://github.com/rwmt/Multiplayer).

My friend and I have been playing multifaction for a while and kept running into the same problems: the relic quest chain never advanced, events kept hitting the wrong colony, dialog choices went to whoever clicked first, joining players could only pick preset ideoligions, and we desynced a lot. Most of this is known on the upstream tracker, but multifaction is a niche mode and understandably not the priority over there. So I started fixing the things that were in our way, and this fork is the result.

The idea I kept in mind while working on it: each player should feel like they're playing their own singleplayer game, with all the choices the game normally offers, just on a shared planet.

Based on the upstream `dev` branch plus [PR #961](https://github.com/rwmt/Multiplayer/pull/961) by CormacOConnor72, which already fixed a lot of the per-faction event routing. Everything else is in the commit history.

**[Download the latest release here.](https://github.com/Disprosio-dot/Multiplayer/releases)**

## What's changed

**Quests actually belong to someone now**

- Quests are owned by the faction they were generated for: letters, dialog choices and rewards go to that player, and nobody else can answer in their place
- Other players see a "«faction»'s quest" entry in the quest log instead of the full details
- Choice letters wait longer before force-picking a default (+1 day, up to twice, with a warning before they expire)
- Epic quest chains run in parallel: each ideoligion gets its own relic hunt, so one player's quest no longer blocks everyone else's
- Quest timers run on the right map's clock (detected via reflection, so DLC and modded quests are covered too)
- With multiple ideoligions in the colony, grouped ritual gizmos open the right ideoligion's dialog instead of refusing with "another ritual session is already in progress"

**You can create your own ideoligion in game**

- Joining players get the full vanilla ideoligion editor right in the faction creation flow: memes, precepts, rituals, styles, everything
- Fluid ideoligions work too
- Before this you had to start a singleplayer game, build the ideoligion there, save it to a file and load it in multiplayer (that still works, if you have files lying around)
- The editor works on a detached copy that only gets rebuilt into the game through a synced command — that's why it doesn't desync

**Faction management**

- The host can retire a failed or abandoned player faction from the Factions window: settlements get abandoned properly, caravans and leftover pawns are cleaned up, and its players go back to spectator so they can start a new colony (getting this desync-free took three rounds of debugging with real desync reports)
- Each faction plays its own scenario, not the host's
- Faction and colony naming prompts work again, and each player names their own

**Stability**

- Saves can no longer be corrupted by gravships: saving mid-transit produced a broken file (ship and crew just missing), now all save paths wait until you've landed — saving in orbit is fine, same as vanilla
- Four real desyncs diagnosed from actual desync reports and fixed at the root
- A vanilla bug in "Usually..." precepts made the RNG stream unstable in multiplayer — neutralized
- Map generation for quest sites is deterministic now, so all clients get identical loot and techprint crates

**Smaller stuff**

- Caravan ambushes pause the game like in singleplayer, instead of quietly killing your caravan while nobody watches
- The transport pod targeter can't soft-lock anymore when two players aim the same pods
- Letters survive reconnection instead of closing themselves after 4 seconds
- Dev mode actions (like removing hediffs from the health tab) are properly synced

## How tested is this?

The big things (parallel relic hunts, faction retirement, the ideoligion editor, the gravship save block, dev mode) are confirmed in our own games, two instances, real sessions. Some of the smaller fixes are waiting for their situation to come up naturally in play. If something breaks for you, the most useful things to share are the desync report (`MpDesyncs` folder in your RimWorld user data) or a screenshot of the Debug log.

Things I'd like to do next: per-faction Anomaly monolith, multiple gravships, the Archonexus ending in multifaction.

## What about other mods?

Mod compatibility is the same as upstream — the sync API is untouched, so a mod that desyncs on the official Multiplayer will desync here too. This fork doesn't fix other mods.

That said, a few things might turn out better in practice (no promises, we play mostly vanilla):

- Modded and DLC quests get their timers put on the right map clock — the base had a hardcoded list of vanilla quest types, this fork detects them via reflection
- Map generation for quest sites is deterministic now, which should help any modded content that spawns there
- With four vanilla desyncs out of the way, when something does desync in a modded game it's easier to tell the mod is the culprit

Fixes that make sense for everyone will be offered upstream as proper pull requests.

## Credits and license

- **Zetrith** and the **RimWorld Multiplayer Team** made and maintain the actual mod. If you want to support someone, [support them](https://github.com/rwmt/Multiplayer#donations).
- **CormacOConnor72** wrote PR #961, which this whole fork stands on.
- **cmlee119** and **TMaGoYT** shared approaches in upstream issue threads that I picked up.
- MIT license, same as upstream.

[Official mod](https://github.com/rwmt/Multiplayer) · [Discord](https://discord.gg/S4bxXpv) · [FAQ and docs](https://hackmd.io/@rimworldmultiplayer/docs/) · [Releases](https://github.com/Disprosio-dot/Multiplayer/releases)
