using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Signal.Services
{
    public class ConnectionService
    {
        private ILogger<ConnectionService> Logger { get; }

        private IHttpContextAccessor ContextAccessor { get; }

        private SessionService SessionService { get; }

        public ConnectionService
        (
            ILogger<ConnectionService> logger,
            IHttpContextAccessor contextAccessor,
            SessionService sessionService
        )
        {
            Logger = logger;
            ContextAccessor = contextAccessor;
            SessionService = sessionService;
        }

        private void Log(LogLevel level, string message, params object[] args)
        {
            if (!Logger.IsEnabled(level))
                return;

            Logger.Log(level, $"{ContextAccessor.HttpContext.TraceIdentifier}: {message}", args);
        }

        public async Task ReceiveClose(WebSocket socket, WebSocketCloseStatus closeStatus, string closeDescription)
        {
            // If this was a host, close all clients' connections too, then remove the session.
            if (SessionService.GetSessionHost(socket) == socket)
            {
                foreach (var otherClient in SessionService.GetAllClients(socket))
                    await Close(otherClient, "Host has disconnected", WebSocketCloseStatus.EndpointUnavailable);

                SessionService.RemoveSession(socket);
            }
            else
            {
                SessionService.LeaveSession(socket);
            }

            await Close(socket, "Closing as requested", WebSocketCloseStatus.NormalClosure);
        }

        public async Task ReceiveMessage(WebSocket socket, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Log(LogLevel.Information, "Received empty message");
                return;
            }

            Log(LogLevel.Debug, "Received message: {0}", message);

            string[] data = JsonSerializer.Deserialize<string[]>(message);

            if (data == null || data.Length == 0)
            {
                Log(LogLevel.Information, "Failed to parse data");
                return;
            }

            switch (data[0])
            {
                case "host":
                    await TryHostSession(socket);
                    break;

                case "join":
                    if (data.Length < 4)
                    {
                        Log(LogLevel.Information, "Invalid join message, expected 4 values");
                        return;
                    }

                    await TryJoinSession(socket, data[1], data[2], data[3]);
                    break;

                case "accept":
                    if (data.Length < 3)
                    {
                        Log(LogLevel.Information, "Invalid accept message, expected 3 values");
                        return;
                    }

                    await TryAcceptJoin(socket, data[1], data[2]);
                    break;

                case "reject":
                    if (data.Length < 3)
                    {
                        Log(LogLevel.Information, "Invalid reject message, expected 3 values");
                        return;
                    }

                    await TryRejectJoin(socket, data[1], data[2]);
                    break;

                case "ice":
                    if (data.Length < 3)
                    {
                        Log(LogLevel.Information, "Invalid ice message, expected 3 values");
                        return;
                    }

                    await TrySendIce(socket, data[1], data[2]);
                    break;

                default:
                    Log(LogLevel.Information, "Unexpected message type: {0}", data[0]);
                    break;
            }
        }

        public async Task<bool> TryHostSession(WebSocket socket)
        {
            Log(LogLevel.Debug, "Trying to host a session...");

            if (SessionService.GetSessionHost(socket) != null)
            {
                Log(LogLevel.Information, "Already in a session, cannot host a new one");
                return false;
            }

            // TODO: URL. Use it or lose it.
            string identifier = SessionService.AddSession(socket, "example.com");

            Log(LogLevel.Information, "Session created: {0}", identifier);

            await SendSessionId(socket, identifier);

            return true;
        }

        public async Task<bool> TryJoinSession(WebSocket socket, string sessionId, string clientName, string rtcOffer)
        {
            Log(LogLevel.Debug, "Trying to join session {0}...", sessionId);

            if (SessionService.GetSessionHost(socket) != null)
            {
                Log(LogLevel.Information, "Already in a session, cannot join another");
                return false;
            }

            var host = SessionService.GetSessionHost(sessionId);
            if (host == null)
            {
                await Close(socket, "Invalid session code");
                return false;
            }

            if (!ValidateName(ref clientName))
                return false;

            if (!SessionService.JoinSession(sessionId, clientName, socket))
            {
                await Close(socket, "Unable to join"); // TODO: better error handling here?
                return false;
            }

            await SendOffer(host, clientName, rtcOffer);

            return false;
        }

        public async Task<bool> TryAcceptJoin(WebSocket hostSocket, string clientName, string rtcAnswer)
        {
            Log(LogLevel.Debug, "Trying to accept offer from client {0}...", clientName);

            if (SessionService.GetSessionHost(hostSocket) != hostSocket)
            {
                Log(LogLevel.Information, "Accept message received from non-host socket");
                return false;
            }

            WebSocket? clientSocket = SessionService.GetSessionClient(hostSocket, clientName);

            if (clientSocket == null)
            {
                Log(LogLevel.Information, "Unknown client name: {0}", clientName);
                return false;
            }
            
            await SendAnswer(clientSocket, rtcAnswer);

            return true;
        }

        public async Task<bool> TryRejectJoin(WebSocket hostSocket, string clientName, string message)
        {
            Log(LogLevel.Debug, "Trying to reject offer from client {0}...", clientName);

            if (SessionService.GetSessionHost(hostSocket) != hostSocket)
            {
                Log(LogLevel.Information, "Reject message received from non-host socket");
                return false;
            }

            WebSocket? clientSocket = SessionService.GetSessionClient(hostSocket, clientName);

            if (clientSocket == null)
            {
                Log(LogLevel.Information, "Unknown client name: {0}", clientName);
                return false;
            }

            await Close(clientSocket, message);
            return true;
        }

        public async Task<bool> TrySendIce(WebSocket fromSocket, string toClientName, string data)
        {
            WebSocket? hostSocket = SessionService.GetSessionHost(fromSocket);

            if (hostSocket == fromSocket)
            {
                Log(LogLevel.Debug, "Trying to pass ice data to client {0}...", toClientName);

                WebSocket? clientSocket = SessionService.GetSessionClient(hostSocket, toClientName);

                if (clientSocket == null)
                {
                    Log(LogLevel.Information, "Unknown client name: {0}", toClientName);
                    return false;
                }

                await SendIce(clientSocket, string.Empty, data);
            }
            else if (hostSocket != null)
            {
                Log(LogLevel.Debug, "Trying to pass ice data to host...");

                string? fromClientName = SessionService.GetClientName(fromSocket);

                if (fromClientName != null)
                    await SendIce(hostSocket, fromClientName, data);
            }
            else
            {
                Log(LogLevel.Warning, "Trying to pass ice data, but cannot find host");
                return false;
            }

            return true;
        }

        private bool ValidateName(ref string name)
        {
            name = name.Trim();

            if (name.Length > 0 && name.Length <= 16)
            {
                Log(LogLevel.Debug, "Valid name: {0}", name);
                return true;
            }

            Log(LogLevel.Debug, "Invalid name: {0}", name);
            return false;
        }

        private Task SendSessionId(WebSocket socket, string sessionId)
        {
            Log(LogLevel.Debug, "Sending session ID: {0}", sessionId);

            return Send(socket, "id", sessionId);
        }

        private Task SendOffer(WebSocket socket, string clientName, string rtcOffer)
        {
            Log(LogLevel.Debug, "Sending offer to host");

            return Send(socket, "join", clientName, rtcOffer);
        }

        private Task SendAnswer(WebSocket socket, string rtcAnswer)
        {
            Log(LogLevel.Debug, "Sending answer to client");

            return Send(socket, "answer", rtcAnswer);
        }

        private Task SendIce(WebSocket socket, string from, string data)
        {
            Log(LogLevel.Debug, "Sending ice data");

            return Send(socket, "ice", from, data);
        }

        private async Task Close(WebSocket socket, string message, WebSocketCloseStatus errorStatus = WebSocketCloseStatus.InvalidPayloadData)
        {
            if (socket.State == WebSocketState.Closed)
                return;

            Log(LogLevel.Debug, "Closing connection: {0}", message);

            await socket.CloseAsync(errorStatus, message, CancellationToken.None);
        }

        private async Task Send(WebSocket socket, params string[] data)
        {
            var strData = JsonSerializer.Serialize(data);
            var byteData = Encoding.UTF8.GetBytes(strData);

            await socket.SendAsync(byteData, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
