using System;
using pincerna;

namespace Appointments;

public class AppointmentsLocalization
{
    public string welcome { get; set; } = "Welcome to the booking program. Please type 'check' to check your current booking, or 'book' to book one, or 'cancel' to cancel an existing booking.";
    public string noBookingFound { get; set; } = "No booking found for you.";
    public string bookingFound { get; set; } = "You are booked for the following time: ";
    public string queryBookTime { get; set; } = "At what time do you want to book? Type 'first' for next slot, 'rand' for a random one, or a time (and optional day of week).";
    public string dateNotUnderstood { get; set; } = "I couldn't understand what you typed, try again or type 'abort' to abort.";
    public string queryConfirmBooking { get; set; } = "We found a slot for you, enter 'yes' to confirm and book the following slot (anything else to try another one): ";
    public string bookingConfirmed { get; set; } = "Your booking is confirmed, please remeber your allocated slot: ";
    public string noCandidateFound { get; set; } = "Could not find anything for that date, try again.";
    public string confirmCancel { get; set; } = "Reply with 'yes' to confirm cancellation of slot: ";
    public string cancelConfirmed { get; set; } = "Your booking was canceled, feeel free to book again";
    public string cancelCanceled { get; set; } = "Operation aborted.";
}

public class AppointmentsAgent
{
    private static string[][] dayOfWeek = new string[][]
    {
        new string[] { "dimanche", "sunday"},
        new string[] { "lundi", "monday"},
        new string[] { "mardi", "tuesday"},
        new string[] { "mercredi", "wendsday"},
        new string[] { "jeudi", "thursday"},
        new string[] { "vendredi", "friday"},
        new string[] { "samedi", "saturday"},
    };
    private static int ParseDayOfWeek(string txt)
    {
        for (var i=0; i< dayOfWeek.Length; i++)
        {
            if (dayOfWeek[i].Contains(txt))
                return i;
        }
        return -1;
    }
    private readonly AppointmentsLocalization Loc = new AppointmentsLocalization();
    private readonly AppointmentsModel Model;
    private readonly IConversation Conversation;
    public static Task Process(AppointmentsModel model, IConversation conversation)
    {
        var agent = new AppointmentsAgent(model, conversation);
        return agent.Run();
    }
    public AppointmentsAgent(AppointmentsModel model, IConversation conversation)
    {
        Model = model;
        Conversation = conversation;
    }
    private bool ParseDate(string txt, out DateTime res)
    {
        res = default;
        var dt = ParseDate(txt);
        if (dt.HasValue)
            res = dt.Value;
        return dt.HasValue;
    }
    private DateTime? ParseDate(string txt)
    {
        TimeSpan? time = null;
        DateOnly? date = null;
        var words = txt.Split(' ');
        foreach (var w in words)
        {
            if (char.IsDigit(w[0]))
            {
                try
                {
                    time = TimeSpan.Parse(w);
                }
                catch(Exception)
                {}
            }
            else
            {
                var dow = ParseDayOfWeek(w);
                if (dow >= 0)
                {
                    date = DateOnly.FromDateTime(DateTime.Now);
                    while ((int)date.Value.DayOfWeek != dow)
                        date = date.Value.AddDays(1);
                }
                else
                {
                    try
                    {
                        date = DateOnly.Parse(w);
                    }
                    catch (Exception)
                    {}
                }
            }
        }
        if (date == null && time == null)
            return null;
        if (date == null)
            date = DateOnly.FromDateTime(DateTime.Now);
        if (time == null)
            time = new TimeSpan();
        return (new DateTime(date.Value.Year, date.Value.Month, date.Value.Day))+time.Value;
    }
    private Message M(string txt)
    {
        return new Message { Text = txt};
    }
    public async Task Run()
    {
        await Conversation.SendAsync(M(Loc.welcome));
        var msg = await Conversation.ReadNextAsync();
        var cmd = msg.Text.Trim().ToLower();
        if (cmd.Contains("check"))
        {
            var hit = Model.Find("id", Conversation.Describe().Id.ToString());
            if (hit == null)
                await Conversation.SendAsync(M(Loc.noBookingFound));
            else
                await Conversation.SendAsync(M(Loc.bookingFound + hit.ToString()));
        }
        else if (cmd.Contains("cancel"))
        {
            var hit = Model.Find("id", Conversation.Describe().Id.ToString());
            if (hit == null)
            {
                await Conversation.SendAsync(M(Loc.noBookingFound));
            }
            else
            {
                await Conversation.SendAsync(M(Loc.confirmCancel + hit.ToString()));
                msg = await Conversation.ReadNextAsync();
                var txt = msg.Text.Trim().ToLower();
                if (txt == "yes")
                {
                    Model.CancelWhere("id", Conversation.Describe().Id.ToString());
                    await Conversation.SendAsync(M(Loc.cancelConfirmed));
                }
                else
                {
                    await Conversation.SendAsync(M(Loc.cancelCanceled));
                }
            }
        }
        else if (cmd.Contains("book"))
        {
            var hit = Model.Find("id", Conversation.Describe().Id.ToString());
            if (hit != null)
                await Conversation.SendAsync(M(Loc.bookingFound + hit.ToString()));
            else
            {
                while (true)
                {
                    await Conversation.SendAsync(M(Loc.queryBookTime));
                    msg = await Conversation.ReadNextAsync();
                    var txt = msg.Text.Trim().ToLower();
                    if (txt.Contains("abort"))
                        break;
                    var rnd = txt.Contains("rand");
                    var queryDate = new DateTime();
                    if (!rnd)
                    {
                        if (txt.Contains("first"))
                            queryDate = DateTime.Now;
                        else if (!ParseDate(txt, out queryDate))
                        {
                            await Conversation.SendAsync(M(Loc.dateNotUnderstood));
                            continue;
                        }
                    }
                    using var book = rnd ? Model.ReserveRandom() : Model.ReserveAt(queryDate);
                    if (book == null)
                    {
                        await Conversation.SendAsync(M(Loc.noCandidateFound));
                        continue;
                    }
                    await Conversation.SendAsync(M(Loc.queryConfirmBooking + book.Start.ToString()));
                    msg = await Conversation.ReadNextAsync();
                    txt = msg.Text.Trim().ToLower();
                    if (txt.Contains("yes"))
                    {
                        book.Book(new Dictionary<string, string>{{"id", Conversation.Describe().Id.ToString()}});
                        await Conversation.SendAsync(M(Loc.bookingConfirmed + book.Start.ToString()));
                        break;
                    }
                }
            }
        }
    }
}