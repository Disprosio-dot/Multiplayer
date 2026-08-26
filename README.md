> # ⚠️ Unofficial Multifaction Fork
>
> This is a working fork of [rwmt/Multiplayer](https://github.com/rwmt/Multiplayer) by **Gesprosio**, built on top of the `dev` branch plus [PR #961](https://github.com/rwmt/Multiplayer/pull/961) by CormacOConnor72. It focuses on making **multifaction** games feel like everyone is playing their own singleplayer story on a shared planet.
>
> **What it adds/fixes** (full changelog in the commit history):
> - Quest ownership: quests, dialog choices, letters and epic quest chains (relic hunts!) belong to the right player's faction; parallel relic quests, one per ideoligion
> - **Custom and fluid ideoligions at faction creation** for joining players — full in-game editor, no more singleplayer detour
> - Per-faction scenarios, deterministic map generation, identical techprint crates
> - **Faction retirement**: remove a lost player faction cleanly (settlements, caravans, pawns), players return to spectator
> - Save corruption fix: saving while a gravship is in transit is blocked (the file came out unloadable)
> - Naming prompts for faction/colony re-enabled, caravan events pause the game, pod targeter soft-lock fixed, several real desyncs diagnosed and killed
>
> **Install**: replaces the official Multiplayer mod — do not enable both. Host and all clients must run this **exact same build**. Download from [Releases](https://github.com/Disprosio-dot/Multiplayer/releases), drop the folder into `RimWorld/Mods/`, place right below Core and expansions. Requires [Prepatcher](https://steamcommunity.com/sharedfiles/filedetails/?id=2934420800).
>
> **Transparency**: developed in pair-programming with AI (Claude by Anthropic), with every change reviewed, documented and play-tested in real multiplayer games. License stays MIT. Credits: Zetrith and the RimWorld Multiplayer Team, CormacOConnor72 (base PR), cmlee119 and TMaGoYT (approaches referenced from issue threads).
>
> Fixes that make sense upstream will be offered as targeted PRs to rwmt/Multiplayer.

---

![banner](https://user-images.githubusercontent.com/49448379/134965756-2a30ffd9-2f6c-43d6-a2a4-584252fc2e4b.png)



The RimWorld Multiplayer mod allows users to play full games of Rimworld cooperatively.

## Links
[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2606448745) |
[Website](https://rimworldmultiplayer.com) |
[Discord](https://discord.gg/S4bxXpv) |
[Documentation](https://hackmd.io/@rimworldmultiplayer/docs/)

## Development
[Git Releases](https://github.com/rwmt/Multiplayer/releases) |
[Installation](https://hackmd.io/@rimworldmultiplayer/docs/https%3A%2F%2Fhackmd.io%2F%40rimworldmultiplayer%2Fplay%23Installation) |
[Hosting](https://hackmd.io/@rimworldmultiplayer/docs/https%3A%2F%2Fhackmd.io%2F%40rimworldmultiplayer%2Fplay%23Installation#Hosting-and-Joining) |
[FAQ](https://hackmd.io/@rimworldmultiplayer/docs/https%3A%2F%2Fhackmd.io%2F%40rimworldmultiplayer%2Ffaq%23Common-Questions#FAQ) |
[Contributing](https://github.com/rwmt/Multiplayer/blob/master/CONTRIBUTORS.md) |
[DEV Wiki](https://hackmd.io/@rimworldmultiplayer/docs/https%3A%2F%2Fhackmd.io%2F%40rimworldmultiplayer%2Fdev-home)

Please do all pull requests to the [dev](https://github.com/rwmt/Multiplayer/tree/dev) branch.

## Donations

If you’re feeling generous these are people who have contributed greatly to the mod’s development and upkeep.

**[Zetrith](https://patreon.com/zetrith)** - Creator, Core, Support\
**[NotFood](https://ko-fi.com/notfood)** - Core, Mod Compatiblity, Compatibility Commissions\
**[Sokyran](https://ko-fi.com/sokyran)** - Core, Mod Compatiblity, Compatibility Commissions\
[Nebual](https://ko-fi.com/Nebual) - Core\
[Thomas107500](https://ko-fi.com/thomas107500) - Mod Compatiblity\
[Luz](https://ko-fi.com/llavorre) - Support, Admin\
[Mistress Mia](https://ko-fi.com/miaamakiir) - Support, Admin\
[Swept](https://ko-fi.com/swept) - Support, Admin, Website

## Notes
Thanks to Pardeike for making [Harmony](https://github.com/pardeike/Harmony) and RevenantX for creating [LiteNetLib](https://github.com/RevenantX/LiteNetLib)

