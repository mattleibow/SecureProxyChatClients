# Testing — Strategy, Patterns, and Coverage

## Overview

The reference implementation uses a four-layer testing strategy that validates security controls, AI integration, game mechanics, and user-facing workflows. All tests use xUnit as the test framework, with concrete test doubles instead of mocking libraries.

## Test Layers

| Layer | Project | Count | Purpose |
|-------|---------|-------|---------|
| Unit | `SecureProxyChatClients.Tests.Unit` | 215 tests | Isolated logic: security, game engine, tools, services |
| Integration | `SecureProxyChatClients.Tests.Integration` | 17 tests | Full Aspire app with real HTTP, auth, sessions |
| E2E | `SecureProxyChatClients.Tests.Playwright` | 30+ tests | Browser-level workflows via Chromium |
| Smoke | `SecureProxyChatClients.Tests.Smoke` | — | Production health verification (scaffold) |

## Unit Tests

Unit tests cover six major areas with 31 test classes.

### Security Controls (33 tests)

Security tests validate each named control in isolation:

- **Content filter** — Verifies removal of `<script>`, `<iframe>`, event handlers, and `javascript:` URIs
- **Input validator** — Tests role stripping (S1), prompt injection detection (S3), length enforcement (S4), tool allowlisting (S5), and HTML injection blocking (S6)
- **Exception handler** — Confirms error detail suppression in production responses

### Game Engine (66 tests)

Game engine tests cover the complete RPG system:

- **Game tools** — Dice mechanics, inventory operations, health/gold/XP modification, NPC generation
- **Tool registry** — Registration, lookup, and state mutation via `ApplyToolResult`
- **Achievements** — Trigger conditions across combat, exploration, social, wealth, and progression categories
- **Bestiary** — Creature data, level-appropriate filtering, encounter generation
- **World map** — Location connections, ASCII map rendering, movement tracking
- **Twist of Fate** — Random event generation and category filtering

### AI and Tools (52 tests)

AI tests verify the provider abstraction and tool execution:

- **Fake provider** — Queue-based responses, streaming simulation, tool call generation, keyword detection
- **Observability wrapper** — Logging delegation
- **Server/client tool registries** — Tool registration and lookup
- **Individual tools** — Dice mechanics with stat modifiers, scene generation

### Agents and Services (32 tests)

Service tests cover orchestration and persistence:

- **Lore agent** — System prompt prepending, response extraction
- **Writers room** — Round-robin multi-agent discussion orchestration
- **System prompt service** — Dynamic prompt generation with player state
- **Story state** — Graph operations for narrative tracking
- **Conversation store** — EF Core persistence with in-memory SQLite

## Test Patterns

### Concrete Test Doubles

The project uses no mocking libraries (Moq, NSubstitute, etc.). Instead, it relies on purpose-built test doubles:

- **FakeChatClient** — Implements `IChatClient` with a response queue, keyword matching, and tool call simulation
- **In-memory databases** — SQLite in-memory mode for EF Core tests

This approach produces tests that are easier to read and more resistant to refactoring.

### Factory Methods

Test classes use factory methods to construct validated objects with sensible defaults:

```csharp
private static InputValidator CreateValidator(SecurityOptions? options = null)
```

This pattern keeps individual test methods focused on the behavior under test.

### Parameterized Tests

`[Theory]` with `[InlineData]` covers multiple input variations for security controls, tool parameters, and game mechanics without duplicating test structure.

## Integration Tests

Integration tests start the complete distributed application using .NET Aspire's `DistributedApplicationTestingBuilder`.

### Setup

Each test class:

1. Builds the AppHost with `AI:Provider=Fake` to eliminate external dependencies
2. Starts all resources and waits for the server to report healthy
3. Creates an HTTP client connected to the server resource
4. Registers a unique test user (GUID-based email) for isolation

### Coverage Areas (5 test classes)

| Class | Tests | Verifies |
|-------|-------|----------|
| Auth endpoints | 3 | Register/login flow, duplicate rejection, wrong password denial |
| Chat endpoints | 4 | Message send/receive, SSE streaming, 401 on unauthenticated, 400 on invalid input |
| Play endpoints | 4 | Game actions, tool execution through the full pipeline |
| Session endpoints | 4 | Create, list, history retrieval, persistence |
| Session isolation | 2 | User A cannot see or access User B's sessions |

### User Isolation

Each test creates a unique user with a GUID-based email address. This prevents test interference and validates that session ownership is enforced at the database level.

## Playwright E2E Tests

E2E tests use Playwright to drive a headless Chromium browser against the running application.

### Fixture Architecture

The `AspirePlaywrightFixture` manages the full test lifecycle:

1. Starts the server on port 5167 and client on port 5053
2. Detects if ports are already in use (supports running against an existing instance)
3. Launches headless Chromium
4. Performs WASM warmup by loading the client once to initialize the .NET IL runtime

All Playwright test classes share this fixture via xUnit's `[Collection("Aspire")]` attribute.

### Coverage Areas (6 test classes)

| Class | Tests | Covers |
|-------|-------|--------|
| Auth flow | 5 | Login form, valid/invalid credentials, unauthenticated state |
| Chat flow | 5+ | Message sending, SSE streaming updates in UI |
| Play flow | 7 | Game interactions, tool calls, state updates |
| Navigation | 5 | Page routing between views |
| Session flow | 3 | Session persistence, logout behavior |
| Writers room | 5 | Multi-agent discussion UI |

### Selector Strategy

Tests use `data-testid` selectors exclusively, decoupling tests from CSS classes and DOM structure. This prevents test breakage from UI styling changes.

## Running Tests

```bash
# Unit tests only
dotnet test tests/SecureProxyChatClients.Tests.Unit

# Integration tests (requires Docker for Aspire resources)
dotnet test tests/SecureProxyChatClients.Tests.Integration

# Playwright E2E tests (requires Playwright browsers installed)
dotnet test tests/SecureProxyChatClients.Tests.Playwright
```

Integration and Playwright tests set `AI__Provider=Fake` automatically.

## CI/CD Integration

The GitHub Actions workflow runs on push and pull request to `main`:

1. Restore → Build (Release configuration) → Unit tests → Integration tests
2. Test results are published as TRX artifacts
3. Integration tests run with `DOTNET_ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` for the CI environment

## Next Steps

- [Deployment](08-deployment.md) — Production deployment and configuration
- [Extending](09-extending.md) — Adding new tools, providers, and security policies
