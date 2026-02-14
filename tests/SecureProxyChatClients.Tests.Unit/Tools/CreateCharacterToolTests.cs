using SecureProxyChatClients.Server.Tools;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Tests.Unit.Tools;

public class CreateCharacterToolTests
{
    [Fact]
    public void CreateCharacter_ReturnsCharacterWithCorrectProperties()
    {
        CharacterResult result = CreateCharacterTool.CreateCharacter(
            "Aria", "protagonist", "A thief who steals memories", ["cunning", "empathetic"]);

        Assert.Equal("Aria", result.Name);
        Assert.Equal("protagonist", result.Role);
        Assert.Equal("A thief who steals memories", result.Backstory);
        Assert.StartsWith("char-", result.Id);
    }

    [Fact]
    public void CreateCharacter_PreservesAllTraits()
    {
        List<string> traits = ["brave", "loyal", "stubborn"];

        CharacterResult result = CreateCharacterTool.CreateCharacter(
            "Kael", "sidekick", "A loyal companion", traits);

        Assert.Equal(3, result.Traits.Count);
        Assert.Contains("brave", result.Traits);
        Assert.Contains("loyal", result.Traits);
        Assert.Contains("stubborn", result.Traits);
    }

    [Fact]
    public void CreateCharacter_GeneratesUniqueIds()
    {
        CharacterResult first = CreateCharacterTool.CreateCharacter(
            "A", "antagonist", "backstory", ["trait"]);
        CharacterResult second = CreateCharacterTool.CreateCharacter(
            "B", "mentor", "backstory", ["trait"]);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("protagonist")]
    [InlineData("antagonist")]
    [InlineData("mentor")]
    [InlineData("sidekick")]
    public void CreateCharacter_AcceptsAllRoles(string role)
    {
        CharacterResult result = CreateCharacterTool.CreateCharacter(
            "Test", role, "backstory", ["trait"]);

        Assert.Equal(role, result.Role);
    }
}
