using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace VirtualVoid.Networking.Client
{
    public class Client
    {
        public static int dataBufferSize = 4096;

        public string ip { get; private set; } = "127.0.0.1";
        public int port { get; private set; } = 26950;
        public int myId { get; private set; } = 0;
        public bool isConnected { get; private set; } = false;
        public TCP tcp;
        public UDP udp;

        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;
        public event Action<string> OnReceiveMessageFromServer;

        public delegate void PacketHandler(Packet _packet);
        private Dictionary<string, PacketHandler> packetHandlers;

        /// <summary>
        /// Creates a new client, and sets the IP and port.
        /// </summary>
        /// <param name="packetHandlers">The methods that will be called when a packet is received. The string is the ID of the packet.</param>
        /// <param name="ip">The IP address of the server.</param>
        /// <param name="port">The port that the server is listening on</param>
        public Client(string ip, int port, Dictionary<string, PacketHandler> packetHandlers)
        {
            this.packetHandlers = packetHandlers;
            this.ip = ip;
            this.port = port;

            Debug.Log("Collecting client packet handlers using reflection...");
            CollectPacketHandlers();
        }

        private void CollectPacketHandlers()
        {
            foreach (Assembly assembly in AssemblyUtil.GetAssemblies())
            {
                //Assembly assembly = NetworkManager.instance.anyComponent.GetType().Assembly;

                Debug.Log(assembly.FullName);

                MethodInfo[] methods = assembly.GetTypes()
                            .SelectMany(t => t.GetMethods())
                            .Where(m => m.GetCustomAttributes(typeof(ClientReceiveAttribute), false).Length > 0)
                            .ToArray();

                Debug.Log("Methods with ClientReceiveAttribute found: " + methods.Length);

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
                            ClientReceiveAttribute attrib = methodInfo.GetCustomAttribute<ClientReceiveAttribute>();
                            Debug.Log("Attrib found: " + attrib != null);
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

        public void ConnectToServer()
        {
            tcp = new TCP(this);
            udp = new UDP(this);

            tcp.Connect();
        }

        public class TCP
        {
            private Client client;
            public TcpClient socket;

            private NetworkStream stream;
            private Packet receivedData;
            private byte[] receiveBuffer;

            public TCP(Client client)
            {
                this.client = client;
            }

            public void Connect()
            {
                //socket = new TcpClient(AddressFamily.InterNetworkV6); IPV6
                socket = new TcpClient();
                socket.ReceiveBufferSize = dataBufferSize;
                socket.SendBufferSize = dataBufferSize;

                receiveBuffer = new byte[dataBufferSize];
                socket.BeginConnect(client.ip, client.port, ConnectCallback, socket);
            }

            private void ConnectCallback(IAsyncResult _result)
            {
                socket.EndConnect(_result);

                if (!socket.Connected)
                {
                    return;
                }

                stream = socket.GetStream();

                receivedData = new Packet();

                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }

            public void SendData(Packet _packet)
            {
                try
                {
                    if (socket != null)
                    {
                        _packet.Encrypt(NetworkManager.instance.encryptionType, NetworkManager.instance.encryptionKey);
                        stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
                    }
                }
                catch (Exception _ex)
                {
                    Debug.Log($"Error sending data to server via TCP: {_ex}");
                }
            }

            private void ReceiveCallback(IAsyncResult _result)
            {
                try
                {
                    int _byteLength = stream.EndRead(_result);
                    if (_byteLength <= 0)
                    {
                        client.Disconnect();
                        Debug.Log("Received null TCP packet. Disconnecting...");
                        return;
                    }

                    byte[] _data = new byte[_byteLength];
                    Array.Copy(receiveBuffer, _data, _byteLength);

                    receivedData.Reset(HandleData(_data));
                    stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);

                }
                catch
                {
                    Debug.Log("Error receiving TCP data. Disconnecting...");
                    Disconnect();
                }
            }

            private bool HandleData(byte[] _data)
            {
                int _packetLength = 0;

                receivedData.SetBytes(_data);

                if (receivedData.UnreadLength() >= 4)
                {
                    _packetLength = receivedData.ReadInt();
                    if (_packetLength <= 0)
                    {
                        return true;
                    }
                }

                while (_packetLength > 0 && _packetLength <= receivedData.UnreadLength())
                {
                    byte[] _packetBytes = receivedData.ReadBytes(_packetLength);
                    ThreadManager.ExecuteOnMainThread(() =>
                    {
                        using (Packet _packet = new Packet(_packetBytes))
                        {
                            _packet.Decrypt(NetworkManager.instance.encryptionType, NetworkManager.instance.encryptionKey);
                            string _packetId = _packet.ReadString();

                            client.HandlePacket(_packetId, _packet);
                        }
                    });

                    _packetLength = 0;

                    if (receivedData.UnreadLength() >= 4)
                    {
                        _packetLength = receivedData.ReadInt();
                        if (_packetLength <= 0)
                        {
                            return true;
                        }
                    }
                }
                if (_packetLength <= 1)
                {
                    return true;
                }

                return false;
            }

            private void Disconnect()
            {
                Debug.Log("Disconnecting from the server...");
                client.Disconnect();

                stream = null;
                receivedData = null;
                receiveBuffer = null;
                socket = null;
            }
        }

        public class UDP
        {
            public Client client;
            public UdpClient socket;
            public IPEndPoint endPoint;

            public UDP(Client client)
            {
                this.client = client;
                IPAddress ipAddress;
                if (!IPAddress.TryParse(client.ip, out ipAddress))
                    ipAddress = Dns.GetHostEntry(client.ip).AddressList[0];

                endPoint = new IPEndPoint(ipAddress, client.port);
            }

            public void Connect(int _localPort)
            {
                //socket = new UdpClient(_localPort, AddressFamily.InterNetworkV6); IPV6
                socket = new UdpClient(_localPort);

                socket.Connect(endPoint);
                socket.BeginReceive(ReceiveCallback, null);

                using (Packet _packet = new Packet())
                {
                    SendData(_packet);
                }
            }

            public void SendData(Packet _packet)
            {
                try
                {
                    _packet.InsertInt(client.myId);
                    if (socket != null)
                    {
                        _packet.Encrypt(NetworkManager.instance.encryptionType, NetworkManager.instance.encryptionKey);
                        socket.BeginSend(_packet.ToArray(), _packet.Length(), null, null);
                    }
                }
                catch (Exception _ex)
                {
                    Debug.Log($"Error sending data to server via UDP: {_ex}");
                }
            }

            private void ReceiveCallback(IAsyncResult _result)
            {
                try
                {
                    byte[] _data = socket.EndReceive(_result, ref endPoint);
                    socket.BeginReceive(ReceiveCallback, null);

                    if (_data.Length < 4)
                    {
                        client.Disconnect();
                        Debug.Log("Received null UDP packet. Disconnecting...");
                        return;
                    }

                    HandleData(_data);
                }
                catch
                {
                    Disconnect();
                }
            }

            private void HandleData(byte[] _data)
            {
                using (Packet _packet = new Packet(_data))
                {
                    int _packetLength = _packet.ReadInt();
                    _data = _packet.ReadBytes(_packetLength);
                }

                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_data))
                    {
                        _packet.Decrypt(NetworkManager.instance.encryptionType, NetworkManager.instance.encryptionKey);
                        string _packetId = _packet.ReadString();
                        client.HandlePacket(_packetId, _packet);
                    }
                });
            }

            private void Disconnect()
            {
                client.Disconnect();

                endPoint = null;
                socket = null;
            }
        }

        private void HandlePacket(string _id, Packet _packet)
        {
            string app_id = _packet.ReadString();
            string version = _packet.ReadString();

            if (app_id != NetworkManager.instance.APPLICATION_ID || version != NetworkManager.instance.VERSION)
            {
                if (app_id != NetworkManager.instance.APPLICATION_ID)
                {
                    Debug.Log($"Packet (ID: {_id}) received from server with invalid APPLICATION_ID! Discarding...");
                    return;
                }
                if (version == NetworkManager.instance.VERSION)
                {
                    Debug.Log($"Packet (ID: {_id}) received from server with invalid VERSION! Discarding...");
                    return;
                }
            }

            if (_id == NetworkManager.DEFAULT_SERVER_WELCOME_ID)
            {
                ServerWelcome(_packet);
            }
            else if (_id == NetworkManager.DEFAULT_SERVER_DISCONNECT_ID)
            {
                ServerDisconnect(_packet);
            }
            else if (_id == NetworkManager.DEFAULT_SERVER_MESSAGE)
            {
                MessageReceived(_packet);
            }

            else if (_id == NetworkManager.DEFAULT_SERVER_SPAWN_NETWORKENTITY)
            {
                SpawnNetworkEntity(_packet);
            }
            else if (_id == NetworkManager.DEFAULT_SERVER_TRANSFORM_NETWORKENTITY)
            {
                TransformNetworkEntity(_packet);
            }
            else if (_id == NetworkManager.DEFAULT_SERVER_DESTROY_NETWORKENTITY)
            {
                DestroyNetworkEntity(_packet);
            }

            else if (packetHandlers.ContainsKey(_id))
            {
                packetHandlers[_id](_packet);
            }
            else
                Debug.LogError($"Error receiving data from server: PacketHandlers does not contain key {_id}!");
        }

        /// <summary>
        /// Disconnects the client from the server. Call when the app is closing, or the connection will persist even after the app closes.
        /// </summary>
        public void Disconnect(bool log = true)
        {
            if (isConnected)
                DisconnectedFromServer();

            isConnected = false;
            if (tcp.socket != null)
            {
                tcp.socket.Close();
            }
            if (udp.socket != null)
                udp.socket.Close();
            if (log)
                Debug.Log("Disconnected.");
        }


        public void ConnectedToServer()
        {
            OnConnectedToServer?.Invoke();
        }

        public void DisconnectedFromServer()
        {
            OnDisconnectedFromServer?.Invoke();
        }


        #region BASE_RECEIVE
        private void ServerWelcome(Packet _packet)
        {
            int _myId = _packet.ReadInt();
            isConnected = true;

            Debug.Log($"Connected to server.");
            myId = _myId;
            WelcomeReceived();

            udp.Connect(((IPEndPoint)tcp.socket.Client.LocalEndPoint).Port);
            ConnectedToServer();
        }

        private void ServerDisconnect(Packet _packet)
        {
            Debug.Log($"Server disconnected: Reason - {_packet.ReadString()}");
            Disconnect(false);
            isConnected = false;
        }

        private void MessageReceived(Packet _packet)
        {
            OnReceiveMessageFromServer?.Invoke(_packet.ReadString());
        }


        private void SpawnNetworkEntity(Packet _packet)
        {
            int id = _packet.ReadInt();
            string entityType = _packet.ReadString();
            Vector3 position = _packet.ReadVector3();
            Quaternion rotation = _packet.ReadQuaternion();
            Vector3 scale = _packet.ReadVector3();

            ClientNetworkEntity.SpawnNetworkEntity(id, entityType, position, rotation, scale);
        }

        private void TransformNetworkEntity(Packet _packet)
        {
            int id = _packet.ReadInt();
            Vector3 position = _packet.ReadVector3();
            Quaternion rotation = _packet.ReadQuaternion();

            ClientNetworkEntity.TransformNetworkEntity(id, position, rotation, this);
        }

        private void DestroyNetworkEntity(Packet _packet)
        {
            int id = _packet.ReadInt();

            ClientNetworkEntity.DestroyNetworkEntity(id);
        }
        #endregion

        #region BASE_SEND

        /// <summary>
        /// Sends the packet over TCP (slow, reliable)
        /// </summary>
        /// <param name="_packet">The packet to send [using (Packet _packet = new Packet("PACKET_ID")) {}]</param>
        public void SendTCPData(Packet _packet)
        {
            _packet.WriteLength();
            tcp.SendData(_packet);
        }

        /// <summary>
        /// Sends the packet over UDP (fast, unreliable)
        /// </summary>
        /// <param name="_packet">The packet to send [using (Packet _packet = new Packet("PACKET_ID")) {}]</param>
        public void SendUDPData(Packet _packet)
        {
            _packet.WriteLength();
            udp.SendData(_packet);
        }

        private void WelcomeReceived()
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_CLIENT_WELCOME_RECEIVED))
            {
                _packet.Write(myId);

                SendTCPData(_packet);
            }
        }

        public void SendMessage(string message)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_CLIENT_MESSAGE))
            {
                _packet.Write(message);

                SendTCPData(_packet);
            }
        }

        public void ResendNetworkEntityDetails(int entityId)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_CLIENT_RESEND_NETWORKENTITY))
            {
                _packet.Write(entityId);

                SendTCPData(_packet);
            }
        }

        #endregion
    }
}
