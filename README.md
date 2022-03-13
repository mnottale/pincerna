Pincerna: C# telegram bot infrastructure
========================================

Pincerna is a C# library aiming to provide a simple API to write async stateful conversational bots.

```C#
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
```


Pincerna modules
================

We plan on writing multiple useful modules that bind to a conversation and provide specific functionalities.

Currently available modules are listed below.

Appointments
------------

Allow users to book slots in a defined schedule.

To use, instanciate a singleton AppointmentsModel. The first argument is the path to the schedule specifications that is a json looking like:

```
{
  "slotDuration": "00:20:00",
  "openings": [
    { "begin": "2022-03-13T08:00:00", "end": "2022-03-13T16:00:00"},
    { "begin": "2022-03-14T08:00:00", "end": "2022-03-14T16:00:00"}
  ]
}
```

The second argument is an existing directory that will be used for persisting state.

Then in your `IApplication` call `AppointmentsAgent.Process(theModel, myConversation)`.
