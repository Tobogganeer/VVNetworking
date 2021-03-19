using UnityEngine;
using System.Collections.Generic;

namespace VirtualVoid.Networking
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager instance;
        public void Awake()
        {
            instance = this;
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

        [HideInInspector] public static readonly string DEFAULT_SERVER_WELCOME_ID = "SERVER_WELCOME";
        [HideInInspector] public static readonly string DEFAULT_SERVER_DISCONNECT_ID = "SERVER_DISCONNECT";
        [HideInInspector] public static readonly string DEFAULT_SERVER_MESSAGE = "SERVER_MESSAGE";

        [HideInInspector] public static readonly string DEFAULT_SERVER_SPAWN_NETWORKENTITY = "SERVER_SPAWN_NETWORKENTITY";
        [HideInInspector] public static readonly string DEFAULT_SERVER_TRANSFORM_NETWORKENTITY = "SERVER_TRANSFORM_NETWORKENTITY";
        [HideInInspector] public static readonly string DEFAULT_SERVER_DESTROY_NETWORKENTITY = "SERVER_DESTROY_NETWORKENTITY";

        [HideInInspector] public static readonly string DEFAULT_CLIENT_WELCOME_RECEIVED = "CLIENT_WELCOME_RECEIVED";
        [HideInInspector] public static readonly string DEFAULT_CLIENT_MESSAGE = "CLIENT_MESSAGE";

        [HideInInspector] public static readonly string DEFAULT_CLIENT_RESEND_NETWORKENTITY = "CLIENT_RESEND_NETWORKENTITY";

        public static List<string> GetDefaultPacketIDs()
        {
            return new List<string>()
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
