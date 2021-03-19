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
        public bool started { get; private set; } = false;

        public event Action OnServerStart;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action OnServerClose;
        public event Action<int, string> OnReceiveMessageFromClient;

        public Dictionary<int, ServerClient> clients = new Dictionary<int, ServerClient>();
        public delegate void PacketHandler(int _fromClient, Packet _packet);
        private Dictionary<string, PacketHandler> packetHandlers;

        private TcpListener tcpListener;
        private UdpClient udpListener;

        public delegate bool ClientsCanConnect();
        protected ClientsCanConnect clientsCanConnect = delegate { return true; };

        //NATUPNPLib.UPnPNATClass upnpnat = new NATUPNPLib.UPnPNATClass();
        //NATUPNPLib.IStaticPortMappingCollection mappings;


        /// <summary>
        /// Initializes the server
        /// </summary>
        /// <param name="maxClients">The maximum clients allowed to be connected at one time.</param>
        /// <param name="port">The port the server will listen on.</param>
        /// <param name="packetHandlers">The methods that will be called when a packet is received. The string is the ID of the packet.</param>
        public Server(int maxClients, int port, Dictionary<string, PacketHandler> packetHandlers)
        {
            MaxClients = maxClients;
            Port = port;
            this.packetHandlers = packetHandlers;

            Debug.Log("Collecting server packet handlers using reflection...");
            CollectPacketHandlers();
        }

        private void CollectPacketHandlers()
        {
            foreach (Assembly assembly in AssemblyUtil.GetAssemblies())
            {
                //Assembly assembly = NetworkManager.instance.anyComponent.GetType().Assembly;

                MethodInfo[] methods = assembly.GetTypes()
                        .SelectMany(t => t.GetMethods())
                        .Where(m => m.GetCustomAttributes(typeof(ServerReceiveAttribute), false).Length > 0)
                        .ToArray();

                foreach (MethodInfo methodInfo in methods)
                {
                    if (!methodInfo.IsStatic)
                    {
                        Debug.Log($"Client receive method {methodInfo.Name} is not static!");
                    }
                    else
                    {
                        if (methodInfo.GetParameters().Length != 1 || methodInfo.GetParameters()[0].ParameterType != typeof(Packet))
                        {
                            Debug.Log($"Client receive method {methodInfo.Name} must have Packet as the first and only parameter!");
                        }
                        else
                        {
                            PacketHandler handler = (PacketHandler)methodInfo.CreateDelegate(typeof(PacketHandler));
                            ServerReceiveAttribute attrib = methodInfo.GetCustomAttribute<ServerReceiveAttribute>();
                            if (!packetHandlers.ContainsKey(attrib.PacketID)) packetHandlers.Add(attrib.PacketID, handler);
                        }
                    }
                }

                foreach (string packetID in packetHandlers.Keys)
                {
                    Debug.Log("Collected packet handler " + packetID);
                }
            }
        }

        public void StartServer()
        {
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

        public void SetClientsCanJoin(ClientsCanConnect clientsCanConnect) { this.clientsCanConnect = clientsCanConnect; }

        private void TCPConnectCallback(IAsyncResult _result)
        {
            //TcpClient _client = new TcpClient(AddressFamily.InterNetworkV6);
            TcpClient _client = tcpListener.EndAcceptTcpClient(_result);
            //_client.Client.DualMode = true; // cant set family here
            tcpListener.BeginAcceptTcpClient(new AsyncCallback(TCPConnectCallback), null);
            Debug.Log($"Incoming connection from {_client.Client.RemoteEndPoint}...");

            if (!clientsCanConnect())
            {
                Debug.Log($"{_client.Client.RemoteEndPoint} failed to connect: CanConnectToServer() returned false.");
            }

            for (int i = 1; i <= MaxClients; i++)
            {
                if (clients[i].tcp.socket == null)
                {
                    clients[i].tcp.Connect(_client);
                    return;
                }
            }

            Debug.Log($"{_client.Client.RemoteEndPoint} failed to connect: Server full");
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
                Debug.Log($"Error receiving UDP data: {_ex}");
                Debug.Log("^^^ This will happen every time you close the server.");
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
                Debug.Log($"Error sending data to {_clientEndPoint} via UDP: {_ex}");
            }
        }

        public void HandleData(int _fromClient, string _packetID, Packet _packet)
        {
            string app_id = _packet.ReadString();
            string version = _packet.ReadString();

            if (app_id != NetworkManager.instance.APPLICATION_ID || version != NetworkManager.instance.VERSION)
            {
                if (app_id != NetworkManager.instance.APPLICATION_ID)
                {
                    Debug.Log($"Packet (ID: {_packetID}) received from client (ID: {_fromClient}) with invalid APPLICATION_ID! Discarding and disconnecting client...");
                    clients[_fromClient].Disconnect("INCORRECT_APP");
                    return;
                }
                if (version == NetworkManager.instance.VERSION)
                {
                    Debug.Log($"Packet (ID: {_packetID}) received from client (ID: {_fromClient}) with invalid VERSION! Discarding and disconnecting client...");
                    clients[_fromClient].Disconnect("INCORRECT_VERSION");
                    return;
                }
            }

            if (_packetID == NetworkManager.DEFAULT_CLIENT_WELCOME_RECEIVED)
            {
                WelcomeReceived(_fromClient, _packet);
            }
            else if (_packetID == NetworkManager.DEFAULT_CLIENT_MESSAGE)
            {
                MessageReceived(_fromClient, _packet);
            }
            else if (_packetID == NetworkManager.DEFAULT_CLIENT_RESEND_NETWORKENTITY)
            {
                ResendNetworkEntity(_packet.ReadInt(), _fromClient);
            }

            else if (packetHandlers.ContainsKey(_packetID))
            {
                packetHandlers[_packetID](_fromClient, _packet);
            }
            else
                Debug.LogError($"Error receiving data from client {_fromClient}: PacketHandlers does not contain key {_packetID}!");
        }

        private void InitializeServerClients()
        {
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
            tcpListener.Stop();
            udpListener.Close();

            foreach (ServerClient client in clients.Values)
            {
                client.Disconnect("SERVER_SHUTDOWN");
            }

            UnPortForward();
            started = false;
            ServerClose();
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

            Debug.Log($"Client {_fromClient} has connected.");

            if (_fromClient != clientIdCheck)
            {
                Debug.Log($"Client (ID: {_fromClient}) has assumed the wrong client ID! ({clientIdCheck}). Disconnecting...");
                clients[_fromClient].Disconnect("FALSE_ID_ASSUMPTION");
                return;
            }

            ClientConnected(_fromClient);
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
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_SPAWN_NETWORKENTITY))
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
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_TRANSFORM_NETWORKENTITY))
            {
                _packet.Write(networkEntity.id);
                _packet.Write(networkEntity.transform.position);
                _packet.Write(networkEntity.transform.rotation);

                SendUDPDataToAll(_packet);
            }
        }

        public void DestroyNetworkEntity(ServerNetworkEntity networkEntity)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_DESTROY_NETWORKENTITY))
            {
                _packet.Write(networkEntity.id);

                SendTCPDataToAll(_packet);
            }
        }

        public void ResendNetworkEntity(int _entityId, int _playerId)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_SPAWN_NETWORKENTITY))
            {
                if (ServerNetworkEntity.entities.ContainsKey(_entityId))
                {
                    ServerNetworkEntity networkEntity = ServerNetworkEntity.entities[_entityId];

                    _packet.Write(networkEntity.id);
                    _packet.Write(networkEntity.entityType);
                    _packet.Write(networkEntity.transform.position);
                    _packet.Write(networkEntity.transform.rotation);
                    _packet.Write(networkEntity.transform.localScale);

                    SendTCPData(_playerId, _packet);
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
}
