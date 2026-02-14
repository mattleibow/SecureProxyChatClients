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
        - Track the player's location with MovePlayer when they travel
        - Award XP for clever solutions and completing objectives
        
        TONE: Dark fantasy with moments of humor. Think Discworld meets Dark Souls.
        """;

    public static RouteGroupBuilder MapPlayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/play")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.BearerScheme},{IdentityConstants.ApplicationScheme}"
            })
            .RequireRateLimiting("chat");

        group.MapPost("/", HandlePlayAsync);
        group.MapPost("/stream", HandlePlayStreamAsync);
        group.MapGet("/state", GetPlayerStateAsync);
        group.MapPost("/new-game", StartNewGameAsync);

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

    private static async Task<IResult> StartNewGameAsync(
        HttpContext httpContext,
        IGameStateStore gameStateStore,
        [FromBody] NewGameRequest request,
        CancellationToken ct)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var state = new PlayerState
        {
            PlayerId = userId,
            Name = string.IsNullOrWhiteSpace(request.CharacterName) ? "Adventurer" : request.CharacterName[..Math.Min(request.CharacterName.Length, 30)],
            CharacterClass = request.CharacterClass ?? "Explorer",
            CurrentLocation = "The Crossroads",
        };

        // Starter items per class
        switch (state.CharacterClass.ToLowerInvariant())
        {
            case "warrior":
                state.Stats["strength"] = 14;
                state.Stats["dexterity"] = 10;
                state.Inventory.Add(new InventoryItem { Name = "Iron Sword", Emoji = "⚔️", Type = "weapon", Description = "A sturdy blade" });
                state.Inventory.Add(new InventoryItem { Name = "Leather Shield", Emoji = "🛡️", Type = "armor", Description = "Basic protection" });
                break;
            case "rogue":
                state.Stats["dexterity"] = 14;
                state.Stats["charisma"] = 12;
                state.Inventory.Add(new InventoryItem { Name = "Twin Daggers", Emoji = "🗡️", Type = "weapon", Description = "Quick and deadly" });
                state.Inventory.Add(new InventoryItem { Name = "Lockpicks", Emoji = "🔧", Type = "key", Description = "Opens most locks" });
                break;
            case "mage":
                state.Stats["wisdom"] = 14;
                state.Stats["charisma"] = 12;
                state.Inventory.Add(new InventoryItem { Name = "Oak Staff", Emoji = "🪄", Type = "weapon", Description = "Channels arcane energy" });
                state.Inventory.Add(new InventoryItem { Name = "Spellbook", Emoji = "📕", Type = "misc", Description = "Contains basic incantations" });
                break;
            default:
                state.Inventory.Add(new InventoryItem { Name = "Walking Stick", Emoji = "🏒", Type = "weapon", Description = "Better than nothing" });
                state.Inventory.Add(new InventoryItem { Name = "Traveler's Map", Emoji = "🗺️", Type = "misc", Description = "Shows nearby areas" });
                break;
        }

        state.Inventory.Add(new InventoryItem { Name = "Healing Potion", Emoji = "🧪", Type = "potion", Description = "Restores 25 HP", Quantity = 2 });

        await gameStateStore.SavePlayerStateAsync(userId, state, ct);
        return Results.Ok(state);
    }

    private static async Task<IResult> HandlePlayAsync(
        HttpContext httpContext,
        [FromBody] ChatRequest request,
        IChatClient chatClient,
        InputValidator inputValidator,
        IGameStateStore gameStateStore,
        IConversationStore conversationStore,
        IStoryMemoryService memoryService,
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
        string gameContext = BuildGameContext(playerState) + memoryContext;
        var gameToolRegistry = new GameToolRegistry();

        // Session management
        string sessionId;
        if (sanitizedRequest!.SessionId is { Length: > 0 and <= 128 } sid)
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

        // Tool execution loop with state tracking
        List<GameEvent> gameEvents = [];

        for (int round = 0; round < MaxToolCallRounds; round++)
        {
            var aiResponse = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken);

            var functionCalls = aiResponse.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();

            if (functionCalls.Count == 0)
            {
                // Final response — save state and return
                await gameStateStore.SavePlayerStateAsync(userId, playerState, cancellationToken);

                var responseMessages = aiResponse.Messages
                    .Where(m => m.Text is { Length: > 0 })
                    .Select(m => new ChatMessageDto { Role = "assistant", Content = m.Text })
                    .ToList();

                // Persist
                if (responseMessages.Count > 0)
                    await conversationStore.AppendMessagesAsync(sessionId, responseMessages, cancellationToken);

                return Results.Ok(new PlayResponse
                {
                    Messages = responseMessages,
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
                        [new FunctionResultContent(fc.CallId, $"Tool error: {ex.Message}")]));
                }
            }
        }

        await gameStateStore.SavePlayerStateAsync(userId, playerState, cancellationToken);
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
        IGameStateStore gameStateStore,
        IConversationStore conversationStore,
        IStoryMemoryService memoryService,
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
        
        string gameContext = BuildGameContext(playerState) + memoryContext;

        // Session management
        string sessionId;
        if (sanitizedRequest!.SessionId is { Length: > 0 and <= 128 } sid)
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

        var fullText = new System.Text.StringBuilder();
        try
        {
            await foreach (var update in chatClient.GetStreamingResponseAsync(
                chatMessages, cancellationToken: cancellationToken))
            {
                if (update.Text is { Length: > 0 } text)
                {
                    fullText.Append(text);
                    string data = JsonSerializer.Serialize(new { text });
                    await httpContext.Response.WriteAsync($"event: text-delta\ndata: {data}\n\n", cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Play stream error for user {UserId}", userId);
        }

        // Persist
        if (fullText.Length > 0)
        {
            await conversationStore.AppendMessagesAsync(sessionId,
                [new ChatMessageDto { Role = "assistant", Content = fullText.ToString() }], cancellationToken);
            
            // Store the narrative as a story memory (truncated for brevity)
            string summary = fullText.Length > 200 ? fullText.ToString()[..200] + "..." : fullText.ToString();
            await memoryService.StoreMemoryAsync(userId, sessionId, summary, "event", ct: CancellationToken.None);
        }

        // Send state + done
        string stateData = JsonSerializer.Serialize(playerState);
        await httpContext.Response.WriteAsync($"event: state\ndata: {stateData}\n\n", CancellationToken.None);

        string doneData = JsonSerializer.Serialize(new { sessionId });
        await httpContext.Response.WriteAsync($"event: done\ndata: {doneData}\n\n", CancellationToken.None);
        await httpContext.Response.Body.FlushAsync(CancellationToken.None);
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
