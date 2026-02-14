using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SecureProxyChatClients.Server.GameEngine;

public sealed class GameToolRegistry
{
    public IReadOnlyList<AIFunction> Tools { get; }

    private readonly IReadOnlyDictionary<string, AIFunction> _toolMap;

    public GameToolRegistry()
    {
        var tools = new List<AIFunction>
        {
            AIFunctionFactory.Create(GameTools.RollCheck),
            AIFunctionFactory.Create(GameTools.MovePlayer),
            AIFunctionFactory.Create(GameTools.GiveItem),
            AIFunctionFactory.Create(GameTools.TakeItem),
            AIFunctionFactory.Create(GameTools.ModifyHealth),
            AIFunctionFactory.Create(GameTools.ModifyGold),
            AIFunctionFactory.Create(GameTools.AwardExperience),
            AIFunctionFactory.Create(GameTools.GenerateNpc),
        };

        Tools = tools.AsReadOnly();
        _toolMap = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public AIFunction? GetTool(string name) =>
        _toolMap.GetValueOrDefault(name);

    /// <summary>
    /// Applies a tool result to the player state. Returns the result to send to the client
    /// (with hidden fields stripped for NPCs).
    /// </summary>
    public static object? ApplyToolResult(object? result, PlayerState state)
    {
        switch (result)
        {
            case LocationResult loc:
                // Normalize location name to match world map (case-insensitive)
                string normalizedLoc = WorldMap.Locations
                    .Select(l => l.Name)
                    .FirstOrDefault(n => n.Equals(loc.Location, StringComparison.OrdinalIgnoreCase))
                    ?? loc.Location;
                state.CurrentLocation = normalizedLoc;
                state.VisitedLocations.Add(normalizedLoc);
                return result;

            case ItemResult item when item.Added:
                state.Inventory.Add(new InventoryItem
                {
                    Name = item.Name,
                    Description = item.Description,
                    Type = item.Type,
                    Emoji = item.Emoji,
                    Rarity = item.Rarity,
                });
                return result;

            case ItemResult item when !item.Added:
                var existing = state.Inventory.FirstOrDefault(
                    i => i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                    state.Inventory.Remove(existing);
                return result;

            case HealthResult hp:
                state.Health = Math.Clamp(state.Health + hp.Amount, 0, state.MaxHealth);
                return result;

            case GoldResult gold:
                state.Gold = Math.Max(0, state.Gold + gold.Amount);
                return result;

            case ExperienceResult xp:
                state.Experience += xp.Amount;
                while (state.Experience >= state.Level * 100)
                {
                    state.Experience -= state.Level * 100;
                    state.Level++;
                    state.MaxHealth += 10;
                    state.Health = state.MaxHealth;
                }
                return result;

            case DiceCheckResult dice:
                if (dice.Success)
                {
                    state.SuccessStreak++;
                    state.MaxStreak = Math.Max(state.MaxStreak, state.SuccessStreak);
                }
                else
                {
                    state.SuccessStreak = 0;
                }
                // Event-based achievements for dice rolls
                if (dice.CriticalSuccess)
                    state.UnlockedAchievements.Add("critical-hit");
                if (dice.Stat is "charisma" && dice.Success)
                    state.UnlockedAchievements.Add("diplomat");
                return result;

            case NpcResult npc:
                // Award first-contact achievement
                state.UnlockedAchievements.Add("first-contact");
                // Strip hidden secret before sending to client
                return new { npc.Id, npc.Name, npc.Role, npc.Description, npc.Attitude };

            default:
                return result;
        }
    }

    /// <summary>
    /// Check and award combat-related event achievements after health changes.
    /// Call after tool results are applied when combat is active.
    /// </summary>
    public static void CheckCombatAchievements(PlayerState state, bool combatWon)
    {
        if (combatWon)
        {
            state.UnlockedAchievements.Add("first-blood");
        }
        if (state.Health > 0 && state.Health < 5 && combatWon)
        {
            state.UnlockedAchievements.Add("survivor");
        }
    }
}
