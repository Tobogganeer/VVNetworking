using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace VirtualVoid.Networking.Server
{
    public class ServerClient
    {
        public static int dataBufferSize = 4096;

        public int id;
        public TCP tcp;
        public UDP udp;
        private Server server;

        public bool isConnected;

        public bool joinedWithCorrectPassword { get; set; } = false;

        public ServerClient(int _clientId, Server server)
        {
            id = _clientId;
            tcp = new TCP(id, this);
            udp = new UDP(id, this);
            this.server = server;
        }

        public class TCP
        {
            public TcpClient socket;

            public readonly int id;
            private NetworkStream stream;
            private Packet receivedData;
            private byte[] receiveBuffer;
            private ServerClient client;

            public TCP(int _id, ServerClient client)
            {
                id = _id;
                this.client = client;
            }

            public void Connect(TcpClient _socket)
            {
                socket = _socket;
                socket.ReceiveBufferSize = dataBufferSize;
                socket.SendBufferSize = dataBufferSize;

                stream = socket.GetStream();

                receivedData = new Packet();
                receiveBuffer = new byte[dataBufferSize];

                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);

                client.Welcome();
                client.isConnected = true;
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
                    Debug.Log($"Error sending data to player {id} via TCP: {_ex}");
                }
            }

            private void ReceiveCallback(IAsyncResult _result)
            {
                try
                {
                    int _byteLength = stream.EndRead(_result);
                    if (_byteLength <= 0)
                    {
                        client.server.clients[id].Disconnect("NULL_TCP_STREAM");
                        return;
                    }

                    byte[] _data = new byte[_byteLength];
                    Array.Copy(receiveBuffer, _data, _byteLength);

                    receivedData.Reset(HandleData(_data));
                    stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);

                }
                catch (Exception _ex)
                {
                    Debug.Log($"Error receiving TCP data: {_ex}"); //got error here
                    client.server.clients[id].Disconnect("ERROR_RECEIVING_TCP_DATA");
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
                            PacketID _packetId = NetworkManager.instance.packetIDType == PacketIDType.STRING ? new PacketID(_packet.ReadString()) : new PacketID(_packet.ReadShort());
                            client.HandleData(_packetId, _packet);
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

            public void Disconnect()
            {
                if (socket != null)
                    socket.Close();
                stream = null;
                receivedData = null;
                receiveBuffer = null;
                socket = null;
            }
        }

        public class UDP
        {
            public IPEndPoint endPoint;
            public int id;
            private ServerClient client;

            public UDP(int _id, ServerClient client)
            {
                id = _id;
                this.client = client;
            }

            public void Connect(IPEndPoint _endPoint)
            {
                endPoint = _endPoint;
            }

            public void SendData(Packet _packet)
            {
                _packet.Encrypt(NetworkManager.instance.encryptionType, NetworkManager.instance.encryptionKey);
                client.server.SendUDPData(endPoint, _packet);
            }

            public void HandleData(Packet _packet)
            {
                int _packetLength = _packet.ReadInt();
                byte[] _packetBytes = _packet.ReadBytes(_packetLength);

                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _newPacket = new Packet(_packetBytes))
                    {
                        _newPacket.Decrypt(NetworkManager.instance.encryptionType, NetworkManager.instance.encryptionKey);
                        PacketID _packetId = NetworkManager.instance.packetIDType == PacketIDType.STRING ? new PacketID(_newPacket.ReadString()) : new PacketID(_newPacket.ReadShort());
                        client.HandleData(_packetId, _newPacket);
                    }
                });
            }

            public void Disconnect()
            {
                endPoint = null;
            }
        }

        public void Disconnect(string reason)
        {
            joinedWithCorrectPassword = false;
            isConnected = false;
            if (tcp.socket != null)
            {
                Debug.Log($"Disconnecting Client (ID: {id})");
                DisconnectClient(reason);
            }

            tcp.Disconnect();
            udp.Disconnect();

            ThreadManager.ExecuteOnMainThread(() => {
                    server.ClientDisconnected(id);
                });
        }

        private void HandleData(PacketID _packetId, Packet _packet)
        {
            server.HandleData(id, _packetId, _packet);
        }

        public void SendTCPData(Packet _packet)
        {
            _packet.WriteLength();
            tcp.SendData(_packet);
        }

        public void SendUDPData(Packet _packet)
        {
            _packet.WriteLength();
            udp.SendData(_packet);
        }

        private void Welcome()
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_WELCOME_ID, PacketVerification.STRINGS))
            {
                _packet.Write(id);

                SendTCPData(_packet);
            }
        }

        private void DisconnectClient(string reason)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_DISCONNECT_ID, PacketVerification.STRINGS))
            {
                _packet.Write(reason);

                SendTCPData(_packet);
            }
        }

        public void SendMessage(string message)
        {
            using (Packet _packet = new Packet(NetworkManager.DEFAULT_SERVER_MESSAGE, PacketVerification.HASH))
            {
                _packet.Write(message);

                SendTCPData(_packet);
            }
        }
    }
}
