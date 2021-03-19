using System;
using UnityEngine;

namespace VirtualVoid.Networking.Client
{
    public class ClientInstance : MonoBehaviour
    {
        [HideInInspector] public Client client;
        //[Header("Contains the methods that will be called when a packet is received. Must inherit from IClientPacketHandler!")]
        //public GameObject packetHandler;

        [Header("This is just a simple MonoBehaviour front for the Client class, you can use this one or make one yourself.")]
        public string ip;
        public int port;
        public bool autoConnectOnAppStart;

        [Header("If enabled, all messages received from the server via the built in SendMessage function will be logged.")]
        public bool logMessagesFromServer;

        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;
        public event Action<string> OnReceiveMessageFromServer;
        //IUPnPNAT

        public void Start()
        {
            OnReceiveMessageFromServer += LogServerMessage;

            if (autoConnectOnAppStart)
            {
                ConnectToServer();
            }
        }

        public void ConnectToServer()
        {
            if (client == null)
            {
                //IClientPacketHandler handler = (IClientPacketHandler)packetHandler.GetComponent(typeof(IClientPacketHandler));
                //if (handler != null)
                //{
                //  client = new Client(ip, port, handler.CollectPacketHandlers());
                client = new Client(ip, port, new System.Collections.Generic.Dictionary<string, Client.PacketHandler>());
                client.OnConnectedToServer += ClientConnectedToServer;
                client.OnDisconnectedFromServer += ClientDisconnectedFromServer;
                client.OnReceiveMessageFromServer += ReceiveMessageFromServer;
                //}
                //else
                //{
                //    Debug.Log("Assigned PacketHandler is not an instance of IClientPacketHandler!");
                //    return;
                //}
            }

            if (!client.isConnected)
            {
                
                client.ConnectToServer();
            }
        }

        public void Disconnect()
        {
            client?.Disconnect();
        }

        public void OnApplicationQuit()
        {
            Disconnect();
        }


        public void SendMessageToServer(string message)
        {
            client.SendMessage(message);
        }

        #region Packet Sending
        public void SendTCPData(Packet _packet)
        {
            client.SendTCPData(_packet);
        }

        public void SendUDPData(Packet _packet)
        {
            client.SendUDPData(_packet);
        }
        #endregion

        #region Events
        public void ClientConnectedToServer()
        {
            OnConnectedToServer?.Invoke();
        }

        public void ClientDisconnectedFromServer()
        {
            OnDisconnectedFromServer?.Invoke();
        }

        public void ReceiveMessageFromServer(string message)
        {
            OnReceiveMessageFromServer?.Invoke(message);
        }
        #endregion

        private void LogServerMessage(string message)
        {
            if (logMessagesFromServer)
                Debug.Log($"Message from server: {message}");
        }
    }
}
