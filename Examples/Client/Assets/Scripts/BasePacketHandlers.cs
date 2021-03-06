using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualVoid.Networking;

public class BasePacketHandlers
{
    static GameObject cube;

    [ClientReceive(1, PacketVerification.HASH)]
    public static void SpawnCube(Packet _packet)
    {
        Vector3 colour = _packet.ReadVector3();
        cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.GetComponent<MeshRenderer>().material.color = new Color(colour.x / 255f, colour.y / 255f, colour.z / 255f);
    }

    [ClientReceive(2, PacketVerification.NONE)]
    public static void RotateCube(Packet _packet)
    {
        Quaternion rotation = _packet.ReadQuaternion();
        if(cube != null) cube.transform.rotation = rotation;
    }
}
