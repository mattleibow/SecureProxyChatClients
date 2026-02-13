# Proposal: Secure AI Proxy for .NET MAUI Apps

> **Authors**: MAUI AI Team
> **Date**: February 2026
> **Status**: For team/management review — seeking alignment on investigation priorities
> **Context**: Based on independent research analyzed by three domain perspectives (strategy, engineering investment, and security architecture)

---

## Problem Statement

MAUI apps that want to use cloud AI services face a fundamental security problem: **the client binary cannot be trusted**. API keys embedded in mobile apps can be extracted. Auth tokens can be stolen. Prompts, tools, and conversation history can be manipulated by a decompiled or instrumented app. Every major production AI app (ChatGPT, Copilot, Gemini) solves this with a server-mediated architecture — we need one too.

Today, MAUI's AI sample (`src/AI/`) connects directly to OpenAI with no proxy layer. This is fine for demos but unacceptable for production.

---

## What the Research Tells Us

We commissioned a deep research investigation covering proxy architectures, security models, industry patterns, AG-UI protocol, MEAI integration, and novel approaches. We then had three independent analyses performed:

- **Strategic analysis** (architecture & product direction)
- **Investment analysis** (what to build, when, and why)
- **Security analysis** (threat modeling, attack surface, trust boundaries)

### Key Findings (Cross-Model Consensus)

All three analyses converge on five findings:

**1. The industry has settled on "thin client, thick server" — this isn't a debate.**
Every production mobile AI app uses a server-owned backend. The client is a rendering engine that sends user input and displays results. No API keys, no prompt construction, no uncontrolled tool execution on the client. This is the only pattern that has survived contact with real adversaries at scale.

**2. AG-UI is powerful but ships without security — adopting it raw is dangerous.**
The Microsoft Agent Framework's AG-UI protocol provides streaming, frontend tools, human-in-the-loop, and shared state — all built on `IChatClient`. But it has **zero** built-in authentication, rate limiting, or input validation. Its own documentation says "never expose to untrusted clients." Adopting AG-UI without a hardening layer creates a larger attack surface than a simple proxy, because its rich feature set (event streaming, frontend tools, stateful sessions) offers more vectors for exploitation.

**3. On-device tool verification is fundamentally unsolved.**
If the server asks the client to "get GPS location" via a frontend tool, there is no way to verify the client returned real data. A compromised app can return fake coordinates, fabricated images, or spoofed sensor data. The only mitigations today are process controls (require user confirmation, limit to non-destructive actions, log everything). Hardware attestation (TEE, Secure Enclave) is theoretically possible but impractical in 2026. **Frontend tool results must be treated as advisory, not authoritative.**

**4. Conversation state location is a hidden security decision.**
Client-managed state (sending full message history each request) enables history tampering — a malicious client can edit, drop, or fabricate prior messages. Server-managed state prevents this but requires infrastructure. Most developers will default to client-managed because it's simpler, accidentally opening a prompt injection vector.

**5. The MEAI `IChatClient` model can be preserved across the proxy boundary.**
A `ProxyChatClient` implementing `IChatClient` can transparently forward to the server. UI-focused middleware (buffering, local caching) runs client-side; security-critical middleware (function invocation, content filtering, rate limiting) runs server-side. The existing `NonFunctionInvokingChatClient` already solves the double-execution problem that arises from this split.

### What the Research Missed (Identified by Analysts)

| Gap | Source | Impact |
|-----|--------|--------|
| Indirect prompt injection via server-side tools (e.g., web search fetches malicious content) | Security analysis | Could bypass all client-side mitigations |
| Supply chain risk from AG-UI's ~25 NuGet package dependencies | Security analysis | Compromise of one preview package = server code execution |
| Latency impact of proxy hops + validation + content filtering | Strategic analysis | Never quantified; 500ms delay breaks chat UX |
| LLM output sanitization (Markdown/HTML → XSS) | Security analysis | LLM could be tricked into outputting malicious scripts |
| Man-in-the-Middle without certificate pinning | Security analysis | Token theft on hostile networks |
| System prompt / RAG knowledge base extraction via repeated queries | Security analysis | Attacker reconstructs proprietary prompt engineering |

---

## Proposed Investigation Options

Based on the research and analysis, we propose the following investigation workstreams. **No decisions have been made** — we are seeking alignment on which to prioritize.

### Option A: Ship a Reference BFF Architecture (Highest Confidence)

**What**: Build a production-ready Backend-for-Frontend reference implementation.

| Component | Description |
|-----------|-------------|
| **Server** | ASP.NET Core BFF that owns AI keys, constructs prompts, whitelists tools, enforces rate limits, runs content safety |
| **Client** | MAUI `ProxyChatClient` (implements `IChatClient`) with SSE streaming + `BufferedChatClient` |
| **Auth** | OAuth/OIDC with short-lived tokens, per-user quotas |
| **State** | Server-managed conversation sessions (Redis/in-memory with signed session tokens) |
| **Gateway** | Azure API Management GenAI Gateway policies for token counting, quota enforcement, content moderation |
| **Templates** | `dotnet new maui-ai-proxy` solution template (MAUI + ASP.NET Core + APIM config) |
| **Tools** | Server-side only in v1; frontend tools deferred until trust model is established |

**Why**: This is what the industry does. It's the only architecture ranked "acceptable" by the security analysis. It preserves `IChatClient` on both sides of the boundary, and the MEAI middleware pipeline works naturally.

**Investment**: Medium — estimated 4-6 weeks for reference architecture + template + sample app.

**Risk**: Low technical risk (proven pattern). Risk of being "boring" — doesn't differentiate MAUI.

### Option B: AG-UI with Hardened Trusted Frontend Server (Highest Potential)

**What**: Adopt the Agent Framework's AG-UI protocol but wrap it in a security layer.

| Component | Description |
|-----------|-------------|
| **Client** | `AGUIChatClient` (already implements `IChatClient`) in MAUI app |
| **Trusted Frontend** | ASP.NET Core middleware between MAUI and AG-UI agent server — authenticates, validates events, enforces role stripping, rate limits |
| **Agent Server** | Standard AG-UI agent with `MapAGUI()`, server-defined system prompt and backend tools |
| **Frontend Tools** | Allowed but sandboxed — server-initiated only, results treated as untrusted input, validated before use |
| **Security Layer** | Event-level validation, tool allowlisting, message role enforcement, content filtering |

**Why**: AG-UI provides streaming, frontend tools, human-in-the-loop, and shared state out of the box. `AGUIChatClient` already implements `IChatClient`. The security layer we'd build is what the AG-UI security doc explicitly recommends but nobody has built yet. Shipping it would differentiate MAUI and benefit the broader Agent Framework ecosystem.

**Investment**: High — estimated 6-10 weeks. Requires cross-team collaboration with Agent Framework team. Depends on preview packages stabilizing.

**Risk**: 
- AG-UI is prerelease — breaking changes likely in 12-18 months
- Cross-team dependency on Agent Framework team's priorities
- ~25 NuGet packages — mobile binary size impact unknown
- Supply chain risk from preview packages

### Option C: Graduated Security Model (Most Flexible)

**What**: Build a layered system where security modes can be toggled via configuration.

| Level | Security Mode | What Changes |
|-------|--------------|--------------|
| 0 | Direct (dev only) | No proxy — same as today's sample |
| 1 | Passthrough + Auth | Proxy hides API key, adds JWT auth + rate limiting |
| 2 | Intercepting | Proxy strips system messages, enforces token limits, runs content moderation |
| 3 | Contract | Proxy validates against schema (allowed tools, response format, max messages) |
| 4 | BFF | Server owns prompts entirely — client sends domain-specific requests |

**Why**: Developers can start at Level 1 for prototyping and graduate to Level 3-4 for production. This matches how teams actually work — they don't start with maximum security on day one.

**Investment**: High — building all levels is significant. Could ship Level 1-2 first, then iterate.

**Risk**: 
- Developers may stay at Level 1 forever ("it works, ship it")
- More surface area to maintain and document
- Contract mode (Level 3) requires schema design tooling that doesn't exist

---

## What We Can Decide Now vs. What Needs More Work

### Ready to Decide

| Decision | Recommendation Basis |
|----------|---------------------|
| "Never embed API keys in client" must be enforced | Universal industry consensus |
| Server-managed conversation state by default | Security analysis — client state enables history tampering |
| Ship a `ProxyChatClient` implementing `IChatClient` | All three analyses confirm MEAI model can be preserved |
| Role stripping middleware (force all client messages to `role: user`) | Security analysis — prevents system message injection |
| Certificate pinning guidance for MAUI apps | Security analysis — prevents MitM token theft |
| LLM output sanitization before rendering | Security analysis — prevents prompt-to-XSS |

### Needs Investigation

| Question | Why It Matters | Suggested Action |
|----------|---------------|-----------------|
| AG-UI mobile binary size impact | Could be a blocker for adoption | Measure: add `Microsoft.Agents.AI.AGUI` to a MAUI app, compare APK/IPA size |
| AG-UI protocol stability timeline | Determines if we can depend on it | Meet with Agent Framework team — ask about 1.0 roadmap |
| SSE vs WebSocket vs gRPC for mobile streaming | Affects latency, battery, reliability | Benchmark all three with simulated AI responses on iOS/Android |
| Azure APIM + AG-UI integration | Does APIM support SSE passthrough? | Test: put APIM in front of an AG-UI endpoint |
| Frontend tool trust model | Can we use App Attest / Play Integrity? | Prototype: device attestation for GPS/camera tool results |
| Content moderation latency impact | Could break chat UX | Measure: Azure Content Safety round-trip in proxy pipeline |

---

## Proposed Next Steps

1. **Align on primary investigation option** (A, B, or C) — this meeting
2. **Spike on AG-UI footprint** — 1 engineer, 2 days — measure binary size + dependency tree on MAUI
3. **Build Option A reference architecture** — regardless of which option we choose long-term, the BFF is the foundation all options need
4. **Schedule meeting with Agent Framework team** — understand AG-UI 1.0 roadmap, discuss MAUI-specific needs, explore contributing security middleware
5. **Prototype `ProxyChatClient`** — validate that `IChatClient` works cleanly across the proxy boundary with streaming

---

## Appendix A: Analysis Sources

Three independent analyses were performed on the research output:

| Perspective | Model | Focus | Key Insight |
|-------------|-------|-------|-------------|
| Strategic | Claude Opus 4.5 | Architecture & product direction | "Ship a secure proxy SDK, not raw AG-UI access" |
| Investment | GPT-5 | Build vs. investigate vs. defer | "BFF + APIM now; AG-UI pilot in parallel" |
| Security | Gemini 3 Pro | Threat modeling & attack surface | "Passthrough is security theater; BFF is the only acceptable architecture" |

Full analysis documents available on request.

## Appendix B: References

### Microsoft Documentation
- [AG-UI Security Considerations](https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/security-considerations)
- [Azure OpenAI Gateway Architecture](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/azure-openai-gateway-guide)
- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [IChatClient Interface](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient)
- [Backend for Frontend Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/backends-for-frontends)
- [Azure API Management AI Gateway](https://learn.microsoft.com/en-us/azure/api-management/genai-gateway-capabilities)

### Agent Framework
- [Microsoft Agent Framework .NET (GitHub)](https://github.com/microsoft/agent-framework/tree/main/dotnet)
- [AG-UI .NET Samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/AGUI)
- [AG-UI Protocol (GitHub)](https://github.com/ag-ui-protocol/ag-ui/)

### External
- [AG-UI Research Paper (ResearchGate)](https://www.researchgate.net/profile/Paul-Pajo/publication/391692988)
- [OpenAI Security Best Practices](https://developers.openai.com/apps-sdk/guides/security-privacy)
- [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)
- [OpenAI-DotNet-Proxy](https://rageagainstthepixel.github.io/OpenAI-DotNet/OpenAI-DotNet-Proxy/Readme.html)

### Existing MAUI Code
- `src/AI/src/Essentials.AI/` — Current MAUI AI abstractions
- `src/AI/samples/Essentials.AI.Sample/` — Direct OpenAI sample (no proxy)
- `src/AI/samples/Essentials.AI.Sample/Services/BufferedChatClient.cs` — Mobile streaming middleware
- `src/AI/samples/Essentials.AI.Sample/AI/NonFunctionInvokingChatClient.cs` — Tool execution dedup