using System.Threading.Tasks;

using pincerna;
using Appointments;

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

public class AppAppointments: IApplication
{
    private readonly AppointmentsModel Model = new AppointmentsModel("appointments.json");

    public async Task Converse(IConversation conv)
    {
        while (true)
        {
            await AppointmentsAgent.Process(Model, conv);
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
        var appts = new AppAppointments();
        var bot = BotFactory.Make(System.IO.File.ReadAllText(".botkey"), appts);
        bot.Run().Wait();
    }
}
