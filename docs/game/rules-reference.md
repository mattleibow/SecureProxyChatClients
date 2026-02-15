# 📋 LoreEngine — Rules Quick Reference

> A concise reference card for all game mechanics, formulas, and data tables. Every value in this document is derived directly from the game engine source code.

---

## Character Defaults

| Attribute | Starting Value |
|---|---|
| Health (HP) | 100 |
| Max Health | 100 |
| Gold | 10 |
| Experience (XP) | 0 |
| Level | 1 |
| Starting Location | The Crossroads |
| All Stats (base) | 10 |

---

## Stats & Modifiers

### Core Stats

| Stat | Base | Modifier | Primary Use |
|---|---|---|---|
| **Strength** | 10 | **+2** | Melee attacks, feats of might |
| **Dexterity** | 10 | **+2** | Dodging, stealth, ranged attacks |
| **Wisdom** | 10 | **+1** | Magic, knowledge, perception |
| **Charisma** | 10 | **+1** | Persuasion, NPC interactions, bartering |

Any stat not recognized defaults to a **+0** modifier.

### Class Starting Stats

| Class | STR | DEX | WIS | CHA |
|---|---|---|---|---|
| ⚔️ Warrior | **14** | 10 | 10 | 10 |
| 🗡️ Rogue | 10 | **14** | 10 | **12** |
| 🪄 Mage | 10 | 10 | **14** | **12** |
| 🧭 Explorer | 10 | 10 | 10 | 10 |

---

## Dice System

### Roll Formula

```
Total = D20 (1–20) + Stat Modifier
Success = Total ≥ Difficulty Class (DC)
```

### Special Rolls

| Roll | Name | Effect |
|---|---|---|
| **Natural 20** | Critical Success | **Always succeeds**, regardless of DC |
| **Natural 1** | Critical Failure | **Always fails**, regardless of modifiers |

**Exact logic:** `Success = (d20 == 20) OR (total >= DC AND d20 != 1)`

### Difficulty Class (DC) Scale

| DC | Difficulty | Example |
|---|---|---|
| 1–5 | Trivial | Opening an unlocked door |
| 6–9 | Easy | Climbing a low wall |
| **10** | **Moderate** | Picking a simple lock |
| 11–14 | Challenging | Persuading a suspicious guard |
| 15–17 | Hard | Dodging a trap in the dark |
| 18–19 | Very Hard | Deciphering ancient runes |
| 20 | Nearly Impossible | Talking down a raging dragon |

DC range: **1 to 30** (clamped by the engine).

---

## Combat Formulas

### Player Attack

```
Roll D20 + relevant stat modifier
  vs. Creature's Attack DC
→ Hit: creature takes damage (narrated by DM)
→ Miss: creature counterattacks
```

### Creature Attack (Player Defense)

```
Roll D20 + Dexterity modifier (+2)
  vs. DC = 10 + Creature Level
→ Success: dodge/block, no damage
→ Failure: player takes creature's Damage value
```

### Damage & Healing Range

| Parameter | Min | Max |
|---|---|---|
| Health change per event | -200 | +200 |
| Gold change per event | -1,000 | +1,000 |
| XP award per event | 0 | 5,000 |

### Health Clamping

```
New HP = Clamp(Current HP + Amount, 0, Max HP)
```

### Gold Floor

```
New Gold = Max(0, Current Gold + Amount)
```

---

## XP & Leveling Table

### Level-Up Formula

```
Level up when: XP ≥ Current Level × 100
```

### On Level Up

- Level increments by 1
- Max HP increases by **10**
- HP is fully restored to new Max HP

### Progression Table

| Level | XP Threshold | Cumulative XP | Max HP |
|---|---|---|---|
| 1 | — | 0 | 100 |
| 2 | 100 | 100 | 110 |
| 3 | 200 | 300 | 120 |
| 4 | 300 | 600 | 130 |
| 5 | 400 | 1,000 | 140 |
| 6 | 500 | 1,500 | 150 |
| 7 | 600 | 2,100 | 160 |
| 8 | 700 | 2,800 | 170 |
| 9 | 800 | 3,600 | 180 |
| 10 | 900 | 4,500 | 190 |

*Note: XP threshold is checked once per event. If XP ≥ Level × 100 at the time of award, the player levels up immediately.*

---

## Item System

### Item Types

| Type ID | Display | Description |
|---|---|---|
| `weapon` | ⚔️ Weapon | Swords, staves, daggers |
| `armor` | 🛡️ Armor | Shields, helmets, cloaks |
| `potion` | 🧪 Potion | Consumable healing/buff items |
| `key` | 🔑 Key | Lockpicks, access keys, door openers |
| `misc` | 📦 Misc | Quest items, tools, trinkets |

Invalid type values default to `misc`.

### Rarity Tiers

| Tier | ID | Indicator |
|---|---|---|
| Common | `common` | Gray |
| Uncommon | `uncommon` | 🟢 Green |
| Rare | `rare` | 🔵 Blue |
| Epic | `epic` | 🟣 Purple |
| Legendary | `legendary` | 🟠 Orange ✨ |

Invalid rarity values default to `common`.

### Item Properties

| Property | Type | Notes |
|---|---|---|
| Id | string | Auto-generated 8-char hex ID |
| Name | string | Display name |
| Description | string | Lore/gameplay text |
| Emoji | string | Visual icon (max 10 chars) |
| Type | string | One of the 5 types above |
| Rarity | string | One of the 5 tiers above |
| Quantity | int | Stack count (default: 1) |

### Starting Inventory by Class

| Class | Item 1 | Item 2 | Bonus |
|---|---|---|---|
| Warrior | ⚔️ Iron Sword (weapon, common) | 🛡️ Leather Shield (armor, common) | 🧪 Healing Potion ×2 |
| Rogue | 🗡️ Twin Daggers (weapon, uncommon) | 🔧 Lockpicks (key, uncommon) | 🧪 Healing Potion ×2 |
| Mage | 🪄 Oak Staff (weapon, uncommon) | 📕 Spellbook (misc, rare) | 🧪 Healing Potion ×2 |
| Explorer | 🏒 Walking Stick (weapon, common) | 🗺️ Traveler's Map (misc, common) | 🧪 Healing Potion ×2 |

---

## Bestiary

### Creature Stat Blocks

| Creature | Lvl | HP | ATK DC | DMG | XP | Gold | Weakness |
|---|---|---|---|---|---|---|---|
| 👺 Goblin Scout | 1 | 15 | 8 | 3 | 25 | 5 | Fire |
| 🐀 Dire Rat | 1 | 10 | 6 | 2 | 15 | 2 | Light |
| 💀 Skeleton Warrior | 2 | 25 | 10 | 5 | 40 | 10 | Bludgeoning |
| 👻 Shadow Wisp | 3 | 20 | 12 | 7 | 60 | 15 | Radiant/Holy |
| 🗡️ Bandit Captain | 3 | 35 | 13 | 8 | 75 | 50 | Bribable |
| 🧌 Forest Troll | 4 | 60 | 11 | 10 | 100 | 25 | Fire (stops regen) |
| 💎 Crystal Golem | 5 | 80 | 14 | 12 | 150 | 40 | Thunder |
| 🦇 Wraith Lord | 6 | 50 | 15 | 15 | 200 | 100 | Sunlight |
| 🐍 Swamp Hydra | 7 | 100 | 13 | 18 | 250 | 60 | Fire (stops heads) |
| 🐉 Ancient Dragon | 10 | 200 | 18 | 30 | 1,000 | 500 | Rune weapons, dragonsbane |

### Creature Abilities

| Creature | Abilities |
|---|---|
| Goblin Scout | Nimble Dodge (avoid one attack/encounter) |
| Dire Rat | Disease Bite (10% poison chance) |
| Skeleton Warrior | Undead Resilience (poison immune), Bone Shield (+2 def) |
| Shadow Wisp | Incorporeal (half physical dmg), Life Drain (self-heal) |
| Bandit Captain | Riposte (counter on miss), Rally (buff allies) |
| Forest Troll | Regeneration (5 HP/round), Crushing Blow (2× crit dmg) |
| Crystal Golem | Magic Resistance (half spell dmg), Shard Burst (AoE <50% HP) |
| Wraith Lord | Dread Aura (fear DC 14), Phase Walk (teleport 30ft), Soul Rend (ignore armor) |
| Swamp Hydra | Multi-Attack (1/head), Regrow Head (unless cauterized), Venomous Bite (poison 3 rounds) |
| Ancient Dragon | Breath Weapon (40 fire dmg), Frightful Presence (DC 18 fear), Tail Sweep (AoE), Legendary Resistance (3 auto-saves/day) |

### Encounter Level Scaling

Creatures are selected within a range relative to the player's level:

```
Min creature level = Max(1, Player Level - 1)
Max creature level = Player Level + 2
```

| Player Level | Available Creatures |
|---|---|
| 1 | Goblin Scout, Dire Rat, Skeleton Warrior |
| 2 | Goblin Scout, Dire Rat, Skeleton Warrior, Shadow Wisp, Bandit Captain, Forest Troll |
| 3 | Skeleton Warrior, Shadow Wisp, Bandit Captain, Forest Troll, Crystal Golem |
| 4 | Shadow Wisp, Bandit Captain, Forest Troll, Crystal Golem, Wraith Lord |
| 5 | Forest Troll, Crystal Golem, Wraith Lord, Swamp Hydra |
| 6 | Crystal Golem, Wraith Lord, Swamp Hydra |
| 7 | Wraith Lord, Swamp Hydra |
| 8–10 | Swamp Hydra, Ancient Dragon |

---

## NPC System

### NPC Attributes

| Attribute | Values |
|---|---|
| Id | Auto-generated 8-char hex ID |
| Name | NPC's identity (default: "Stranger") |
| Role | Visible occupation (default: "unknown") |
| Description | Personality/appearance (default: "A mysterious figure.") |
| Hidden Secret | Known only to the DM; revealed through gameplay |
| Attitude | `friendly`, `neutral`, `hostile`, `suspicious` (default: `neutral`) |

**Note:** The Hidden Secret is stripped from NPC data before it's sent to the client. Players discover secrets through gameplay, not data inspection.

---

## Achievements

### Complete Achievement List

18 achievements across 5 categories. Achievements are checked automatically after each game action.

#### ⚔️ Combat (4)

| ID | Title | Emoji | Condition | Trigger |
|---|---|---|---|---|
| `first-blood` | First Blood | ⚔️ | Win first combat encounter | Event-based |
| `critical-hit` | Critical Hit | 🎯 | Roll a natural 20 | Event-based |
| `survivor` | Survivor | 💪 | Survive fight with < 5 HP | Event-based |
| `dragon-slayer` | Dragon Slayer | 🐉 | Defeat an Ancient Dragon | Event-based |

#### 🗺️ Exploration (3)

| ID | Title | Emoji | Condition | Trigger |
|---|---|---|---|---|
| `first-steps` | First Steps | 👣 | Leave The Crossroads | State: location ≠ "The Crossroads" |
| `explorer` | World Walker | 🗺️ | Visit 5 locations | State: visited ≥ 5 |
| `cartographer` | Cartographer | 🧭 | Visit 10 locations | State: visited ≥ 10 |

#### 💬 Social (3)

| ID | Title | Emoji | Condition | Trigger |
|---|---|---|---|---|
| `first-contact` | First Contact | 🤝 | Meet first NPC | Event-based |
| `diplomat` | Silver Tongue | 🗣️ | Succeed at charisma check | Event-based |
| `secret-keeper` | Secret Keeper | 🤫 | Discover NPC's hidden secret | Event-based |

#### 💰 Wealth (4)

| ID | Title | Emoji | Condition | Trigger |
|---|---|---|---|---|
| `first-loot` | Loot Goblin | 📦 | Find first item (inventory > 2) | State-based |
| `hoarder` | Hoarder | 🎒 | Own 10+ total items (by qty) | State: total qty ≥ 10 |
| `wealthy` | Wealthy | 💰 | Accumulate 100 gold | State: gold ≥ 100 |
| `rich` | Filthy Rich | 👑 | Accumulate 500 gold | State: gold ≥ 500 |

#### 📈 Progression (4)

| ID | Title | Emoji | Condition | Trigger |
|---|---|---|---|---|
| `level-2` | Getting Stronger | ⬆️ | Reach level 2 | State: level ≥ 2 |
| `level-5` | Seasoned Adventurer | 🌟 | Reach level 5 | State: level ≥ 5 |
| `level-10` | Legend | ✨ | Reach level 10 | State: level ≥ 10 |
| `twist-of-fate` | Tempting Fate | 🌀 | Trigger a Twist of Fate | Event-based |

### Achievement Trigger Types

- **State-based:** Checked automatically by comparing PlayerState values after every game action
- **Event-based:** Awarded by specific game events (combat victories, dice rolls, NPC interactions). These are tracked by the DM and game event handlers, not by the state checker.

---

## World Map

### Location Graph

| Location | Emoji | Connections |
|---|---|---|
| The Crossroads | ✖️ | Dark Forest, Village of Thornwall, Mountain Path, Swamp of Sorrows |
| Dark Forest | 🌲 | The Crossroads, Ancient Temple, Witch's Hut |
| Ancient Temple | 🏛️ | Dark Forest |
| Witch's Hut | 🏠 | Dark Forest, Swamp of Sorrows |
| Village of Thornwall | 🏘️ | The Crossroads, Castle Ironhold, Market Square |
| Castle Ironhold | 🏰 | Village of Thornwall |
| Market Square | 🏪 | Village of Thornwall |
| Mountain Path | ⛰️ | The Crossroads, Dragon's Peak, Dwarven Mines |
| Dragon's Peak | 🐉 | Mountain Path |
| Dwarven Mines | ⛏️ | Mountain Path |
| Swamp of Sorrows | 🏚️ | The Crossroads, Sunken Ruins, Witch's Hut |
| Sunken Ruins | 🗿 | Swamp of Sorrows |

**Total locations:** 12

### Dead Ends

These locations have only one exit — plan your route carefully:
- 🏛️ Ancient Temple (exit: Dark Forest)
- 🏰 Castle Ironhold (exit: Village of Thornwall)
- 🏪 Market Square (exit: Village of Thornwall)
- 🐉 Dragon's Peak (exit: Mountain Path)
- ⛏️ Dwarven Mines (exit: Mountain Path)
- 🗿 Sunken Ruins (exit: Swamp of Sorrows)

### Map Display Legend

| Symbol | Meaning |
|---|---|
| `[🏛️]` | Your current location |
| ` 🏛️ ` | Previously visited location |
| ` ? ` | Adjacent but unexplored |
| ` · ` | Unknown (not adjacent to visited) |

---

## Twists of Fate

### Categories & Counts

| Category | ID | Count |
|---|---|---|
| Environment | `environment` | 4 |
| Combat | `combat` | 1 |
| Encounter | `encounter` | 3 |
| Discovery | `discovery` | 4 |
| Personal | `personal` | 4 |
| **Total** | | **16** |

### Complete Twist List

| Title | Emoji | Category |
|---|---|---|
| Earthquake! | 🌋 | environment |
| Eclipse | 🌑 | environment |
| Fog of Whispers | 🌫️ | environment |
| Wild Magic Surge | ⚡ | environment |
| Ambush! | ⚔️ | combat |
| Mysterious Stranger | 🕵️ | encounter |
| Merchant of Wonders | 🏪 | encounter |
| Wounded Creature | 🦌 | encounter |
| Hidden Passage | 🚪 | discovery |
| Ancient Artifact | 💫 | discovery |
| Portal Rift | 🌀 | discovery |
| Treasure Map Fragment | 🗺️ | discovery |
| Memory Flash | 🧠 | personal |
| Cursed! | ☠️ | personal |
| Rival Appears | 🦹 | personal |
| Divine Vision | 👁️ | personal |

---

## Success Streaks

| Metric | Description |
|---|---|
| `SuccessStreak` | Current consecutive successful dice rolls |
| `MaxStreak` | All-time highest consecutive successes |

- Incremented on every successful `RollCheck`
- Reset to 0 on any failed `RollCheck`
- Critical successes count. Critical failures reset the streak.

---

## Game Engine Limits

| Parameter | Value |
|---|---|
| Max tool call rounds per turn | 8 |
| Max tool result size | 32,768 characters |
| AI response timeout | 5 minutes |
| Oracle question max length | 500 characters |
| Character name max length | 30 characters |
| String field max length | 500 characters |
| Emoji field max length | 10 characters |
| Oracle response timeout | 2 minutes |
| Story memories retrieved per turn | 5 most recent |

---

## Memory System

### Stored Memory Types

| Type ID | Trigger |
|---|---|
| `location` | Player moves to a new location |
| `character` | NPC is generated |
| `item` | Item is given or taken |
| `event` | Dice rolls, health changes, XP awards, narrative |
| `lore` | Oracle consultations |

### Memory Context

The DM receives the 5 most recent memories as "PAST EVENTS THE PLAYER REMEMBERS" in the system prompt, formatted as:

```
- [memory_type] memory content
```

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| POST | `/api/play/` | Process a game turn (non-streaming) |
| POST | `/api/play/stream` | Process a game turn (SSE streaming) |
| GET | `/api/play/state` | Get current player state |
| POST | `/api/play/new-game` | Start a new game with class selection |
| GET | `/api/play/twist` | Get a random Twist of Fate |
| GET | `/api/play/achievements` | Get all achievements with unlock status |
| POST | `/api/play/oracle` | Consult the Oracle with a question |
| GET | `/api/play/map` | Get ASCII world map with exploration progress |
| GET | `/api/play/encounter` | Generate a random combat encounter |

All endpoints require bearer token authentication and are rate-limited.

---

## Achievement Checklist

| # | Cat | ID | Title | Emoji | ☐ |
|---|---|---|---|---|---|
| 1 | ⚔️ | `first-blood` | First Blood | ⚔️ | ☐ |
| 2 | ⚔️ | `critical-hit` | Critical Hit | 🎯 | ☐ |
| 3 | ⚔️ | `survivor` | Survivor | 💪 | ☐ |
| 4 | ⚔️ | `dragon-slayer` | Dragon Slayer | 🐉 | ☐ |
| 5 | 🗺️ | `first-steps` | First Steps | 👣 | ☐ |
| 6 | 🗺️ | `explorer` | World Walker | 🗺️ | ☐ |
| 7 | 🗺️ | `cartographer` | Cartographer | 🧭 | ☐ |
| 8 | 💬 | `first-contact` | First Contact | 🤝 | ☐ |
| 9 | 💬 | `diplomat` | Silver Tongue | 🗣️ | ☐ |
| 10 | 💬 | `secret-keeper` | Secret Keeper | 🤫 | ☐ |
| 11 | 💰 | `first-loot` | Loot Goblin | 📦 | ☐ |
| 12 | 💰 | `hoarder` | Hoarder | 🎒 | ☐ |
| 13 | 💰 | `wealthy` | Wealthy | 💰 | ☐ |
| 14 | 💰 | `rich` | Filthy Rich | 👑 | ☐ |
| 15 | 📈 | `level-2` | Getting Stronger | ⬆️ | ☐ |
| 16 | 📈 | `level-5` | Seasoned Adventurer | 🌟 | ☐ |
| 17 | 📈 | `level-10` | Legend | ✨ | ☐ |
| 18 | 📈 | `twist-of-fate` | Tempting Fate | 🌀 | ☐ |

---

*LoreEngine — Where Every Choice Writes History* ✨
