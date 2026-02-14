# Overview — Secure AI Proxy for Client Applications

## Introduction

This reference implementation demonstrates how to build a **secure Backend-for-Frontend (BFF) proxy** that mediates between untrusted client applications and AI services like Azure OpenAI. The pattern ensures that API keys, system prompts, and sensitive configuration never leave the server, while clients enjoy rich AI-powered experiences.

## Problem Statement

Modern AI-powered applications face a fundamental security challenge: client applications (mobile apps, SPAs, desktop apps) cannot be trusted with AI provider credentials. Embedding API keys in client code exposes them to extraction, abuse, and financial risk.

The secure proxy pattern solves this by:

- **Isolating credentials** — AI keys and endpoints stay on the server
- **Validating input** — All user input is sanitized before reaching the AI provider
- **Filtering output** — AI responses are sanitized before reaching the client
- **Enforcing quotas** — Per-user rate limiting prevents abuse
- **Auditing usage** — All AI interactions are logged and traceable

## What You'll Learn

After studying this reference implementation, you will understand how to:

1. **Design a secure AI proxy** using the BFF pattern with ASP.NET Core
2. **Implement defense-in-depth** with 20 named security controls
3. **Integrate Azure OpenAI** using Microsoft.Extensions.AI (MEAI)
4. **Support tool calling** with both server-side and client-side tools
5. **Stream AI responses** using Server-Sent Events (SSE)
6. **Manage conversation state** with session isolation and ownership validation
7. **Deploy to production** with proper configuration and monitoring

## Target Audience

This guide is intended for:

- **.NET developers** building AI-powered applications
- **Solution architects** designing secure AI integration patterns
- **Security engineers** evaluating AI application threat models
- **DevOps engineers** deploying AI proxy infrastructure

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Server | ASP.NET Core 10, Minimal APIs | Secure proxy with API endpoints |
| Client | Blazor WebAssembly | Interactive SPA (represents any untrusted client) |
| AI | Microsoft.Extensions.AI, Azure OpenAI | AI provider abstraction |
| Orchestration | .NET Aspire | Local development orchestration |
| Database | SQLite + PostgreSQL (pgvector) | Conversation persistence + vector memory |
| Identity | ASP.NET Core Identity | Authentication and authorization |
| Testing | xUnit, Playwright | Unit, integration, and E2E testing |

## Architecture at a Glance

```
┌─────────────────┐         ┌──────────────────────────┐         ┌─────────────┐
│  Blazor WASM    │  HTTPS  │  ASP.NET Core Server     │  HTTPS  │  Azure      │
│  Client App     │◄──────►│  (Secure AI Proxy)       │◄──────►│  OpenAI     │
│                 │ Bearer  │                          │ API Key │             │
│  - Chat UI      │ Token   │  - Authentication        │         │  - GPT-4o   │
│  - Game UI      │         │  - Input validation      │         │  - Embeddings│
│  - Client tools │         │  - Rate limiting         │         │             │
│                 │         │  - Content filtering     │         │             │
│                 │         │  - Tool execution        │         │             │
│                 │         │  - Session management    │         │             │
└─────────────────┘         └──────────────────────────┘         └─────────────┘
```

The server acts as a **trust boundary** — the client never communicates directly with the AI provider. All credentials, system prompts, and security policies are enforced server-side.

## Sample Application

The reference implementation includes **LoreEngine**, an interactive fiction RPG that demonstrates all proxy capabilities:

- **Streaming narrative** — Real-time story generation via SSE
- **Tool calling** — Server-side game tools (combat, inventory, NPCs) and client-side tools (dice rolling, local storage)
- **Structured output** — Game state management with typed responses
- **Session persistence** — Conversation history across sessions
- **Vector memory** — Long-term story memory using pgvector

## Next Steps

- [Architecture](02-architecture.md) — Understand the system design and trust boundaries
- [Security](03-security.md) — Learn about the 20 defense-in-depth controls
