using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SecureProxyChatClients.Server.Data;
using SecureProxyChatClients.Server.GameEngine;
using SecureProxyChatClients.Server.Security;
using SecureProxyChatClients.Server.Services;
using SecureProxyChatClients.Server.VectorStore;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Server.Endpoints;

public static class PlayEndpoints
{
    private const int MaxToolCallRounds = 8;
    private const int MaxToolResultLength = 32_768;

    private const string OracleSystemPrompt = """
        You are the Oracle of LoreEngine — an ancient, all-seeing entity who speaks in cryptic riddles.
        
        RULES:
        - Never give direct answers. Always speak in riddles, metaphors, and prophecy.
        - Reference the player's current game state when forming hints.
        - Keep responses to 2-3 sentences maximum.
        - Use archaic language ("Thou", "seeketh", "the path doth wind...")
        - Occasionally reference creatures from the bestiary as omens.
        - If the player seems stuck, give a slightly more direct hint wrapped in mysticism.
        - Always begin with "The Oracle speaks:" followed by the prophecy.
        - End with a cryptic farewell like "The mists close..." or "The vision fades..."
        """;

    private const string DmSystemPrompt = """
        You are the Dungeon Master (DM) of LoreEngine, an interactive fiction game.
        
        RULES:
        - You narrate the story in second person ("You enter the tavern...")
        - Keep descriptions vivid but concise (2-4 paragraphs per scene)
        - Always end with a prompt for player action ("What do you do?")
        - Use the game tools to enforce mechanics: roll dice for risky actions, track items, manage health
        - Start each NEW scene with an ASCII art block (5-10 lines) depicting the location
        - Wrap ASCII art in triple backticks with 'ascii' label: ```ascii\n...\n```
        - Never reveal NPC hidden secrets until the player discovers them through gameplay
        - Be fair but challenging. Not every action succeeds.
        - When a player takes damage, call ModifyHealth. When they find items, call GiveItem.
        - For any risky action (combat, stealth, persuasion), call RollCheck first
        - When calling RollCheck, ALWAYS pass the player's actual stat value (from their stats in the game context) as the statValue parameter
        - Track the player's location with MovePlayer when they travel
        - Award XP for clever solutions and completing objectives
        
        COMBAT RULES:
        - Use creatures from the AVAILABLE CREATURES list appropriate to the player's level
        - Describe the creature vividly using its emoji and description
        - Each combat round: player acts, then creature acts
        - Use RollCheck for player attacks (stat: relevant weapon stat, DC: creature's AttackDc, statValue: player's actual stat value)
        - On hit, describe damage to the creature (track in narrative). On miss, the creature attacks.
        - Creature attacks: RollCheck for player defense (stat: dexterity, DC: 10 + creature level, statValue: player's DEX)
        - On failed defense, call ModifyHealth on the player (negative, amount: creature's Damage)
        - Award XP and Gold on creature defeat using AwardExperience and ModifyGold
        - IMPORTANT: When a creature is defeated, call RecordCombatWin(creatureName) to unlock achievements
        - Reference creature weaknesses — give hints to observant players
        
        TONE: Dark fantasy with moments of humor. Think Discworld meets Dark Souls.
        """;

    public static RouteGroupBuilder MapPlayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/play")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = IdentityConstants.BearerScheme
            })
            .RequireRateLimiting("chat");

        group.MapPost("/", HandlePlayAsync)
            .WithName("Play")
            .WithSummary("Processes a game turn with tool execution and state tracking")
            .Produces<PlayResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/stream", HandlePlayStreamAsync)
            .WithName("PlayStream")
            .WithSummary("Streams a game turn via SSE with real-time tool events")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/state", GetPlayerStateAsync)
            .WithName("GetPlayerState")
            .WithSummary("Gets the current player state");

        group.MapPost("/new-game", StartNewGameAsync)
            .WithName("StartNewGame")
            .WithSummary("Creates a new character and starts a fresh game");

        group.MapGet("/twist", GetTwistOfFateAsync)
            .WithName("GetTwistOfFate")
            .WithSummary("Gets a random twist of fate event");

        group.MapGet("/achievements", GetAchievementsAsync)
            .WithName("GetAchievements")
            .WithSummary("Gets all achievements with unlock status");

        group.MapPost("/oracle", ConsultOracleAsync)
            .WithName("ConsultOracle")
            .WithSummary("Consults the Oracle for cryptic hints");

        group.MapGet("/map", GetWorldMapAsync)
            .WithName("GetWorldMap")
            .WithSummary("Gets the ASCII world map with exploration progress");

        group.MapGet("/encounter", GetRandomEncounterAsync)
            .WithName("GetRandomEncounter")
            .WithSummary("Generates a random combat encounter");

        return group;
    }

    private static async Task<IResult> GetPlayerStateAsync(
        HttpContext httpContext,
        IGameStateStore gameStateStore,
        CancellationToken ct)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var state = await gameStateStore.GetOrCreatePlayerStateAsync(userId, ct);
        return Results.Ok(state);
    }

    private static async Task<IResult> GetTwistOfFateAsync(
        HttpContext httpContext,
        IGameStateStore gameStateStore,
        CancellationToken ct)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var twist = TwistOfFate.GetRandomTwist();

        // Award the "twist-of-fate" achievement
        var state = await gameStateStore.GetOrCreatePlayerStateAsync(userId, ct);
        if (!state.UnlockedAchievements.Contains("twist-of-fate"))
        {
            state.UnlockedAchievements.Add("twist-of-fate");
            await gameStateStore.SavePlayerStateAsync(userId, state, ct);
        }

        return Results.Ok(new { twist.Title, twist.Prompt, twist.Emoji, twist.Category });
    }

    private static async Task<IResult> GetAchievementsAsync(
        HttpContext httpContext,
        IGameStateStore gameStateStore,
        CancellationToken ct)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var state = await gameStateStore.GetOrCreatePlayerStateAsync(userId, ct);

        var all = Achievements.All.Select(a => new
        {
            a.Id,
            a.Title,
            a.Description,
            a.Emoji,
            a.Category,
            Unlocked = state.UnlockedAchievements.Contains(a.Id),
        });

        return Results.Ok(all);
    }

    private static async Task<IResult> StartNewGameAsync(
        HttpContext httpContext,
        IGameStateStore gameStateStore,
        [FromBody] NewGameRequest request,
        InputValidator inputValidator,
        CancellationToken ct)
    {
        // Validate character name against injection patterns
        if (!string.IsNullOrWhiteSpace(request.CharacterName))
        {
            (bool isValid, string? error, _) = inputValidator.ValidateAndSanitize(
                new ChatRequest { Messages = [new ChatMessageDto { Role = "user", Content = request.CharacterName }] });
            
            if (!isValid)
                return Results.BadRequest(new { error = error ?? "Invalid character name." });
        }

        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        // Validate CharacterClass against allowlist to prevent prompt injection
        string[] validClasses = ["warrior", "rogue", "mage", "explorer"];
        string characterClass = validClasses.Contains(request.CharacterClass?.ToLowerInvariant())
            ? request.CharacterClass!
            : "Explorer";

        var state = new PlayerState
        {
            PlayerId = userId,
            Name = string.IsNullOrWhiteSpace(request.CharacterName) ? "Adventurer" : request.CharacterName[..Math.Min(request.CharacterName.Length, 30)],
            CharacterClass = characterClass,
            CurrentLocation = "The Crossroads",
        };

        // Starter items per class
        switch (state.CharacterClass.ToLowerInvariant())
        {
            case "warrior":
                state.Stats["strength"] = 14;
                state.Stats["dexterity"] = 10;
                state.Inventory.Add(new InventoryItem { Name = "Iron Sword", Emoji = "⚔️", Type = "weapon", Rarity = "common", Description = "A sturdy blade" });
                state.Inventory.Add(new InventoryItem { Name = "Leather Shield", Emoji = "🛡️", Type = "armor", Rarity = "common", Description = "Basic protection" });
                break;
            case "rogue":
                state.Stats["dexterity"] = 14;
                state.Stats["charisma"] = 12;
                state.Inventory.Add(new InventoryItem { Name = "Twin Daggers", Emoji = "🗡️", Type = "weapon", Rarity = "uncommon", Description = "Quick and deadly" });
                state.Inventory.Add(new InventoryItem { Name = "Lockpicks", Emoji = "🔧", Type = "key", Rarity = "uncommon", Description = "Opens most locks" });
                break;
            case "mage":
                state.Stats["wisdom"] = 14;
                state.Stats["charisma"] = 12;
                state.Inventory.Add(new InventoryItem { Name = "Oak Staff", Emoji = "🪄", Type = "weapon", Rarity = "uncommon", Description = "Channels arcane energy" });
                state.Inventory.Add(new InventoryItem { Name = "Spellbook", Emoji = "📕", Type = "misc", Rarity = "rare", Description = "Contains basic incantations" });
                break;
            default:
                state.Inventory.Add(new InventoryItem { Name = "Walking Stick", Emoji = "🏒", Type = "weapon", Description = "Better than nothing" });
                state.Inventory.Add(new InventoryItem { Name = "Traveler's Map", Emoji = "🗺️", Type = "misc", Description = "Shows nearby areas" });
                break;
        }

        state.Inventory.Add(new InventoryItem { Name = "Healing Potion", Emoji = "🧪", Type = "potion", Description = "Restores 25 HP", Quantity = 2 });

        await gameStateStore.ResetPlayerStateAsync(userId, state, ct);
        return Results.Ok(state);
    }

    private static async Task<IResult> HandlePlayAsync(
        HttpContext httpContext,
        [FromBody] ChatRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        ContentFilter contentFilter,
        IGameStateStore gameStateStore,
        IConversationStore conversationStore,
        IStoryMemoryService memoryService,
        GameToolRegistry gameToolRegistry,
        ILogger<IConversationStore> logger,
        CancellationToken cancellationToken)
    {
        (bool isValid, string? error, ChatRequest? sanitizedRequest) = inputValidator.ValidateAndSanitize(request);
        if (!isValid)
            return Results.BadRequest(new { error });

        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var playerState = await gameStateStore.GetOrCreatePlayerStateAsync(userId, cancellationToken);

        // Recall past story memories for context
        var recentMemories = await memoryService.GetRecentMemoriesAsync(userId, 5, cancellationToken);
        string memoryContext = recentMemories.Count > 0
            ? "\n\nPAST EVENTS THE PLAYER REMEMBERS:\n" + string.Join("\n", recentMemories.Select(m => $"- [{m.MemoryType}] {m.Content}"))
            : "";

        // Build game context for the DM
        string destinations = string.Join(", ", WorldMap.GetConnections(playerState.CurrentLocation));
        string gameContext = BuildGameContext(playerState) + memoryContext
            + Bestiary.FormatForDmPrompt(playerState.Level)
            + $"\n\nAVAILABLE DESTINATIONS FROM {playerState.CurrentLocation}: {destinations}";

        // Session management
        string sessionId;
        if (sanitizedRequest!.SessionId is { Length: > 128 })
            return Results.BadRequest(new { error = "Invalid session ID." });

        if (sanitizedRequest.SessionId is { Length: > 0 } sid)
        {
            string? owner = await conversationStore.GetSessionOwnerAsync(sid, cancellationToken);
            if (owner is null || owner != userId)
                return Results.Forbid();
            sessionId = sid;
        }
        else
        {
            sessionId = await conversationStore.CreateSessionAsync(userId, cancellationToken);
        }

        // Build messages with DM system prompt + game context
        var systemMessage = new ChatMessageDto { Role = "system", Content = $"{DmSystemPrompt}\n\n{gameContext}" };
        var allMessages = new List<ChatMessageDto> { systemMessage };
        allMessages.AddRange(sanitizedRequest.Messages);

        List<ChatMessage> chatMessages = allMessages.Select(m =>
            new ChatMessage(m.Role switch
            {
                "system" => ChatRole.System,
                "assistant" => ChatRole.Assistant,
                _ => ChatRole.User,
            }, m.Content)).ToList();

        ChatOptions chatOptions = new() { Tools = [.. gameToolRegistry.Tools] };

        // Persist the user's message for session history
        var userMessages = sanitizedRequest.Messages
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase)).ToList();
        if (userMessages.Count > 0)
            await conversationStore.AppendMessagesAsync(sessionId, userMessages, cancellationToken);

        // Tool execution loop with state tracking and timeout
        List<GameEvent> gameEvents = [];
        using var aiTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        aiTimeout.CancelAfter(TimeSpan.FromMinutes(5));

        for (int round = 0; round < MaxToolCallRounds; round++)
        {
            var aiResponse = await chatClient.GetResponseAsync(chatMessages, chatOptions, aiTimeout.Token);

            var functionCalls = aiResponse.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();

            if (functionCalls.Count == 0)
            {
                // Check achievements
                var newAchievements = Achievements.CheckAchievements(playerState, playerState.UnlockedAchievements);
                foreach (var ach in newAchievements)
                {
                    playerState.UnlockedAchievements.Add(ach.Id);
                    gameEvents.Add(new GameEvent
                    {
                        Type = "Achievement",
                        Data = JsonSerializer.SerializeToElement(new { ach.Id, ach.Title, ach.Emoji, ach.Description }),
                    });
                }

                // Final response — save state and return
                try
                {
                    await gameStateStore.SavePlayerStateAsync(userId, playerState, cancellationToken);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Concurrency"))
                {
                    return Results.Conflict(new { error = "Game state changed by another request. Please try again." });
                }

                var responseMessages = aiResponse.Messages
                    .Where(m => m.Text is { Length: > 0 })
                    .Select(m => new ChatMessageDto { Role = "assistant", Content = m.Text })
                    .ToList();

                // Filter AI output for XSS/injection
                var filtered = contentFilter.FilterResponse(new Shared.Contracts.ChatResponse { Messages = responseMessages });

                // Persist
                if (filtered.Messages.Count > 0)
                    await conversationStore.AppendMessagesAsync(sessionId, filtered.Messages, cancellationToken);

                return Results.Ok(new PlayResponse
                {
                    Messages = filtered.Messages.ToList(),
                    SessionId = sessionId,
                    PlayerState = playerState,
                    GameEvents = gameEvents,
                });
            }

            chatMessages.AddRange(aiResponse.Messages);

            foreach (var fc in functionCalls)
            {
                AIFunction? tool = gameToolRegistry.GetTool(fc.Name);
                if (tool is null) continue;

                try
                {
                    var args = fc.Arguments is { Count: > 0 }
                        ? new AIFunctionArguments(fc.Arguments!)
                        : null;
                    object? result = await tool.InvokeAsync(args, cancellationToken);

                    // Apply to state and get client-safe version
                    object? clientResult = GameToolRegistry.ApplyToolResult(result, playerState);

                    gameEvents.Add(new GameEvent
                    {
                        Type = fc.Name,
                        Data = JsonSerializer.SerializeToElement(clientResult),
                    });

                    string resultJson = JsonSerializer.Serialize(result);
                    if (resultJson.Length > MaxToolResultLength)
                        resultJson = resultJson[..MaxToolResultLength];

                    chatMessages.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(fc.CallId, resultJson)]));

                    logger.LogInformation("Game tool {Tool} executed for player {PlayerId}", fc.Name, userId);

                    // Store significant events as memories
                    await StoreGameEventAsMemoryAsync(memoryService, userId, sessionId, fc.Name, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Game tool {Tool} failed", fc.Name);
                    chatMessages.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(fc.CallId, "Tool execution failed.")]));
                }
            }
        }

        try
        {
            await gameStateStore.SavePlayerStateAsync(userId, playerState, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Concurrency"))
        {
            return Results.Conflict(new { error = "Game state changed by another request. Please try again." });
        }
        
        return Results.Ok(new PlayResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "The threads of fate grow tangled... (Tool limit reached)" }],
            SessionId = sessionId,
            PlayerState = playerState,
            GameEvents = gameEvents,
        });
    }

    private static async Task HandlePlayStreamAsync(
        HttpContext httpContext,
        [FromBody] ChatRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        ContentFilter contentFilter,
        IGameStateStore gameStateStore,
        IConversationStore conversationStore,
        IStoryMemoryService memoryService,
        GameToolRegistry gameToolRegistry,
        ILogger<IConversationStore> logger,
        CancellationToken cancellationToken)
    {
        (bool isValid, string? error, ChatRequest? sanitizedRequest) = inputValidator.ValidateAndSanitize(request);
        if (!isValid)
        {
            httpContext.Response.StatusCode = 400;
            await httpContext.Response.WriteAsJsonAsync(new { error }, cancellationToken);
            return;
        }

        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) { httpContext.Response.StatusCode = 401; return; }

        var playerState = await gameStateStore.GetOrCreatePlayerStateAsync(userId, cancellationToken);
        
        // Recall past story memories
        var recentMemories = await memoryService.GetRecentMemoriesAsync(userId, 5, cancellationToken);
        string memoryContext = recentMemories.Count > 0
            ? "\n\nPAST EVENTS THE PLAYER REMEMBERS:\n" + string.Join("\n", recentMemories.Select(m => $"- [{m.MemoryType}] {m.Content}"))
            : "";
        
        string gameContext = BuildGameContext(playerState) + memoryContext
            + Bestiary.FormatForDmPrompt(playerState.Level)
            + $"\n\nAVAILABLE DESTINATIONS FROM {playerState.CurrentLocation}: {string.Join(", ", WorldMap.GetConnections(playerState.CurrentLocation))}";

        // Session management
        string sessionId;
        if (sanitizedRequest!.SessionId is { Length: > 128 })
        {
            httpContext.Response.StatusCode = 400;
            await httpContext.Response.WriteAsJsonAsync(new { error = "Invalid session ID." }, cancellationToken);
            return;
        }

        if (sanitizedRequest.SessionId is { Length: > 0 } sid)
        {
            string? owner = await conversationStore.GetSessionOwnerAsync(sid, cancellationToken);
            if (owner is null || owner != userId)
            {
                httpContext.Response.StatusCode = 403;
                return;
            }
            sessionId = sid;
        }
        else
        {
            sessionId = await conversationStore.CreateSessionAsync(userId, cancellationToken);
        }

        var systemMessage = new ChatMessage(ChatRole.System, $"{DmSystemPrompt}\n\n{gameContext}");
        var chatMessages = new List<ChatMessage> { systemMessage };
        chatMessages.AddRange(sanitizedRequest.Messages.Select(m =>
            new ChatMessage(m.Role switch
            {
                "assistant" => ChatRole.Assistant,
                _ => ChatRole.User,
            }, m.Content)));

        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        // Enforce AI call timeout to prevent hanging
        using var aiTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        aiTimeout.CancelAfter(TimeSpan.FromMinutes(5));
        var ct = aiTimeout.Token;

        ChatOptions chatOptions = new() { Tools = [.. gameToolRegistry.Tools] };
        List<GameEvent> gameEvents = [];

        // Persist the user's message for session history
        var userMessages = sanitizedRequest.Messages
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase)).ToList();
        if (userMessages.Count > 0)
            await conversationStore.AppendMessagesAsync(sessionId, userMessages, cancellationToken);

        // Tool execution loop (same as non-streaming, but we stream the final text)
        var fullText = new System.Text.StringBuilder();
        try
        {
            for (int round = 0; round < MaxToolCallRounds; round++)
            {
                var aiResponse = await chatClient.GetResponseAsync(chatMessages, chatOptions, ct);

                var functionCalls = aiResponse.Messages
                    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                    .ToList();

                if (functionCalls.Count == 0)
                {
                    // Check achievements
                    var newAchievements = Achievements.CheckAchievements(playerState, playerState.UnlockedAchievements);
                    foreach (var ach in newAchievements)
                    {
                        playerState.UnlockedAchievements.Add(ach.Id);
                        gameEvents.Add(new GameEvent
                        {
                            Type = "Achievement",
                            Data = JsonSerializer.SerializeToElement(new { ach.Id, ach.Title, ach.Emoji, ach.Description }),
                        });
                    }

                    // Stream the final text response
                    string? responseText = aiResponse.Text;
                    if (responseText is { Length: > 0 })
                    {
                        // Filter AI output for XSS/injection before streaming
                        var filteredChunk = contentFilter.FilterResponse(new Shared.Contracts.ChatResponse
                        {
                            Messages = [new ChatMessageDto { Role = "assistant", Content = responseText }]
                        });
                        responseText = filteredChunk.Messages[0].Content ?? responseText;

                        fullText.Append(responseText);
                        // Stream in chunks for realistic effect
                        int chunkSize = 12;
                        for (int i = 0; i < responseText.Length; i += chunkSize)
                        {
                            string chunk = responseText[i..Math.Min(i + chunkSize, responseText.Length)];
                            string data = JsonSerializer.Serialize(new { text = chunk });
                            await httpContext.Response.WriteAsync($"event: text-delta\ndata: {data}\n\n", ct);
                            await httpContext.Response.Body.FlushAsync(ct);
                        }
                    }

                    // Save state — handle concurrency conflicts explicitly
                    try
                    {
                        await gameStateStore.SavePlayerStateAsync(userId, playerState, cancellationToken);
                    }
                    catch (InvalidOperationException concurrencyEx)
                    {
                        logger.LogWarning(concurrencyEx, "State concurrency conflict during stream for user {UserId}", userId);
                        await WriteSseEventSafeAsync(httpContext, "error",
                            new { error = "State conflict — another request modified your game state. Please refresh." });
                    }
                    break;
                }

                chatMessages.AddRange(aiResponse.Messages);

                foreach (var fc in functionCalls)
                {
                    AIFunction? tool = gameToolRegistry.GetTool(fc.Name);
                    if (tool is null) continue;

                    try
                    {
                        var args = fc.Arguments is { Count: > 0 }
                            ? new AIFunctionArguments(fc.Arguments!)
                            : null;
                        object? result = await tool.InvokeAsync(args, cancellationToken);

                        object? clientResult = GameToolRegistry.ApplyToolResult(result, playerState);

                        var gameEvent = new GameEvent
                        {
                            Type = fc.Name,
                            Data = JsonSerializer.SerializeToElement(clientResult),
                        };
                        gameEvents.Add(gameEvent);

                        // Send tool result as SSE event in real-time
                        string toolEventData = JsonSerializer.Serialize(gameEvent);
                        await httpContext.Response.WriteAsync($"event: tool-result\ndata: {toolEventData}\n\n", ct);
                        await httpContext.Response.Body.FlushAsync(ct);

                        string resultJson = JsonSerializer.Serialize(result);
                        if (resultJson.Length > MaxToolResultLength)
                            resultJson = resultJson[..MaxToolResultLength];

                        chatMessages.Add(new ChatMessage(ChatRole.Tool,
                            [new FunctionResultContent(fc.CallId, resultJson)]));

                        logger.LogInformation("Game tool {Tool} executed for player {PlayerId}", fc.Name, userId);

                        await StoreGameEventAsMemoryAsync(memoryService, userId, sessionId, fc.Name, result, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Game tool {Tool} failed", fc.Name);
                        chatMessages.Add(new ChatMessage(ChatRole.Tool,
                            [new FunctionResultContent(fc.CallId, "Tool execution failed")]));
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Play stream error for user {UserId}", userId);
        }

        // Persist — apply final content filter to catch split-XSS before storage
        if (fullText.Length > 0)
        {
            var finalFiltered = contentFilter.FilterResponse(new Shared.Contracts.ChatResponse
            {
                Messages = [new ChatMessageDto { Role = "assistant", Content = fullText.ToString() }]
            });
            string sanitizedFull = finalFiltered.Messages[0].Content ?? fullText.ToString();

            using var saveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await conversationStore.AppendMessagesAsync(sessionId,
                    [new ChatMessageDto { Role = "assistant", Content = sanitizedFull }], saveCts.Token);
                
                // Store the narrative as a story memory (truncated for brevity)
                string summary = sanitizedFull.Length > 200 ? sanitizedFull[..200] + "..." : sanitizedFull;
                await memoryService.StoreMemoryAsync(userId, sessionId, summary, "event", ct: saveCts.Token);
            }
            catch (OperationCanceledException) { /* Best effort save */ }
        }

        // Send state + done (safe — client may have disconnected)
        await WriteSseEventSafeAsync(httpContext, "state", playerState);
        await WriteSseEventSafeAsync(httpContext, "done", new { sessionId });
    }

    /// <summary>
    /// Writes an SSE event, silently ignoring errors if the client has disconnected.
    /// </summary>
    private static async Task WriteSseEventSafeAsync(HttpContext httpContext, string eventName, object payload)
    {
        try
        {
            if (httpContext.RequestAborted.IsCancellationRequested) return;
            string data = JsonSerializer.Serialize(payload);
            await httpContext.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n");
            await httpContext.Response.Body.FlushAsync();
        }
        catch (Exception)
        {
            // Client disconnected — safe to ignore
        }
    }

    private static async Task<IResult> GetWorldMapAsync(
        HttpContext httpContext,
        IGameStateStore gameStateStore,
        CancellationToken ct)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var state = await gameStateStore.GetOrCreatePlayerStateAsync(userId, ct);
        string map = WorldMap.GenerateMap(state.CurrentLocation, state.VisitedLocations);
        var connections = WorldMap.GetConnections(state.CurrentLocation);

        return Results.Ok(new { map, connections, explored = state.VisitedLocations.Count, total = WorldMap.Locations.Count });
    }

    private static async Task<IResult> GetRandomEncounterAsync(
        HttpContext httpContext,
        IGameStateStore gameStateStore,
        CancellationToken ct)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var state = await gameStateStore.GetOrCreatePlayerStateAsync(userId, ct);
        var creature = Bestiary.GetEncounterCreature(state.Level);

        return Results.Ok(new
        {
            creature.Name,
            creature.Emoji,
            creature.Level,
            creature.Health,
            creature.AttackDc,
            creature.Damage,
            creature.Description,
            creature.Abilities,
            creature.Weakness,
            creature.XpReward,
            creature.GoldDrop,
            Prompt = $"[COMBAT ENCOUNTER: {creature.Emoji} {creature.Name} (Level {creature.Level})] " +
                     $"A {creature.Name} appears! {creature.Description} " +
                     $"It has {creature.Health} HP, attacks at DC {creature.AttackDc} for {creature.Damage} damage. " +
                     $"Weakness: {creature.Weakness}. Abilities: {string.Join(", ", creature.Abilities)}. " +
                     $"Resolve this combat encounter following the combat rules.",
        });
    }

    private static async Task<IResult> ConsultOracleAsync(
        HttpContext httpContext,
        [FromBody] OracleRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        ContentFilter contentFilter,
        IGameStateStore gameStateStore,
        IStoryMemoryService memoryService,
        CancellationToken ct)
    {
        // Validate input against injection attacks
        (bool isValid, string? error, _) = inputValidator.ValidateAndSanitize(
            new ChatRequest { Messages = [new ChatMessageDto { Role = "user", Content = request.Question }] });
            
        if (!isValid)
            return Results.BadRequest(new { error = error ?? "Invalid question." });

        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 500)
            return Results.BadRequest(new { error = "Ask a clear question (max 500 chars)." });

        var state = await gameStateStore.GetOrCreatePlayerStateAsync(userId, ct);
        var memories = await memoryService.GetRecentMemoriesAsync(userId, 3, ct);

        string context = $"""
            {OracleSystemPrompt}
            
            PLAYER STATE: {state.Name} the {state.CharacterClass}, Level {state.Level}, at {state.CurrentLocation}.
            HP: {state.Health}/{state.MaxHealth}, Gold: {state.Gold}
            Recent events: {string.Join("; ", memories.Select(m => m.Content))}
            
            The player asks: {request.Question}
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, context),
            new(ChatRole.User, request.Question),
        };

        using var oracleTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        oracleTimeout.CancelAfter(TimeSpan.FromMinutes(2));

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: oracleTimeout.Token);
        string oracleText = response.Messages.LastOrDefault(m => m.Text is { Length: > 0 })?.Text ?? "The Oracle is silent...";

        // Filter AI output for XSS/injection
        var filteredResponse = contentFilter.FilterResponse(new Shared.Contracts.ChatResponse
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = oracleText }]
        });
        oracleText = filteredResponse.Messages[0].Content ?? oracleText;

        // Store the oracle consultation as a memory
        await memoryService.StoreMemoryAsync(userId, "oracle", $"Consulted the Oracle about: {request.Question}", "lore", ct: ct);

        return Results.Ok(new { oracle = oracleText });
    }

    private static string BuildGameContext(PlayerState state) => $"""
        CURRENT GAME STATE:
        Player: {state.Name} the {state.CharacterClass} (Level {state.Level})
        HP: {state.Health}/{state.MaxHealth} | Gold: {state.Gold} | XP: {state.Experience}
        Location: {state.CurrentLocation}
        Stats: STR {state.Stats.GetValueOrDefault("strength", 10)} | DEX {state.Stats.GetValueOrDefault("dexterity", 10)} | WIS {state.Stats.GetValueOrDefault("wisdom", 10)} | CHA {state.Stats.GetValueOrDefault("charisma", 10)}
        Inventory: {string.Join(", ", state.Inventory.Select(i => $"{i.Emoji} {i.Name} (x{i.Quantity})"))}
        """;

    private static async Task StoreGameEventAsMemoryAsync(
        IStoryMemoryService memoryService, string userId, string sessionId,
        string toolName, object? result, CancellationToken ct)
    {
        string? content = toolName switch
        {
            "MovePlayer" when result is LocationResult lr =>
                $"Traveled to {lr.Location}",
            "GiveItem" when result is ItemResult ir =>
                $"Acquired {ir.Name}",
            "TakeItem" when result is ItemResult ir =>
                $"Lost {ir.Name}",
            "RollCheck" when result is DiceCheckResult dr =>
                $"Rolled {dr.Roll} for {dr.Stat} check ({(dr.Success ? "succeeded" : "failed")})",
            "GenerateNpc" when result is NpcResult nr =>
                $"Met {nr.Name}, a {nr.Role}",
            "ModifyHealth" when result is HealthResult hr =>
                $"Health changed by {hr.Amount} (from {hr.Source})",
            "AwardExperience" when result is ExperienceResult xr =>
                $"Gained {xr.Amount} XP for {xr.Reason}",
            _ => null,
        };

        if (content is null) return;

        string memoryType = toolName switch
        {
            "MovePlayer" => "location",
            "GenerateNpc" => "character",
            "GiveItem" or "TakeItem" => "item",
            _ => "event",
        };

        await memoryService.StoreMemoryAsync(userId, sessionId, content, memoryType, ct: ct);
    }
}

public sealed record NewGameRequest
{
    public string? CharacterName { get; init; }
    public string? CharacterClass { get; init; }
}

public sealed record OracleRequest
{
    public string Question { get; init; } = string.Empty;
}
