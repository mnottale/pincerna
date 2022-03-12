using System.Threading.Tasks;

using pincerna;

public class Parrot: IApplication
{
    private async Task Converse(IConversation conv)
    {
        var counter = 1;
        while (true)
        {
            var msg = await conv.ReadNextAsync();
            var reply = $"Message {counter}: {msg.Text}";
            await conv.SendAsync(new Message { Text = reply});
            counter++;
        }
    }
    public void OnNewConversation(IBot bot, IConversation conv)
    {
        Task.Factory.StartNew(() => Converse(conv));
    }
}

public class Program
{
    public static void Main()
    {
        var parrot = new Parrot();
        var bot = BotFactory.Make(System.IO.File.ReadAllText(".botkey"), parrot);
        bot.Run().Wait();
    }
}
