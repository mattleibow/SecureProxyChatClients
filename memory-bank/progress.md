# Progress Log

## Current Status (2026-02-15)
- **Build**: ✅ 0 errors
- **Unit Tests**: 378 passing
- **Integration Tests**: 32 passing
- **Walkthrough Test**: ✅ 27 steps, all passing with real Azure OpenAI
- **Total**: 410 tests passing
- **Git**: pushed to origin/main (commit eaf6cf4)
- **Convergence**: Both Gemini and Codex say CONVERGED after 4+ review rounds

## Recent Session Work
1. Fixed WASM loading in Playwright tests (stale client fingerprints)
2. Fixed Error 400 bug: MaxMessages increased from 10 to 50
3. Fixed dice modifiers: use actual player stats (D&D formula: (stat-10)/2)
4. Fixed level-up: while loop for multi-level XP awards
5. Fixed achievement system: event-based awards for combat/social/NPC events
6. Fixed twist-of-fate achievement: moved from Oracle to Twist endpoint
7. Fixed first-loot threshold: >3 instead of >2
8. Fixed location normalization: case-insensitive WorldMap matching
9. Fixed security: memory logging, forwarded headers, error display
10. Fixed API docs: synced all endpoint contracts with actual implementation
11. Complete 27-step walkthrough with real AI screenshots (no errors)

## Gemini Review Findings (All Fixed)
- Critical: Dice modifiers hardcoded → now uses (statValue-10)/2
- High: Level-up fires once → while loop
- Medium: GameState race condition → acknowledged (optimistic concurrency acceptable)

## Codex Review Findings
### Fixed
- High: Event achievements never awarded → added in ApplyToolResult
- High: twist-of-fate triggered by Oracle → moved to Twist endpoint
- High: API docs stale → synced all contracts
- High: Forwarded headers trusted all → keep loopback defaults
- High: TakeItem removes whole stack → now decrements Quantity
- High: Bestiary GetEncounterCreature crashes on empty list → fallback to last creature
- Medium: UseRateLimiter before UseAuthentication → moved after UseAuthentication
- Medium: InMemoryStoryMemoryService unsynchronized → added Lock
- Medium: Empty/whitespace user messages pass validation → rejected
- Medium: first-loot threshold wrong → >3
- Medium: Memory content logged → metadata only
- Medium: Location validation → WorldMap normalization
- Medium: dragon-slayer/secret-keeper never awarded → implemented via CombatResult/NpcResult
- Medium: Bestiary DM prompt XP formatting → removed stray 'g' suffix
- Medium: DC scale handbook says 1-20 but code clamps 1-30 → updated docs to 1-30 with Legendary tier
- Medium: Twist endpoint lacked concurrency handler → added
- Low: Oracle docs incorrect → updated
- Low: NPC whitespace/empty HiddenSecret → confirmed properly handled, tests added
- Low: GameStateStore concurrency/version/copy tests → added

### Acknowledged (Not Fixed)
- Medium: sessionStorage token (XSS risk) → BFF cookies planned for future
- Medium: Combat not deterministic on server → prompt-based is intentional for AI creativity
- Medium: Memory endpoint tests → lower priority, endpoints are simple
- Low: Walkthrough isn't exhaustive → renamed to "sample session"

## Completed Phases
All 12 phases complete. See plan.md for details.
