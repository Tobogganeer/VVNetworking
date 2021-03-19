using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualVoid.Networking;
using VirtualVoid.Networking.Client;

public class MessageSender : MonoBehaviour
{
    public ClientInstance client;

    private void Start()
    {
        client.OnConnectedToServer += SendMessage;
    }

    private void SendMessage()
    {
        client.SendMessageToServer("Hello from the client!");
    }
}
