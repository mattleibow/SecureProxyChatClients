# LoreEngine — *The Writer's Room*

> **Created**: 2026-02-13
> **Status**: v1 game concept for SecureProxyChatClients reference sample
> **Related**: `docs/plan.md` (reference sample architecture & requirements)

## Concept

You're the **Creative Director** of an interactive fiction studio. You have a team of AI agents — your "Writer's Room" — who collaborate in a group chat to build your story. You pitch ideas, they debate, draft, challenge each other, and refine. You make the final calls.

**Tagline**: Your AI writing team. Your story. Your rules.

---

## Why This App?

LoreEngine was chosen as the showcase app because every infrastructure capability maps naturally to a game feature:

| Infrastructure Capability | Game Feature |
|--------------------------|--------------|
| Streaming (SSE) | Prose generation streams word-by-word |
| Server-side tools (AIFunction) | GenerateScene, CreateCharacter, AnalyzeStory, SuggestTwist |
| Client-side tools (AIFunction) | GetStoryGraph, SearchStory, SaveStoryState, RollDice, GetWorldRules |
| Structured output | Scene, Character, StoryAnalysis typed schemas |
| Authentication | Must log in to create stories |
| Sessions | Story + conversation persists across sessions |
| Multi-agent orchestration | Writer's Room = Group Chat pattern (on client) |
| Security (role stripping) | Prompt injection could corrupt the story |

---

## Architecture: Agents on the Client

All agents run in **Blazor WASM** (the client). The server is a **secure augmenting proxy** — it authenticates requests, enforces security policies, executes server-side tools, filters content, and enriches client requests before forwarding to Azure OpenAI. It has no knowledge of agents or game logic.

```
Blazor WASM (Client — separate app)     ASP.NET Core (Server — separate app)
┌─────────────────────────┐    CORS     ┌────────────────────┐
│ GroupChatOrchestrator    │◄──────────►│ Secure Augmenting   │
│ ├─ Storyteller ──────────┼─IChatClient┼─→ POST /api/chat ──┼─→ Azure OpenAI
│ ├─ Critic ───────────────┼─IChatClient┼─→ (same endpoint) ─┼─→ Azure OpenAI
│ ├─ Archivist ────────────┼─IChatClient┼─→ (same endpoint) ─┼─→ Azure OpenAI
│                          │            │                    │
│ Client Tools (local):    │            │ Server Tools:       │
│ - GetStoryGraph          │            │ - GenerateScene     │
│ - SearchStory            │            │ - CreateCharacter   │
│ - SaveStoryState         │            │ - AnalyzeStory      │
│ - RollDice               │            │ - SuggestTwist      │
│ - GetWorldRules          │            │                    │
│                          │            │ Identity UI (reg)   │
│ Login.razor (login only) │            │ Auth + Security     │
│ Story State (IndexedDB)  │            │ Rate Limiting       │
└─────────────────────────┘            └────────────────────┘
```

---

## The Writer's Room (3 Agents)

Three specialized agents collaborate via Group Chat orchestration. All run in Blazor WASM, all use `ProxyChatClient` → server → Azure OpenAI for AI completions.

| Agent | Role | Personality |
|-------|------|-------------|
| 📖 **Storyteller** | Prose — descriptions, narration, characters, dialog | Eloquent, dramatic, loves vivid imagery |
| 🔍 **Critic** | Quality — plot holes, clichés, pacing issues, world rule violations | Blunt, skeptical, "this doesn't make sense because..." |
| 📚 **Archivist** | Memory — tracks entities, timeline, world state, continuity | Precise, never forgets, "in Chapter 2 you said..." |

### Orchestration: Group Chat

Using Agent Framework's `GroupChatOrchestrator` with a custom `WritersRoomStrategy`:

```csharp
var orchestrator = new GroupChatOrchestrator(
    [storyteller, critic, archivist],
    strategy: new WritersRoomStrategy()
);
```

When the user pitches an idea, each agent contributes their perspective. The user sees the debate and makes final decisions.

---

## Creation Mode (v1 Only)

The Writer's Room builds your story through conversation.

```
┌─────────────────────────────────────────────────┐
│  1. PITCH — You describe what you want          │
│     "I want a noir detective story set in 1940s │
│      Chicago with a femme fatale twist"         │
├─────────────────────────────────────────────────┤
│  2. WRITER'S ROOM — Agents discuss (Group Chat) │
│     Storyteller: "Rain-slicked streets, jazz.." │
│     Critic: "Femme fatale trope needs a         │
│              subversion or it's cliché"         │
│     Archivist: "Noted: setting=Chicago, era=40s"│
├─────────────────────────────────────────────────┤
│  3. DECIDE — You pick what you like, give       │
│     direction. "Make her the real detective."   │
├─────────────────────────────────────────────────┤
│  4. DRAFT — Storyteller generates content,      │
│     Critic reviews, Archivist records state     │
└─────────────────────────────────────────────────┘
```

---

## Tools

### Server Tools (4 — executed on server, AI generation)

| Tool | What It Does | Why Server |
|------|-------------|-----------|
| `GenerateScene(prompt, constraints)` | Creates a full scene with description, characters, choices | Needs GPT-4o creativity |
| `CreateCharacter(brief)` | Generates complete character with backstory, voice, arc | Complex creative generation |
| `AnalyzeStory()` | Story analysis: pacing, plot holes, dead ends | Complex cross-reference |
| `SuggestTwist(currentPlot)` | Generates unexpected but consistent plot twists | Creative reasoning |

### Client Tools (5 — executed locally in Blazor WASM)

| Tool | Called By | What It Does | Why Local |
|------|----------|-------------|-----------|
| `GetStoryGraph` | Archivist, Critic | Returns full story structure (scenes, branches, connections) | Story data stays in browser (IndexedDB) |
| `SearchStory(query)` | Archivist | Search local story content by query | Fast local search, no network |
| `SaveStoryState(entity)` | Archivist | Persists updated story graph to IndexedDB | Local persistence |
| `RollDice(sides)` | Storyteller | Random outcomes for story events | Client-side randomness |
| `GetWorldRules` | Critic, Archivist | Returns established world rules (magic system, physics) | Consistency reference |

### Scene-Scoped Context

The client owns the full story graph in IndexedDB. When making server requests, the client sends only scene-scoped context:
- Current scene + connected scenes (1-2 hops)
- Active characters in those scenes
- Relevant world rules
- Brief story summary (~200 words, auto-generated)

This keeps request sizes small while giving server tools enough context.

---

## Structured Output Schemas

| Schema | Fields | Used By |
|--------|--------|---------|
| `Scene` | id, description, characters[], choices[], mood, location | GenerateScene |
| `Character` | name, traits[], backstory, dialogStyle, arc, relationships{} | CreateCharacter |
| `StoryGraph` | scenes[], characters[], timeline[], worldRules | GetStoryGraph, SaveStoryState |
| `StoryAnalysis` | plotHoles[], deadEnds[], pacingScore, suggestions[] | AnalyzeStory |

---

## Example Session

```
You: Start a new story. Dark fantasy. A thief who steals memories.

[Writer's Room Discussion]
📖 Storyteller: "Opening scene: a rain-drenched market, the thief's fingers
   brush a merchant's wrist, and suddenly they know the merchant's daughter's
   name. The power has a cost — each stolen memory replaces one of their own."
🔍 Critic: "Memory-theft is well-trodden in fantasy. The 'replacing your own
   memories' angle is the differentiator — lean into the identity crisis hard."
📚 Archivist: "New story initialized. Setting: dark fantasy. Protagonist:
   memory thief. Core mechanic: memory exchange. No characters defined yet."

You: I love the identity crisis angle. Make the first chapter.

[Storyteller calls GenerateScene → server generates scene]
[Archivist calls SaveStoryState → saves to IndexedDB]
[Critic calls GetWorldRules → checks consistency]

📖 Storyteller: [streaming word-by-word]
"The market is a living thing at dusk — a tangle of voices, colors, and the
wet-stone smell of rain on cobbles. You pull your hood lower..."
```

---

## Future Enhancements (Phase 12+)

Not in v1 scope:
- **Play Mode** — Switch from Creator to Player, hybrid input (structured choices + free-form text)
- **More agents** — Add Weaver (plot branching) + CastDirector (character focus) for 5-agent Writer's Room
- **Local agents** — Critic + Archivist on Ollama for offline/privacy
- **Export** — Stories as playable HTML or shareable links
- **Progression** — Unlock tools/agents as you create more content
