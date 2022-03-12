using System;
using System.Threading.Tasks;

namespace pincerna;

public class ChatTarget
{
    public string Name { get; set; } = "";
    public ulong Id { get; set; } = 0;
}

public class Message
{
    public string Text = "";
}

public interface IConversation
{
    ChatTarget Describe();
    Task SendAsync(Message message);
    Task<Message> ReadNextAsync();
}

public interface IBot
{
    Task Run();
}

public interface IApplication
{
    void OnNewConversation(IBot bot, IConversation conversation);
}

public static class BotFactory
{
    public static IBot Make(string botKey, IApplication app)
    {
        return new Bot(botKey, app);
    }
}