using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace pincerna;


internal class Conversation: IConversation
{
    private readonly Bot Backend;
    private readonly ChatTarget Target;

    public Conversation(Bot bot, ChatTarget target)
    {
        Backend = bot;
        Target = target;
    }

    public ChatTarget Describe()
    {
        return Target;
    }

    public Task SendAsync(Message message)
    {
        return Backend.SendAsync(Target, message);
    }

    public Task<Message> ReadNextAsync()
    {
        return Backend.ReadNextAsync(Target);
    }
}

internal class ConvState
{
    public TaskCompletionSource<Message>? Waiter;
    public List<Message> Queue = new List<Message>();
    public SemaphoreSlim Lock = new SemaphoreSlim(1,1);
}

internal static class HttpExtensions
{
    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, HttpContent? content = null,
        Dictionary<string, string>? headers = null)
    {
        HttpRequestMessage request = new HttpRequestMessage(method, uri)
        {
            Content = content
        };
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }
        return request;
    }
    private static HttpContent SerializeContent(object payload)
    {
        var j = JsonConvert.SerializeObject(payload);
        var httpContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(j));
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return httpContent;
    }
    public static async Task<JObject> GetJsonAsync(this HttpClient client, string url)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, url);
        using HttpResponseMessage response = await client.SendAsync(request);
        var txt = await response.Content.ReadAsByteArrayAsync();
        return JObject.Parse(System.Text.Encoding.UTF8.GetString(txt));
    }
    public static async Task<JObject> PostJsonAsync(this HttpClient client, string url, object payload)
    {
         using HttpContent httpContent = SerializeContent(payload);
         using HttpRequestMessage request = CreateRequest(HttpMethod.Post, url, httpContent);
         using HttpResponseMessage response = await client.SendAsync(request);
         var txt = await response.Content.ReadAsByteArrayAsync();
        return JObject.Parse(System.Text.Encoding.UTF8.GetString(txt));
    }
}
internal class Bot: IBot
{
    private readonly string BaseURL = "https://api.telegram.org";
    private readonly string BotKey;
    private readonly HttpClient Client;
    private readonly IApplication App;
    private ulong Offset = 0;
    private ConcurrentDictionary<ulong, ConvState> State = new();
    private ILogger Logger;
    public Bot(string botKey, IApplication app)
    {
        BotKey = "bot" + botKey;
        Client = new HttpClient();
        App = app;
        Logger = (new LoggerFactory()).CreateLogger("Bot");
    }
    public Task<Message> ReadNextAsync(ChatTarget target)
    {
        ConvState? conv = null;
        if (!State.TryGetValue(target.Id, out conv))
        {
            conv = new ConvState();
            State.TryAdd(target.Id, conv);
        }
        conv.Lock.Wait();
        if (conv.Queue.Any())
        {
            var msg = conv.Queue[0];
            conv.Queue.RemoveAt(0);
            conv.Lock.Release();
            return Task.FromResult(msg);
        }
        else
        {
            conv.Waiter = new TaskCompletionSource<Message>();
            conv.Lock.Release();
            return conv.Waiter.Task;
        }
    }
    private Message MakeMessage(JObject u)
    {
        return new Message
        {
            Text = u["message"]["text"].Value<string>(),
        };
    }
    public Task SendAsync(ChatTarget target, Message message)
    {
        return Client.PostJsonAsync($"{BaseURL}/{BotKey}/sendMessage", new {chat_id = target.Id, text = message.Text});
    }
    public async Task Run()
    {
        while (true)
        {
            var j = await Client.GetJsonAsync($"{BaseURL}/{BotKey}/getUpdates?offset={Offset}&timeout=60");
            foreach (var u in j["result"])
            {
                var msg = MakeMessage(u as JObject);
                var cid = u["message"]["chat"]["id"].Value<ulong>();
                ConvState? conv = null;
                var notify = false;
                if (!State.TryGetValue(cid, out conv))
                {
                    conv = new ConvState();
                    State.TryAdd(cid, conv);
                    notify = true;
                }
                conv.Lock.Wait();
                try
                {
                    if (conv.Waiter != null)
                    {
                        conv.Waiter.SetResult(msg);
                        conv.Waiter = null;
                    }
                    else
                    {
                        conv.Queue.Add(msg);
                    }
                }
                finally
                {
                    conv.Lock.Release();
                }
                if (notify)
                    App.OnNewConversation(this, new Conversation(this, new ChatTarget { Id = cid})); 
                Offset = u["update_id"].Value<ulong>() + 1;
            }
        }
    }
}