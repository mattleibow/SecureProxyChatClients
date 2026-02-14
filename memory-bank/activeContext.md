# Active Context

> **Last updated**: 2026-02-18

## Current Status

**ALL 11 PHASES COMPLETE + COMBAT TRACKER + MARKDOWN RENDERING + TEST STABILITY** ✅

232 unit tests + 4 integration + 30 Playwright = 266 tests. Combat encounter system with visual tracker. Markdown rendering in story text.

## Current Focus

**Feature Expansion & Polish** — Continuously improving the game and reference sample:
- Combat tracker UI with enemy/player display, turn counter, HP bars
- Markdown rendering in story messages (bold, italic, line breaks)
- Playwright test stability improvements (fixture reuse, WASM warm-up, StartOver bug fix)
- System prompts hidden from story area (cleaner UX)

## Architecture Summary

```
Blazor WASM Client          Server (Secure Augmenting Proxy)          Azure OpenAI
┌─────────────────┐         ┌──────────────────────────────┐         ┌───────────┐
│ Login            │ Bearer  │ Identity + Rate Limiting      │         │           │
│ Play Mode ⚔️     │───────>│ Input Validation + Filtering  │───────>│ Chat API  │
│ Chat / Writers   │ Token   │ System Prompt + Game Engine   │ API Key │           │
│ Journal / Bestiary│<───────│ Server Tool Execution         │<───────│           │
│ Client Tools     │         │ Session + Vector Store        │         │           │
└─────────────────┘         └──────────────────────────────┘         └───────────┘
                                       │
                                  PostgreSQL + pgvector
                                  (story memory / embeddings)
```

## What's Next

- Stream tool execution in SSE (game events appear in real-time during AI response)
- Tool result validation on server (security showcase for the proxy pattern)
- Turn-based combat state machine (track combat rounds, creature HP)
- Keep growing automated tests
- Deploy to Azure (future)
- Add Entra ID authentication (deferred)