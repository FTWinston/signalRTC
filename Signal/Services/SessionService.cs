using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;

namespace Signal.Services
{
    public class SessionService
    {
        private class SessionInfo
        {
            public SessionInfo(string identifier, WebSocket host, string url)
            {
                Identifier = identifier;
                Host = host;
                Url = url;
            }

            public string Identifier { get; }

            public WebSocket Host { get; }

            public string Url { get; }

            public ConcurrentDictionary<string, WebSocket> Clients { get; } = new ConcurrentDictionary<string, WebSocket>();
        }

        private ConcurrentDictionary<string, SessionInfo> SessionsByIdentifier { get; } = new ConcurrentDictionary<string, SessionInfo>();
        private ConcurrentDictionary<WebSocket, SessionInfo> SessionsBySocket { get; } = new ConcurrentDictionary<WebSocket, SessionInfo>();

        private IdentifierService IdentifierService { get; }

        public SessionService(IdentifierService identifierService)
        {
            IdentifierService = identifierService;
        }

        public string AddSession(WebSocket hostSocket, string url)
        {
            string id = IdentifierService.GenerateIdentifier();

            var session = new SessionInfo(id, hostSocket, url);

            SessionsByIdentifier.TryAdd(id, session);
            SessionsBySocket.TryAdd(session.Host, session);

            return id;
        }

        public WebSocket GetSessionHost(string identifier)
        {
            if (!SessionsByIdentifier.TryGetValue(identifier, out SessionInfo session))
                return null;

            return session.Host;
        }

        public WebSocket GetSessionHost(WebSocket client)
        {
            if (!SessionsBySocket.TryGetValue(client, out SessionInfo session))
                return null;

            return session.Host;
        }

        public WebSocket GetSessionClient(WebSocket knownSocket, string clientName)
        {
            if (!SessionsBySocket.TryGetValue(knownSocket, out SessionInfo session))
                return null;

            if (!session.Clients.TryGetValue(clientName, out var clientSocket))
                return null;

            return clientSocket;
        }

        public string GetClientName(WebSocket clientSocket)
        {
            if (!SessionsBySocket.TryGetValue(clientSocket, out SessionInfo session))
                return null;

            return session.Clients
                .FirstOrDefault(c => c.Value == clientSocket)
                .Key;
        }

        public bool JoinSession(string identifier, string clientName, WebSocket client)
        {
            if (!SessionsByIdentifier.TryGetValue(identifier, out SessionInfo session))
                return false;

            return session.Clients.TryAdd(clientName, client);
        }

        public IEnumerable<WebSocket> GetAllClients(WebSocket socket)
        {
            if (!SessionsBySocket.TryGetValue(socket, out SessionInfo session))
                return null;

            return session.Clients.Values;
        }

        public bool LeaveSession(WebSocket socket)
        {
            if (!SessionsBySocket.TryRemove(socket, out SessionInfo session))
                return false;

            string removeKey = null;

            foreach (var client in session.Clients)
                if (client.Value == socket)
                {
                    removeKey = client.Key;
                    break;
                }

            if (removeKey == null)
                return false;

            return session.Clients.TryRemove(removeKey, out _);
        }

        public bool RemoveSession(WebSocket socket)
        {
            if (!SessionsBySocket.TryGetValue(socket, out var session))
                return false;

            SessionsByIdentifier.TryRemove(session.Identifier, out _);

            SessionsBySocket.TryRemove(session.Host, out _);

            foreach (var client in session.Clients.Values)
                SessionsBySocket.TryRemove(client, out _);

            return true;
        }
    }
}
