using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualVoid.Networking;
using VirtualVoid.Networking.Server;

public class CubeSpawner : MonoBehaviour
{
    public ServerInstance server;
    public Vector3 colour = new Vector3(10, 255, 50);

    private void Start()
    {
        server.OnClientConnected += SpawnCube;
    }

    private void SpawnCube(int _client)
    {
        using (Packet _packet = new Packet(1, PacketVerification.HASH))
        {
            _packet.Write(colour);
            server.server.SendTCPData(_client, _packet);
        }

        server.SendMessageToClient(_client, $"Spawned cube, colour {colour}");
    }

    private void FixedUpdate()
    {
        if (!server.server.clients.ContainsKey(1) || !server.server.clients[1].isConnected) return;

        transform.Rotate(new Vector3(5, 10, 7));

        using (Packet _packet = new Packet(2, PacketVerification.NONE))
        {
            _packet.Write(transform.rotation);
            server.server.SendTCPData(1, _packet);
        }
    }
}
