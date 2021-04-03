using UnityEngine;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace VirtualVoid.Networking
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager instance;
        public void Awake()
        {
            instance = this;
            SHA256 sha = SHA256.Create();
        }

        [Header("PLEASE REMEMBER TO ATTACH A ThreadManager COMPONENT TO A GAMEOBJECT, AND A TickLogic COMPONENT IF USING Interpolator SCRIPT ON OBJECTS!")]
        [Header("You will also need a ClientNetworkEntityManager component attached to a GameObject in order to use ClientNetworkEntities.")]
        [Space]
        [Header("These fields must match between client and server!")]
        public string APPLICATION_ID;
        public string VERSION;
        [Header("TESTING! Encryption does NOT currently work!")]
        public Encryption.NetworkEncryptionType encryptionType;
        [Header("Must match between client and server, 32 characters long.")]
        public string encryptionKey;

        [Space]
        [Header("The ID used to identify packets. Must match between client and server.")]
        [Header("Using a short will bloat the packet less. Can be simplified by using an enum and casting to a short. Do not use negative shorts.")]
        [Header("(Empty string is null string packet ID and -1 is null short packet ID, other negative numbers are used by internal functions)")]
        public PacketIDType packetIDType = PacketIDType.SHORT;


        //packets received from server
        [HideInInspector] public static readonly PacketID DEFAULT_SERVER_WELCOME_ID = new PacketID("SERVER_WELCOME", -2);
        [HideInInspector] public static readonly PacketID DEFAULT_SERVER_DISCONNECT_ID = new PacketID("SERVER_DISCONNECT", -3);
        [HideInInspector] public static readonly PacketID DEFAULT_SERVER_MESSAGE = new PacketID("SERVER_MESSAGE", -4);

        [HideInInspector] public static readonly PacketID DEFAULT_SERVER_SPAWN_NETWORKENTITY = new PacketID("SERVER_SPAWN_NETWORKENTITY", -5);
        [HideInInspector] public static readonly PacketID DEFAULT_SERVER_TRANSFORM_NETWORKENTITY = new PacketID("SERVER_TRANSFORM_NETWORKENTITY", -6);
        [HideInInspector] public static readonly PacketID DEFAULT_SERVER_DESTROY_NETWORKENTITY = new PacketID("SERVER_DESTROY_NETWORKENTITY", -7);

        //packets received from client
        [HideInInspector] public static readonly PacketID DEFAULT_CLIENT_WELCOME_RECEIVED = new PacketID("CLIENT_WELCOME_RECEIVED", -2);
        [HideInInspector] public static readonly PacketID DEFAULT_CLIENT_MESSAGE = new PacketID("CLIENT_MESSAGE", -3);

        [HideInInspector] public static readonly PacketID DEFAULT_CLIENT_RESEND_NETWORKENTITY = new PacketID("CLIENT_RESEND_NETWORKENTITY", -4);

        public static List<PacketID> GetDefaultPacketIDs()
        {
            return new List<PacketID>()
            {
                DEFAULT_SERVER_WELCOME_ID,
                DEFAULT_SERVER_DISCONNECT_ID,
                DEFAULT_SERVER_MESSAGE,

                DEFAULT_SERVER_SPAWN_NETWORKENTITY,
                DEFAULT_SERVER_TRANSFORM_NETWORKENTITY,
                DEFAULT_SERVER_DESTROY_NETWORKENTITY,

                DEFAULT_CLIENT_WELCOME_RECEIVED,
                DEFAULT_CLIENT_MESSAGE
            };
        }
    }
}
