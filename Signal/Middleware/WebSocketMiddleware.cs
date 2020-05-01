using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Signal.Services;

namespace Signal.Middleware
{
    public class WebSocketMiddleware
    {
        private RequestDelegate Next { get; }
        private int BufferSize { get; }

        public WebSocketMiddleware(RequestDelegate next, WebSocketOptions options)
        {
            Next = next;
            BufferSize = options.ReceiveBufferSize;
        }

        public async Task InvokeAsync(HttpContext context, ConnectionService connectionService)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
                await ProcessWebSocket(socket, connectionService);
            }
            else
            {
                await Next(context);
            }
        }

        private async Task ProcessWebSocket(WebSocket socket, ConnectionService connectionService)
        {
            var buffer = new byte[BufferSize];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await connectionService.ReceiveClose(socket, result.CloseStatus.GetValueOrDefault(), result.CloseStatusDescription);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    string strData = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await connectionService.ReceiveMessage(socket, strData);
                }
            }
        }
    }
}