using System.Net.WebSockets;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:6960");

var app = builder.Build();
app.UseWebSockets();
var connections = new List<WebSocket>();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var curName = context.Request.Query["name"];

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        connections.Add(ws);

        await Boardcast($"{curName} joined the room");
        await Boardcast($"{connections.Count} users conected");

        await ReceiveMessage(ws,
            async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await Boardcast(curName + ": " + message);
                }
                else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
                {
                    connections.Remove(ws);
                    await Boardcast($"{curName} left the room");
                    await Boardcast($"{connections.Count()} users connected");
                    await ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                }
            });
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
    }
}
async Task Boardcast(string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    foreach (var socket in connections)
    {
        if (socket.State == WebSocketState.Open)
        {
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
await app.RunAsync();

