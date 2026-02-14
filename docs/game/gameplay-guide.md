# 🎮 LoreEngine — Complete Gameplay Guide

> *"The world shapes itself around your choices. No two journeys are alike."*

Welcome to **LoreEngine**, an AI-powered interactive fiction RPG where a living Dungeon Master narrates your adventure in real time. Every conversation, every battle, and every discovery is dynamically generated — powered by advanced AI, grounded in classic tabletop mechanics.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Character Classes](#character-classes)
3. [Core Mechanics](#core-mechanics)
4. [Combat System](#combat-system)
5. [Inventory & Items](#inventory--items)
6. [The Oracle](#the-oracle)
7. [Twists of Fate](#twists-of-fate)
8. [World Map & Exploration](#world-map--exploration)
9. [Story Memory](#story-memory)
10. [Tips & Strategies](#tips--strategies)

---

## Getting Started

### Registration

Create an account to begin your adventure. Your account is secured with bearer token authentication, and your game state is saved automatically after every turn.

### Starting a New Game

When you start a new game, you choose:

- **Character Name** — Up to 30 characters. This is how the Dungeon Master and NPCs will address you.
- **Character Class** — Warrior, Rogue, Mage, or Explorer. Each class starts with different stats, equipment, and playstyle strengths.

After character creation, you begin at **The Crossroads** — the central hub of the world — with 100 HP, 10 Gold, and your class-specific starter gear plus two Healing Potions.

### How a Turn Works

Each turn follows this loop:

1. **You describe your action** — Type what you want to do in natural language ("I search the room for traps", "I attack the goblin", "I try to persuade the guard").
2. **The AI Dungeon Master responds** — It narrates the scene, rolls dice for risky actions, awards items and XP, and manages combat — all automatically through game tools.
3. **Your state updates** — HP, Gold, XP, inventory, and location change in real time. Achievements unlock automatically when conditions are met.
4. **The DM prompts you** — Every scene ends with "What do you do?" to keep the adventure moving.

The DM narrates in second person ("You enter the tavern...") and renders ASCII art at the start of each new scene. The tone is **dark fantasy with moments of humor** — think Discworld meets Dark Souls.

---

## Character Classes

### ⚔️ Warrior

*"Steel solves most problems."*

The frontline fighter — tough, powerful, and built to take a hit.

| Stat | Value |
|---|---|
| Strength | **14** |
| Dexterity | 10 |
| Wisdom | 10 |
| Charisma | 10 |

**Starting Equipment:**
- ⚔️ Iron Sword (weapon, common) — "A sturdy blade"
- 🛡️ Leather Shield (armor, common) — "Basic protection"
- 🧪 Healing Potion ×2 (potion) — "Restores 25 HP"

**Playstyle:** Strength-based combat. Your +2 STR modifier makes you the best at melee attacks and feats of physical might. Charge in, swing hard, absorb damage.

---

### 🗡️ Rogue

*"Why fight fair when you can fight smart?"*

Master of stealth, cunning, and precision strikes.

| Stat | Value |
|---|---|
| Strength | 10 |
| Dexterity | **14** |
| Wisdom | 10 |
| Charisma | **12** |

**Starting Equipment:**
- 🗡️ Twin Daggers (weapon, uncommon) — "Quick and deadly"
- 🔧 Lockpicks (key, uncommon) — "Opens most locks"
- 🧪 Healing Potion ×2 (potion) — "Restores 25 HP"

**Playstyle:** Dexterity-focused with a Charisma edge. Excels at dodging, stealth, ranged attacks, and talking your way out of trouble. Your lockpicks open doors others can't.

---

### 🪄 Mage

*"Knowledge is the ultimate weapon."*

Wielder of arcane power and ancient knowledge.

| Stat | Value |
|---|---|
| Strength | 10 |
| Dexterity | 10 |
| Wisdom | **14** |
| Charisma | **12** |

**Starting Equipment:**
- 🪄 Oak Staff (weapon, uncommon) — "Channels arcane energy"
- 📕 Spellbook (misc, rare) — "Contains basic incantations"
- 🧪 Healing Potion ×2 (potion) — "Restores 25 HP"

**Playstyle:** Wisdom-based with Charisma support. Best at magic, knowledge checks, and perception. Your Spellbook is rare-tier from the start — a head start on arcane power.

---

### 🧭 Explorer

*"The journey is the destination."*

The jack-of-all-trades — balanced, versatile, and ready for anything.

| Stat | Value |
|---|---|
| Strength | 10 |
| Dexterity | 10 |
| Wisdom | 10 |
| Charisma | 10 |

**Starting Equipment:**
- 🏒 Walking Stick (weapon, common) — "Better than nothing"
- 🗺️ Traveler's Map (misc, common) — "Shows nearby areas"
- 🧪 Healing Potion ×2 (potion) — "Restores 25 HP"

**Playstyle:** No stat bonuses, but no weaknesses either. The Explorer can adapt to any situation. Ideal for players who want to experience everything the world has to offer without committing to a single playstyle.

---

## Core Mechanics

### Hit Points (HP)

- **Starting HP:** 100 (all classes)
- **Max HP increases** by 10 each time you level up
- **Healing** restores HP up to your current maximum
- **At 0 HP:** You are incapacitated — not permanently dead, but there will be consequences
- Health changes are clamped between 0 and your Max HP
- Damage and healing range from -200 to +200 per event

### Gold

- **Starting Gold:** 10
- Gold cannot drop below 0
- Earn gold by defeating creatures (2–500 gold depending on the creature), completing quests, and finding treasure
- Spend gold on equipment, bribes, and access to certain areas
- Gold changes are clamped between -1,000 and +1,000 per event

### Experience Points (XP) & Leveling

XP is awarded for defeating creatures, solving puzzles, exploring new locations, and successful social encounters. XP awards range from 0 to 5,000 per event.

**Leveling formula:**

```
Level up when: XP ≥ Current Level × 100
```

| Level | XP Required | Max HP |
|---|---|---|
| 1 → 2 | 100 XP | 110 |
| 2 → 3 | 200 XP | 120 |
| 3 → 4 | 300 XP | 130 |
| 4 → 5 | 400 XP | 140 |
| 5 → 6 | 500 XP | 150 |
| 6 → 7 | 600 XP | 160 |
| 7 → 8 | 700 XP | 170 |
| 8 → 9 | 800 XP | 180 |
| 9 → 10 | 900 XP | 190 |
| 10+ | 1,000+ XP | 200+ |

**On level up:**
- Max HP increases by 10
- HP is fully restored to your new maximum
- Stronger creatures become available as encounters
- Better rewards from combat and exploration

### Stats & Modifiers

Four stats define your character. They influence dice rolls for actions related to that stat.

| Stat | Modifier | Best For |
|---|---|---|
| **Strength** | +2 | Melee attacks, breaking things, feats of physical power |
| **Dexterity** | +2 | Dodging, stealth, ranged attacks, acrobatics |
| **Wisdom** | +1 | Magic, perception, knowledge checks, puzzle solving |
| **Charisma** | +1 | Persuasion, NPC interactions, bartering, deception |

Base value for all stats is 10. Class bonuses raise specific stats at character creation.

### Success Streaks

The game tracks your consecutive successful dice rolls. Each success increments your streak counter; any failure resets it to zero. Your all-time maximum streak is also recorded as a personal best.

---

## Combat System

### How Combat Works

When you encounter a hostile creature or provoke a fight, combat begins. The AI Dungeon Master handles all mechanics using the game tools.

**Combat Round Structure:**

1. **Player Action** — You describe what you do (attack, defend, use an item, flee, or attempt something creative)
2. **Attack Roll** — The DM calls `RollCheck` with your relevant stat against the creature's Attack DC
3. **Resolution:**
   - **Hit:** The DM calls `ModifyHealth` on the creature (tracked in narrative)
   - **Miss:** The creature counterattacks
4. **Creature Attack** — If the creature attacks, the DM rolls a defense check for you (Dexterity vs. DC 10 + creature level)
5. **Damage:** On failed defense, the DM calls `ModifyHealth` with negative damage equal to the creature's Damage stat
6. **Victory:** When the creature is defeated, you receive XP via `AwardExperience` and Gold via `ModifyGold`

### The Dice Roll

Every risky action uses a D20 roll:

```
🎲 Total = D20 (1-20) + Stat Modifier (+1 or +2)
        vs.
🎯 Difficulty Class (DC)
```

- **Total ≥ DC** → Success
- **Total < DC** → Failure
- **Natural 20** → Critical Success — **always succeeds**, regardless of DC
- **Natural 1** → Critical Failure — **always fails**, regardless of modifiers

DC ranges from 1 (trivial) to 30 (maximum), with 10 being moderate difficulty.

### Creature Encounters

Encounters are generated based on your current level. The game selects creatures within ±1 to +2 levels of yours, ensuring fights are challenging but fair.

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
| 🐍 Swamp Hydra | 7 | 100 | 13 | 18 | 250 | 60 | Fire (stops regrow) |
| 🐉 Ancient Dragon | 10 | 200 | 18 | 30 | 1,000 | 500 | Rune weapons |

### Creature Abilities

Each creature has unique abilities that affect the flow of combat:

- **Goblin Scout:** Nimble Dodge — can avoid one attack per encounter
- **Dire Rat:** Disease Bite — 10% chance to poison on hit
- **Skeleton Warrior:** Undead Resilience (immune to poison), Bone Shield (+2 defense)
- **Shadow Wisp:** Incorporeal (physical attacks deal half damage), Life Drain (heals itself)
- **Bandit Captain:** Riposte (counterattacks on miss), Rally (buffs allies)
- **Forest Troll:** Regeneration (5 HP/round), Crushing Blow (double damage on crit)
- **Crystal Golem:** Magic Resistance (halves spell damage), Shard Burst (AoE below half HP)
- **Wraith Lord:** Dread Aura (fear DC 14), Phase Walk (teleport), Soul Rend (ignores armor)
- **Swamp Hydra:** Multi-Attack (one per head), Regrow Head, Venomous Bite (poison 3 rounds)
- **Ancient Dragon:** Breath Weapon (40 fire damage), Frightful Presence (DC 18), Tail Sweep (AoE), Legendary Resistance (auto-saves ×3)

### Combat Tips

- **Exploit weaknesses** — The DM hints at creature weaknesses for observant players
- **Creative solutions work** — The Bandit Captain can be bribed or persuaded instead of fought
- **Retreat is valid** — There's no shame in running from a fight you can't win
- **Use items** — A Healing Potion mid-combat can save your life

---

## Inventory & Items

### Item Types

| Type | Description | Examples |
|---|---|---|
| ⚔️ **Weapon** | Tools of combat | Iron Sword, Twin Daggers, Oak Staff |
| 🛡️ **Armor** | Defensive equipment | Leather Shield |
| 🧪 **Potion** | Consumable items | Healing Potion (restores 25 HP) |
| 🔑 **Key** | Unlock doors and secrets | Lockpicks |
| 📦 **Misc** | Everything else | Spellbook, Traveler's Map, quest items |

### Rarity Tiers

Items come in five tiers of increasing power and scarcity:

| Rarity | Indicator | Drop Rate |
|---|---|---|
| **Common** | Gray | Found frequently |
| **Uncommon** | 🟢 Green | A step above basic |
| **Rare** | 🔵 Blue | Genuinely powerful |
| **Epic** | 🟣 Purple | Exceptional, with unique abilities |
| **Legendary** | 🟠 Orange ✨ | Mythic power. Extremely rare |

### How Items Work

- Items are **added** to your inventory via the `GiveItem` tool when you find, buy, or receive them
- Items are **removed** via `TakeItem` when you use, sell, drop, or lose them
- Each item has a **name**, **description**, **emoji**, **type**, **rarity**, and **quantity**
- Items stack by quantity — two Healing Potions show as a single entry with ×2
- The DM awards items narratively — find treasure in a chest, loot a defeated creature, or receive a reward from an NPC

---

## The Oracle

*"Seek the Oracle when the path grows dark."*

The Oracle is a separate AI entity that speaks in **riddles, prophecy, and archaic language**. It never gives direct answers — instead, it wraps hints in mysticism.

### How to Consult the Oracle

Send a question (up to 500 characters) and the Oracle responds with a cryptic hint based on your current game state.

The Oracle knows:
- Your character name, class, and level
- Your current location
- Your HP, Gold, and recent events
- The creatures and locations of the world

### Oracle Response Format

Every response follows this pattern:
1. Opens with *"The Oracle speaks:"*
2. Delivers 2–3 sentences of riddle-wrapped guidance
3. Closes with a cryptic farewell (*"The mists close..."* or *"The vision fades..."*)

### Example Oracle Exchanges

**Question:** "How do I beat the troll?"
> *"The Oracle speaks: The green one fears what dances and consumes. That which warms the hearth also cleanses the flesh that knits itself anew. Carry the flame, child, or be devoured by that which cannot die. The vision fades..."*

**Translation:** Use fire to stop the Forest Troll's regeneration.

**Question:** "Where should I go next?"
> *"The Oracle speaks: Thou seeketh riches, yet thy blade is dull and thy pouch light. The village folk trade iron for coin, and coin for survival. Sharpen thyself before the mountain calls. The mists close..."*

**Translation:** Visit the Village of Thornwall to gear up before attempting the Mountain Path.

---

## Twists of Fate

At any time, you can trigger a **Twist of Fate** — a random dramatic event that shakes up your adventure. There are **16 possible twists** across four categories.

### 🌍 Environment Twists

| Twist | Emoji | What Happens |
|---|---|---|
| **Earthquake!** | 🌋 | The ground shakes, cracks open, and something ancient stirs beneath the surface |
| **Eclipse** | 🌑 | Unnatural darkness falls. Stars appear in daylight. Shadows move on their own |
| **Fog of Whispers** | 🌫️ | Luminous fog rolls in carrying fragments of a prophecy about you |
| **Wild Magic Surge** | ⚡ | Untamed magical energy makes every spell and enchantment go haywire |

### ⚔️ Combat Twists

| Twist | Emoji | What Happens |
|---|---|---|
| **Ambush!** | ⚔️ | Enemies burst from hiding — you're surrounded and must fight or escape |

### 👤 Encounter Twists

| Twist | Emoji | What Happens |
|---|---|---|
| **Mysterious Stranger** | 🕵️ | A cloaked figure claims to know your destiny — but their intentions are unclear |
| **Merchant of Wonders** | 🏪 | A peculiar merchant appears selling bottled starlight, future-telling mirrors, and hidden maps |
| **Wounded Creature** | 🦌 | A magnificent half-dragon, half-stag creature lies dying — it looks at you with intelligent eyes |

### 🔍 Discovery Twists

| Twist | Emoji | What Happens |
|---|---|---|
| **Hidden Passage** | 🚪 | A wall shifts, revealing a passage descending into cold, ancient darkness |
| **Ancient Artifact** | 💫 | An artifact from a civilization that shouldn't exist pulses with power and seems to recognize you |
| **Portal Rift** | 🌀 | A shimmering rift tears open showing another world. It's unstable and won't last long |
| **Treasure Map Fragment** | 🗺️ | A fragment of an old map marks a location very close to where you stand |

### 👁️ Personal Twists

| Twist | Emoji | What Happens |
|---|---|---|
| **Memory Flash** | 🧠 | A vivid memory floods your mind — but it belongs to someone who stood here centuries ago |
| **Cursed!** | ☠️ | A malevolent presence latches onto your soul. Something inside you has changed |
| **Rival Appears** | 🦹 | Someone from your past arrives to settle an old score |
| **Divine Vision** | 👁️ | A deity notices you and sends a vision — a task that promises power but threatens divine wrath |

Twists are woven into the ongoing narrative by the AI Dungeon Master. They can alter your path, introduce new characters, reveal secrets, or throw you into unexpected combat.

---

## World Map & Exploration

The world of LoreEngine consists of **12 interconnected locations**. Travel between them by following connected paths.

### World Map Layout

```
                    🏛️ Ancient Temple
                   ↗
        🌲 Dark Forest ─────→ 🏠 Witch's Hut
       ↗                                ↑
      /                                  |
✖️ The Crossroads ────→ 🏚️ Swamp of Sorrows ─→ 🗿 Sunken Ruins
      \
       ↘
        🏘️ Village of Thornwall ─→ 🏰 Castle Ironhold
                    ↘
                     🏪 Market Square
      ↘
       ⛰️ Mountain Path ─→ 🐉 Dragon's Peak
                    ↘
                     ⛏️ Dwarven Mines
```

### Location Directory

| Icon | Location | Connections |
|---|---|---|
| ✖️ | **The Crossroads** | Dark Forest, Village of Thornwall, Mountain Path, Swamp of Sorrows |
| 🌲 | **Dark Forest** | The Crossroads, Ancient Temple, Witch's Hut |
| 🏛️ | **Ancient Temple** | Dark Forest |
| 🏠 | **Witch's Hut** | Dark Forest, Swamp of Sorrows |
| 🏘️ | **Village of Thornwall** | The Crossroads, Castle Ironhold, Market Square |
| 🏰 | **Castle Ironhold** | Village of Thornwall |
| 🏪 | **Market Square** | Village of Thornwall |
| ⛰️ | **Mountain Path** | The Crossroads, Dragon's Peak, Dwarven Mines |
| 🐉 | **Dragon's Peak** | Mountain Path |
| ⛏️ | **Dwarven Mines** | Mountain Path |
| 🏚️ | **Swamp of Sorrows** | The Crossroads, Sunken Ruins, Witch's Hut |
| 🗿 | **Sunken Ruins** | Swamp of Sorrows |

### Map Display

The in-game map shows your exploration progress:
- **[Emoji]** in brackets — Your current location
- **Emoji** — A location you've previously visited
- **?** — An adjacent location you haven't explored yet
- **·** — An unknown location (not adjacent to anywhere you've been)

Your exploration progress is tracked as "Explored: X/12 locations."

### The Witch's Hut Shortcut

The Witch's Hut connects the Dark Forest to the Swamp of Sorrows, making it a valuable shortcut between the western and southern regions of the map.

---

## Story Memory

LoreEngine features a persistent story memory system that gives the AI Dungeon Master context about your journey. The game remembers:

| Memory Type | What's Stored | Example |
|---|---|---|
| **Location** | Where you've traveled | "Traveled to Dark Forest" |
| **Character** | NPCs you've met | "Met Elara, a wandering healer" |
| **Item** | Items gained or lost | "Acquired Iron Sword" |
| **Event** | Dice rolls, combat, story beats | "Rolled 18 for strength check (succeeded)" |
| **Lore** | Oracle consultations | "Consulted the Oracle about: the dragon" |

### How Memory Works

- The DM receives your **5 most recent memories** as context before each response
- Significant game events (movement, combat, NPC meetings, item changes) are automatically stored
- Oracle consultations are stored as lore memories
- Narrative text from each turn is summarized and stored as an event memory
- Memories ensure the AI maintains **narrative continuity** — it won't forget that you befriended the witch or that the guard recognized you from a previous encounter

### Memory Types

Memories are categorized for intelligent retrieval:
- **location** — Travel events
- **character** — NPC encounters
- **item** — Inventory changes
- **event** — General gameplay events
- **lore** — Knowledge and Oracle wisdom

---

## Tips & Strategies

### For New Players

1. **Start as an Explorer** if unsure — balanced stats handle any situation
2. **Visit the Village of Thornwall early** — stock up on supplies before venturing into danger
3. **Save your Healing Potions** for tough fights — don't waste them on Dire Rats
4. **Talk to everyone** — NPCs offer quests, hints, and sometimes free items
5. **Use the Oracle** when stuck — it's always relevant to your current situation
6. **Type creatively** — the AI responds to creative actions, not just "attack" and "move"

### For Combat

1. **Exploit weaknesses** — Fire against Trolls and Hydras, bludgeoning against Skeletons
2. **Know when to run** — if your HP is low against a Level 5+ creature, retreat
3. **The Bandit Captain can be bribed** — sometimes gold is cheaper than HP
4. **Stock up before Dragon's Peak** — full HP, potions, and your best gear
5. **Build success streaks** — consecutive wins track your personal best

### For Exploration

1. **Map the connections** — knowing routes is crucial for efficient travel
2. **The Witch's Hut is a shortcut** — it bridges the Dark Forest and Swamp paths
3. **Don't skip locations** — every area has unique content and rewards
4. **Watch for Twists of Fate** — they can open hidden passages or grant rare artifacts

### For Economy

1. **Sell common items** you don't need — hoard gold, not gray-tier gear
2. **The Wraith Lord drops 100 gold** — high risk, high reward
3. **Invest in gear** at the Market Square before tackling end-game content
4. **Charisma affects bartering** — Rogues and Mages get better prices

---

*LoreEngine — Where Every Choice Writes History* ✨
