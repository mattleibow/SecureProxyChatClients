# Product Context

> **Last updated**: 2026-02-14

## What Is This?

SecureProxyChatClients is a reference implementation demonstrating how to securely connect untrusted client applications to Azure OpenAI through a secure augmenting proxy (BFF pattern). It proves that MEAI's `IChatClient` abstraction works cleanly across a proxy boundary with full security, streaming, tool calling, structured output, and multi-agent orchestration support.

### Showcase App: **LoreEngine** — *The Writer's Room*

An interactive fiction builder. A team of 3 AI agents (Storyteller, Critic, Archivist — the "Writer's Room") collaborate via Group Chat on the **client** to build branching stories. **Creation Mode** only in v1 — agents debate and build. Play Mode deferred to Phase 12+.

## Why Does It Exist?

The proposal (`docs/proposal.md`) identified that every production AI app uses a server-mediated architecture. MAUI apps (and any client apps) cannot safely embed API keys or trust client-provided messages. This sample demonstrates the secure pattern with .NET Aspire orchestration.

## Who Is It For?

- .NET developers building AI-powered client apps (MAUI, console, Blazor, WPF)
- Teams evaluating secure AI architectures for production
- The MAUI AI team as a foundation for official guidance

## Key Capabilities

1. **Secure augmenting proxy** — server authenticates, rate-limits, filters, enriches requests; client never sees OpenAI credentials
2. **Agents on client** — `GroupChatOrchestrator` + 3 `ChatClientAgent` instances (Storyteller, Critic, Archivist) run in Blazor WASM
3. **Streaming** — SSE-formatted stream over HttpClient from server to client via `IAsyncEnumerable`
4. **Server-side tools** — AIFunctions on server (GenerateScene, CreateCharacter, AnalyzeStory, SuggestTwist)
5. **Client-side tools** — AIFunctions on client (GetStoryGraph, SearchStory, SaveStoryState, RollDice, GetWorldRules), results treated as untrusted
6. **Split state model** — client authoritative for context window (`messages`); server persists history for audit/resume; story data in client IndexedDB
7. **Auth** — ASP.NET Core Identity API endpoints for v1; Microsoft Entra ID additive in Phase 12+
8. **Structured output** — typed Scene, Character, StoryAnalysis schemas
9. **Aspire orchestration** — single F5 launches server + WASM as separate apps on separate ports
10. **Separate apps** — server (web app with Identity UI) and client (plain WASM) on separate origins; CORS + bearer tokens
