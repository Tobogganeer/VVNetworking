using System;
using UnityEngine;

namespace VirtualVoid.Networking.Server
{
    public class ServerInstance : MonoBehaviour
    {
        [HideInInspector] public Server server;
        //[Header("Contains the methods that will be called when a packet is received. Must inherit from IServerPacketHandler!")]
        //public GameObject packetHandler;

        [Header("This is just a simple MonoBehaviour front for the Server class, you can use this one or make one yourself.")]
        public int maxClients;
        public int port;
        public string password = "";
        public bool autoRunOnAppStart;
        public bool initializeServerOnStart = true;

        [Header("If enabled, all messages received from clients via the built in SendMessage function will be logged.")]
        public bool logMessagesFromClients;

        [Header("If false, incoming connection will be rejected.")]
        public bool clientsCanJoin = true;

        public event Action OnServerStart;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action OnServerClose;
        public event Action<int, string> OnReceiveMessageFromClient;

        [Header("If true, the IP of clients attempting to connect will be shown in the Debug logs.")]
        public bool showIncomingClientIPInLogs = false;

        public void Start()
        {
            OnReceiveMessageFromClient += LogCLientMessage;

            if (initializeServerOnStart)
            {
                server = new Server(maxClients, port, new System.Collections.Generic.Dictionary<PacketID, VerifiedPacketHandler>(/*new PacketIDEqualityComparer()*/));
                server.SetClientsCanJoin(delegate { return clientsCanJoin; });
                server.OnServerStart += ServerStart;
                server.OnClientConnected += ClientConnected;
                server.OnClientDisconnected += ClientDisconnected;
                server.OnServerClose += ServerClose;
                server.OnReceiveMessageFromClient += ReceiveMessageFromClient;
            }

            if (autoRunOnAppStart)
            {
                StartServer();
            }
        }

        public void StartServer()
        {
            if (server == null)
            {
                //IServerPacketHandler handler = (IServerPacketHandler)packetHandler.GetComponent(typeof(IServerPacketHandler));
                //if (handler != null)
                //{
                //server = new Server(maxClients, port, handler.CollectPacketHandlers());
                server = new Server(maxClients, port, new System.Collections.Generic.Dictionary<PacketID, VerifiedPacketHandler>(/*new PacketIDEqualityComparer()*/));
                server.SetClientsCanJoin(delegate { return clientsCanJoin; });
                server.OnServerStart += ServerStart;
                server.OnClientConnected += ClientConnected;
                server.OnClientDisconnected += ClientDisconnected;
                server.OnServerClose += ServerClose;
                server.OnReceiveMessageFromClient += ReceiveMessageFromClient;
                //}
                //else
                //{
                //    Debug.Log("Assigned PacketHandler is not an instance of IServerPacketHandler!");
                //    return;
                //}
            }

            if (!server.started)
            {
                server.showIncomingClientIPInLogs = showIncomingClientIPInLogs;
                server.StartServer(password);
            }
        }

        public void StopServer()
        {
            if (server != null && server.started && !server.stopping)
                server.Stop();
        }

        public void OnApplicationQuit()
        {
            StopServer();
        }

        #region Packet Sending
        public void SendTCPData(int _toClient, Packet _packet)
        {
            server.SendTCPData(_toClient, _packet);
        }

        public void SendUDPData(int _toClient, Packet _packet)
        {
            server.SendUDPData(_toClient, _packet);
        }

        public void SendTCPDataToAll(Packet _packet)
        {
            server.SendTCPDataToAll(_packet);
        }

        public void SendTCPDataToAll(int _exceptClient, Packet _packet)
        {
            server.SendTCPDataToAll(_exceptClient, _packet);
        }

        public void SendUDPDataToAll(Packet _packet)
        {
            server.SendUDPDataToAll(_packet);
        }

        public void SendUDPDataToAll(int _exceptClient, Packet _packet)
        {
            server.SendUDPDataToAll(_exceptClient, _packet);
        }
        #endregion

        #region Message Sending
        public void SendMessageToClient(int _client, string _message)
        {
            server.SendMessage(_client, _message);
        }

        public void SendMessageToAllClients(string _message)
        {
            server.SendMessageToAll(_message);
        }

        public void SendMessageToAll(int _exceptClient, string _message)
        {
            server.SendMessageToAll(_exceptClient, _message);
        }
        #endregion

        #region NetworkEntities
        public void SpawnNetworkEntity(ServerNetworkEntity networkEntity)
        {
            server?.SpawnNetworkEntity(networkEntity);
        }

        public void TransformNetworkEntity(ServerNetworkEntity networkEntity)
        {
            server?.TransformNetworkEntity(networkEntity);
        }

        public void DestroyNetworkEntity(ServerNetworkEntity networkEntity)
        {
            server?.DestroyNetworkEntity(networkEntity);
        }
        #endregion

        #region Events
        public void ServerStart()
        {
            OnServerStart?.Invoke();
        }

        public void ClientConnected(int _client)
        {
            OnClientConnected?.Invoke(_client);
        }

        public void ClientDisconnected(int _client)
        {
            OnClientDisconnected?.Invoke(_client);
        }

        public void ServerClose()
        {
            OnServerClose?.Invoke();
        }

        public void ReceiveMessageFromClient(int fromClient, string message)
        {
            OnReceiveMessageFromClient?.Invoke(fromClient, message);
        }
        #endregion

        private void LogCLientMessage(int fromClient, string message)
        {
            if (logMessagesFromClients)
                Debug.Log($"Message from client (ID: {fromClient}): {message}");
        }
    }
}
