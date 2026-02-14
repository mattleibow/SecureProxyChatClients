namespace SecureProxyChatClients.Server.GameEngine;

/// <summary>
/// Random dramatic events that can be triggered by the "Twist of Fate" button.
/// Demonstrates AI grounding with structured random events.
/// </summary>
public static class TwistOfFate
{
    private static readonly Random _rng = new();

    public sealed record Twist(string Title, string Prompt, string Emoji, string Category);

    private static readonly Twist[] Twists =
    [
        // Environmental
        new("Earthquake!", "A sudden earthquake shakes the ground violently. Cracks appear in the earth around you, and something ancient stirs beneath the surface.", "🌋", "environment"),
        new("Eclipse", "The sun vanishes behind an unnatural darkness. Stars appear in the daytime sky, and shadows begin to move on their own.", "🌑", "environment"),
        new("Fog of Whispers", "A thick, luminous fog rolls in from nowhere. Within it, you hear whispered voices telling fragments of a prophecy about you.", "🌫️", "environment"),
        new("Wild Magic Surge", "The air crackles with untamed magical energy. Every spell and enchantment in the area goes haywire. Something unexpected happens.", "⚡", "environment"),

        // Encounters
        new("Ambush!", "Enemies burst from hiding! You're surrounded and must fight or find a clever way to escape.", "⚔️", "combat"),
        new("Mysterious Stranger", "A cloaked figure steps from the shadows. They claim to know your destiny — but their intentions are unclear.", "🕵️", "encounter"),
        new("Merchant of Wonders", "A peculiar merchant appears with impossible wares: bottled starlight, maps to hidden places, and a mirror that shows the future.", "🏪", "encounter"),
        new("Wounded Creature", "You find a magnificent creature — half-dragon, half-stag — wounded and dying. It looks at you with intelligent eyes, as if asking for help.", "🦌", "encounter"),

        // Discovery
        new("Hidden Passage", "The wall beside you suddenly shifts, revealing a narrow passage descending into darkness. Cold air rises from below, carrying the scent of something ancient.", "🚪", "discovery"),
        new("Ancient Artifact", "Something gleams in the rubble — an artifact from a civilization that shouldn't exist. It pulses with power and seems to recognize you.", "💫", "discovery"),
        new("Portal Rift", "A shimmering rift tears open in the air before you, showing a glimpse of another world entirely. It's unstable and won't last long.", "🌀", "discovery"),
        new("Treasure Map Fragment", "You discover a fragment of an old map that seems to mark a location very close to where you are now. An X marks something buried nearby.", "🗺️", "discovery"),

        // Personal
        new("Memory Flash", "A vivid memory floods your mind — but it's not YOUR memory. It belongs to someone who stood in this exact spot, centuries ago.", "🧠", "personal"),
        new("Cursed!", "A malevolent presence brushes against your soul. You feel something change inside you — a curse has taken hold.", "☠️", "personal"),
        new("Rival Appears", "Someone from your past arrives — a rival who has been tracking you. They challenge you to settle an old score.", "🦹", "personal"),
        new("Divine Vision", "A deity notices you and sends a vision: a task that, if completed, will grant you great power. But failure means divine displeasure.", "👁️", "personal"),
    ];

    /// <summary>
    /// Get a random twist of fate.
    /// </summary>
    public static Twist GetRandomTwist() => Twists[_rng.Next(Twists.Length)];

    /// <summary>
    /// Get a twist from a specific category.
    /// </summary>
    public static Twist GetTwistByCategory(string category)
    {
        var filtered = Twists.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToArray();
        return filtered.Length > 0 ? filtered[_rng.Next(filtered.Length)] : GetRandomTwist();
    }
}
