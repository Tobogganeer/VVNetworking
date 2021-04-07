using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace VirtualVoid.Networking.Server
{
    public class Server
    {
        public int MaxClients { get; private set; }
        public int Port { get; private set; }
        private string Password;
        public bool started { get; private set; } = false;
        public bool stopping { get; private set; } = false;

        public event Action OnServerStart;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action OnServerClose;
        public event Action<int, string> OnReceiveMessageFromClient;

        public Dictionary<int, ServerClient> clients = new Dictionary<int, ServerClient>();
        public delegate void PacketHandler(int _fromClient, Packet _packet);
        private Dictionary<PacketID, VerifiedPacketHandler> packetHandlers;
        private Dictionary<string, VerifiedPacketHandler> packetHandlers_string = new Dictionary<string, VerifiedPacketHandler>();
        private Dictionary<short, VerifiedPacketHandler> packetHandlers_short = new Dictionary<short, VerifiedPacketHandler>();

        private Dictionary<PacketID, VerifiedPacketHandler> defaultPacketHandlers;
        private Dictionary<string, VerifiedPacketHandler> defaultPacketHandlers_string = new Dictionary<string, VerifiedPacketHandler>();
        private Dictionary<short, VerifiedPacketHandler> defaultPacketHandlers_short = new Dictionary<short, VerifiedPacketHandler>();

        private TcpListener tcpListener;
        private UdpClient udpListener;

        public bool showIncomingClientIPInLogs = false;

        public bool clientsCanJoin = true;

        //NATUPNPLib.UPnPNATClass upnpnat = new NATUPNPLib.UPnPNATClass();
        //NATUPNPLib.IStaticPortMappingCollection mappings;


        /// <summary>
        /// Initializes the server
        /// </summary>
        /// <param name="maxClients">The maximum clients allowed to be connected at one time.</param>
        /// <param name="port">The port the server will listen on.</param>
        /// <param name="packetHandlers">The methods that will be called when a packet is received. The string is the ID of the packet.</param>
        public Server(int maxClients, int port, Dictionary<PacketID, VerifiedPacketHandler> packetHandlers)
        {
            MaxClients = maxClients;
            Port = port;
            this.packetHandlers = packetHandlers;

            Debug.Log("Collecting default server packet handlers...");
            CollectDefaultPacketHandlers();

            Debug.Log("Collecting server packet handlers using reflection...");
            CollectReflectionPacketHandlers();

            Debug.Log("Filling specific server packet handlers...");
            FillSpecificPacketHandlers();
        }

        private void CollectDefaultPacketHandlers()
        {
            defaultPacketHandlers = new Dictionary<PacketID, VerifiedPacketHandler>()//new PacketIDEqualityComparer())
            {
                { NetworkManager.DEFAULT_CLIENT_WELCOME_RECEIVED, new VerifiedPacketHandler(PacketVerification.STRINGS, WelcomeReceived) },
                { NetworkManager.DEFAULT_CLIENT_MESSAGE, new VerifiedPacketHandler(PacketVerification.HASH, MessageReceived) },
                { NetworkManager.DEFAULT_CLIENT_RESEND_NETWORKENTITY, new VerifiedPacketHandler(PacketVerification.HASH, ResendNetworkEntity) },
            };
        }

        private void CollectReflectionPacketHandlers()
        {

            foreach (MethodInfo methodInfo in AssemblyUtil.GetAllMethodsWithAttribute(typeof(ServerReceiveAttribute)))
            {
                if (!methodInfo.IsStatic)
                {
                    Debug.Log($"Server receive method {methodInfo.Name} is not static!");
                }
                else
                {
                    if (methodInfo.GetParameters().Length != 2 || methodInfo.GetParameters()[0].ParameterType != typeof(int) || methodInfo.GetParameters()[1].ParameterType != typeof(Packet))
                    {
                        Debug.Log($"Server receive method {methodInfo.Name} must have int as the first parameter and Packet as the second!");
                    }
                    else
                    {
                        PacketHandler handler = (PacketHandler)methodInfo.CreateDelegate(typeof(PacketHandler));
                        ServerReceiveAttribute attrib = methodInfo.GetCustomAttribute<ServerReceiveAttribute>();
                        if (!packetHandlers.ContainsKey(attrib.PacketID)) packetHandlers.Add(attrib.PacketID, new VerifiedPacketHandler(attrib.ExpectedVerification, handler));
                    }
                }
            }

            foreach (PacketID packetID in packetHandlers.Keys)
            {
                Debug.Log("Collected packet handler " + packetID);
            }
        }

        private void FillSpecificPacketHandlers()
        {
            packetHandlers_string.Clear();
            packetHandlers_short.Clear();

            foreach (PacketID key in packetHandlers.Keys)
            {
                if (key.short_ID != -1) packetHandlers_short.Add(key.short_ID, packetHandlers[key]);
                if (key.string_ID != "") packetHandlers_string.Add(key.string_ID, packetHandlers[key]);
            }

            defaultPacketHandlers_string.Clear();
            defaultPacketHandlers_short.Clear();

            foreach (PacketID key in defaultPacketHandlers.Keys)
            {
                if (key.short_ID != -1) defaultPacketHandlers_short.Add(key.short_ID, defaultPacketHandlers[key]);
                if (key.string_ID != "") defaultPacketHandlers_string.Add(key.string_ID, defaultPacketHandlers[key]);
            }
        }

        public void StartServer(string password = "")
        {
            this.Password = password;
            //mappings = upnpnat.StaticPortMappingCollection;
            PortForward();
            InitializeServerClients();

            tcpListener = new TcpListener(IPAddress.Any, Port);
            //tcpListener.AllowNatTraversal(true);
            //tcpListener = new TcpListener(IPAddress.IPv6Any, Port);
            //tcpListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false); IPV6
            tcpListener.Start();
            tcpListener.BeginAcceptTcpClient(new AsyncCallback(TCPConnectCallback), null);

            //udpListener = new UdpClient(Port, AddressFamily.InterNetworkV6); IPV6
            udpListener = new UdpClient(Port);
            udpListener.BeginReceive(UDPReceiveCallback, null);

            Debug.Log($"Server started on {Port}");
            started = true;
            stopping = false;

            ServerStart();
        }

        public void PortForward()
        {
            //mappings.Add(Port, "TCP", Port, GetLocalIPAddress(), true, $"{NetworkManager.instance.APPLICATION_ID} server");
            //mappings.Add(Port, "UDP", Port, GetLocalIPAddress(), true, $"{NetworkManager.instance.APPLICATION_ID} server");
        }

        public void UnPortForward()
        {
            //mappings.Remove(Port, "TCP");
            //mappings.Remove(Port, "UDP");
        }

        private void TCPConnectCallback(IAsyncResult _result)
        {
            //TcpClient _client = new TcpClient(AddressFamily.InterNetworkV6);
            TcpClient _client = tcpListener.EndAcceptTcpClient(_result);
            //_client.Client.DualMode = true; // cant set family here
            tcpListener.BeginAcceptTcpClient(new AsyncCallback(TCPConnectCallback), null);
            if (showIncomingClientIPInLogs) Debug.Log($"Incoming connection from {_client.Client.RemoteEndPoint}...");
            else Debug.Log("Incoming connection from {IP Hidden}...");

            if (!clientsCanJoin)
            {
                if (showIncomingClientIPInLogs) Debug.Log($"{_client.Client.RemoteEndPoint} failed to connect: CanConnectToServer() returned false.");
                else Debug.Log("{IP Hidden} failed to connect: CanConnectToServer() returned false.");
            }

            for (int i = 1; i <= MaxClients; i++)
            {
                if (clients[i].tcp.socket == null)
                {
                    clients[i].tcp.Connect(_client);
                    return;
                }
            }

            if (showIncomingClientIPInLogs) Debug.Log($"{_client.Client.RemoteEndPoint} failed to connect: Server full");
            else Debug.Log("{IP Hidden} failed to connect: Server full");
        }

        private void UDPReceiveCallback(IAsyncResult _result)
        {
            try
            {
                IPEndPoint _clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] _data = udpListener.EndReceive(_result, ref _clientEndPoint);
                udpListener.BeginReceive(UDPReceiveCallback, null);

                if (_data.Length < 4)
                {
                    return;
                }

                using (Packet _packet = new Packet(_data))
                {
                    int _clientId = _packet.ReadInt();

                    if (_clientId == 0)
                    {
                        return;
                    }

                    if (clients[_clientId].udp.endPoint == null)
                    {
                        clients[_clientId].udp.Connect(_clientEndPoint);
                        return;
                    }

                    if (clients[_clientId].udp.endPoint.ToString() == _clientEndPoint.ToString())
                    {
                        clients[_clientId].udp.HandleData(_packet);
                    }
                }
            }
            catch (Exception _ex)
            {
                if (!stopping)
                {
                    Debug.Log($"Error receiving UDP data: {_ex}");
                    Debug.Log("^^^ This will happen every time you close the server.");
                }
            }
        }

        public void SendUDPData(IPEndPoint _clientEndPoint, Packet _packet)
        {
            try
            {
                if (_clientEndPoint != null)
                {
                    udpListener.BeginSend(_packet.ToArray(), _packet.Length(), _clientEndPoint, null, null);
                }
            }
            catch (Exception _ex)
            {
                if (showIncomingClientIPInLogs) Debug.Log($"Error sending data to {_clientEndPoint} via UDP: {_ex}");
                else Debug.Log("Error sending data to {IP Hidden} via UDP: " + _ex);
            }
        }

        public void HandleData(int _fromClient, PacketID _packetID, Packet _packet)
        {
            if (TryGetPacketHandler(_packetID, out VerifiedPacketHandler handler))
            {
                if (!clients[_fromClient].joinedWithCorrectPassword && _packetID != NetworkManager.DEFAULT_CLIENT_WELCOME_RECEIVED)
                {
                    Debug.Log($"Client {_fromClient} sent message with ID {_packetID} without verifying password first!");
                    return;
                }

                if (VerifyPacket(_packet, handler.expectedVerification, _packetID, _fromClient))
                    handler.packetHandler(_fromClient, _packet);
            }

            else Debug.LogError($"Error receiving data from client {_fromClient}: PacketHandlers does not contain key {_packetID}!");
        }

        private bool TryGetPacketHandler(PacketID id, out VerifiedPacketHandler handler)
        {
            switch (NetworkManager.instance.packetIDType)
            {
                case PacketIDType.STRING:
                    if (defaultPacketHandlers_string.TryGetValue(id.string_ID, out handler)) return true;
                    else if (packetHandlers_string.TryGetValue(id.string_ID, out handler)) return true;
                    return false;
                case PacketIDType.SHORT:
                    if (defaultPacketHandlers_short.TryGetValue(id.short_ID, out handler)) return true;
                    else if (packetHandlers_short.TryGetValue(id.short_ID, out handler)) return true;
                    return false;
            }

            handler = new VerifiedPacketHandler();
            return false;
        }

        private bool VerifyPacket(Packet _packet, PacketVerification expectedVerification, PacketID packetIDforDebug, int clientIDforDebug)
        {
            PacketVerification verification = (PacketVerification)_packet.ReadByte();

            if (verification != expectedVerification)
            {
                Debug.LogError($"Packet ({packetIDforDebug}, from client: {clientIDforDebug}) verification did not match the expected verification! Discarding...");
                return false;
            }

            switch (verification)
            {
                case PacketVerification.NONE:
                    return true;

                case PacketVerification.STRINGS:
                    string app_id = _packet.ReadString();
                    string version = _packet.ReadString();
                    if (app_id != NetworkManager.instance.APPLICATION_ID || version != NetworkManager.instance.VERSION)
                    {
                        if (app_id != NetworkManager.instance.APPLICATION_ID)
                        {
                            Debug.Log($"Packet ({packetIDforDebug}) received from client {clientIDforDebug} with invalid APPLICATION_ID! Discarding...");
                            return false;
                        }
                        if (version == NetworkManager.instance.VERSION)
                        {
                            Debug.Log($"Packet ({packetIDforDebug}) received from client {clientIDforDebug} with invalid VERSION! Discarding...");
                            return false;
                        }
                    }
                    return true;

                case PacketVerification.HASH:
                    int app_idHash = _packet.ReadInt();
                    int versionHash = _packet.ReadInt();
                    if (app_idHash != NetworkManager.instance.APPLICATION_ID.GetHashCode() || versionHash != NetworkManager.instance.VERSION.GetHashCode())
                    {
                        if (app_idHash != NetworkManager.instance.APPLICATION_ID.GetHashCode())
                        {
                            Debug.Log($"Packet ({packetIDforDebug}) received from client {clientIDforDebug} with invalid APPLICATION_ID hash! Discarding...");
                            return false;
                        }
                        if (versionHash != NetworkManager.instance.VERSION.GetHashCode())
                        {
                            Debug.Log($"Packet ({packetIDforDebug}) received from client {clientIDforDebug} with invalid VERSION hash! Discarding...");
                            return false;
                        }
                    }
                    return true;

                default:
                    return true;
            }
        }

        private void InitializeServerClients()
        {
            clients.Clear();

            for (int i = 1; i <= MaxClients; i++)
            {
                clients.Add(i, new ServerClient(i, this));
            }
        }

        /// <summary>
        /// Call when you want to stop the server.
        /// </summary>
        public void Stop()
        {
            stopping = true;
            started = false;
            tcpListener.Stop();
            udpListener.Close();

            ServerClose();

            foreach (ServerClient client in clients.Values)
            {
                client.Disconnect("SERVER_SHUTDOWN");
            }

            UnPortForward();
        }

        #region Packet Sending Methods
        public void SendTCPData(int _toClient, Packet _packet)
        {
            _packet.WriteLength();
            clients[_toClient].tcp.SendData(_packet);
        }

        public void SendUDPData(int _toClient, Packet _packet)
        {
            _packet.WriteLength();
            clients[_toClient].udp.SendData(_packet);
        }

        public void SendTCPDataToAll(Packet _packet)
        {
            _packet.WriteLength();
            for (int i = 1; i < MaxClients; i++)
            {
                clients[i].tcp.SendData(_packet);
            }
        }

        public void SendTCPDataToAll(int _exceptClient, Packet _packet)
        {
            _packet.WriteLength();
            for (int i = 1; i < MaxClients; i++)
            {
                if (i != _exceptClient)
                    clients[i].tcp.SendData(_packet);
            }
        }

        public void SendUDPDataToAll(Packet _packet)
        {
            _packet.WriteLength();
            for (int i = 1; i < MaxClients; i++)
            {
                clients[i].udp.SendData(_packet);
            }
        }

        public void SendUDPDataToAll(int _exceptClient, Packet _packet)
        {
            _packet.WriteLength();
            for (int i = 1; i < MaxClients; i++)
            {
                if (i != _exceptClient)
                    clients[i].udp.SendData(_packet);
            }
        }
        #endregion

        private void WelcomeReceived(int _fromClient, Packet _packet)
        {
            int clientIdCheck = _packet.ReadInt();
            bool passwordIsEmpty = _packet.ReadBool();
            string clientPass = passwordIsEmpty ? "" : _packet.ReadString();

            if (clientPass == Password)
            {
                Debug.Log($"Client {_fromClient} has connected.");

                if (_fromClient != clientIdCheck)
                {
                    Debug.Log($"Client (ID: {_fromClient}) has assumed the wrong client ID! ({clientIdCheck}). Disconnecting...");
                    clients[_fromClient].Disconnect("FALSE_ID_ASSUMPTION");
                    return;
                }

                clients[_fromClient].joinedWithCorrectPassword = true;
                ClientConnected(_fromClient);
            }
            else
            {
                Debug.Log($"Client {_fromClient} tried to join with incorrect password!");
                clients[_fromClient].Disconnect("WRONG_PASSWORD");
            }
        }

        private void MessageReceived(int _fromClient, Packet _packet)
        {
            OnReceiveMessageFromClient?.Invoke(_fromClient, _packet.ReadString());
        }


        public void SendMessage(int _client, string _message)
        {
            clients[_client].SendMessage(_message);
        }

        public void SendMessageToAll(string _message)
        {
            foreach(ServerClient client in clients.Values)
            {
                client.SendMessage(_message);
            }
        }

        public void SendMessageToAll(int _exceptClient, string _message)
        {
            foreach (ServerClient client in clients.Values)
            {
                if (client.id != _exceptClient)
                    client.SendMessage(_message);
            }
        }

        public void SpawnNetworkEntity(ServerNetworkEntity networkEntity)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_SPAWN_NETWORKENTITY, PacketVerification.HASH))
            {
                _packet.Write(networkEntity.id);
                _packet.Write(networkEntity.entityType);
                _packet.Write(networkEntity.transform.position);
                _packet.Write(networkEntity.transform.rotation);
                _packet.Write(networkEntity.transform.localScale);

                SendTCPDataToAll(_packet);
            }
        }

        public void TransformNetworkEntity(ServerNetworkEntity networkEntity)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_TRANSFORM_NETWORKENTITY, PacketVerification.NONE))
            {
                _packet.Write(networkEntity.id);
                _packet.Write(networkEntity.transform.position);
                _packet.Write(networkEntity.transform.rotation);

                SendUDPDataToAll(_packet);
            }
        }

        public void DestroyNetworkEntity(ServerNetworkEntity networkEntity)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_DESTROY_NETWORKENTITY, PacketVerification.HASH))
            {
                _packet.Write(networkEntity.id);

                SendTCPDataToAll(_packet);
            }
        }

        public void ResendNetworkEntity(int _fromClient, Packet _clientPacket)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_SPAWN_NETWORKENTITY, PacketVerification.HASH))
            {
                int _entityId = _clientPacket.ReadInt();

                if (ServerNetworkEntity.entities.ContainsKey(_entityId))
                {
                    ServerNetworkEntity networkEntity = ServerNetworkEntity.entities[_entityId];

                    _packet.Write(networkEntity.id);
                    _packet.Write(networkEntity.entityType);
                    _packet.Write(networkEntity.transform.position);
                    _packet.Write(networkEntity.transform.rotation);
                    _packet.Write(networkEntity.transform.localScale);

                    SendTCPData(_fromClient, _packet);
                }
                else
                {
                    Debug.LogError($"Client requested resend of entity with ID {_entityId} which no longer exists!");
                }
            }
        }


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


        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }

    public struct VerifiedPacketHandler
    {
        public PacketVerification expectedVerification;
        public Server.PacketHandler packetHandler;

        public VerifiedPacketHandler(PacketVerification expectedVerification, Server.PacketHandler packetHandler)
        {
            this.expectedVerification = expectedVerification;
            this.packetHandler = packetHandler;
        }
    }
}
