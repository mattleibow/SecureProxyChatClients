using System.ComponentModel;
using SecureProxyChatClients.Client.Web.Services;
using SecureProxyChatClients.Shared.Contracts;

namespace SecureProxyChatClients.Client.Web.Tools;

public class SaveStoryStateTool(StoryStateService storyState)
{
    [Description("Saves the current story state to local browser storage")]
    public SaveResult SaveStoryState(
        [Description("The scene ID to save")] string sceneId,
        [Description("The content to save for the scene")] string content)
    {
        bool updated = storyState.UpdateSceneContent(sceneId, content);
        if (updated)
            return new SaveResult { Success = true, Message = $"Scene '{sceneId}' saved." };

        // Scene doesn't exist yet — create it
        storyState.AddScene(new SceneResult
        {
            Id = sceneId,
            Title = sceneId,
            Description = content,
            Characters = [],
            Location = "Unknown",
            Mood = "neutral",
        });
        return new SaveResult { Success = true, Message = $"Scene '{sceneId}' created." };
    }
}
