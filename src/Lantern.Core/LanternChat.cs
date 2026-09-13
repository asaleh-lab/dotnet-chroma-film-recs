using OpenAI.Chat;

namespace Lantern.Core;

public static class LanternChat
{
    public static async Task<string> CompleteAsync(string apiKey, string model, string system, string user)
    {
        var client = new ChatClient(model, apiKey);
        ChatCompletion completion = await client.CompleteChatAsync(
            [new SystemChatMessage(system), new UserChatMessage(user)],
            new ChatCompletionOptions { Temperature = 0f });
        return completion.Content.Count > 0 ? completion.Content[0].Text : "";
    }
}
