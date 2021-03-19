using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualVoid.Networking
{
    public class ServerNetworkEntity : MonoBehaviour
    {
        public static Dictionary<int, ServerNetworkEntity> entities = new Dictionary<int, ServerNetworkEntity>();
        private static int nextEntityID = 0;

        public int id { get; private set; } = -1;
        [Header("Used on the client to know which entity to spawn.")]
        public string entityType = "Entity";
        [Header("Which server this entity will be sent on.")]
        public Server.ServerInstance server;

        void Start()
        {
            id = nextEntityID;
            nextEntityID++;
            entities.Add(id, this);

            SendSpawnInformation();
        }

        public void SendSpawnInformation()
        {
            server.SpawnNetworkEntity(this);
        }

        void FixedUpdate()
        {
            server.TransformNetworkEntity(this);
        }

        void OnDestroy()
        {
            entities.Remove(id);
            server.DestroyNetworkEntity(this);
        }
    }

    public class ClientNetworkEntity : MonoBehaviour
    {
        public static Dictionary<int, ClientNetworkEntity> entities = new Dictionary<int, ClientNetworkEntity>();
        public int id { get; private set; } = -1;
        public string entityType = "Entity";

        public void Initialize(int id, string entityType)
        {
            this.id = id;
            this.entityType = entityType;
        }

        public static ClientNetworkEntity SpawnNetworkEntity(int id, string entityType, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (entities.ContainsKey(id))
            {
                Debug.LogWarning($"Tried to spawn entity (type: {entityType}) with ID {id}, but an entity with that ID has already been added!");
                return null;
            }

            ClientNetworkEntity entity = ClientNetworkEntityManager.SpawnClientNetworkEntity(entityType, position, rotation, scale)
                .AddComponent<ClientNetworkEntity>();
            entity.Initialize(id, entityType);
            entities.Add(entity.id, entity);
            return entity;
        }

        public static void TransformNetworkEntity(int id, Vector3 position, Quaternion rotation, Client.Client client)
        {
            if (!entities.ContainsKey(id))
            {
                Debug.LogWarning($"Tried to transform entity (ID: {id}), but no such entity with that ID exists! Asking server for information...");
                Debug.LogWarning($"(This will happen if objects were already spawned on the server)");

                client.ResendNetworkEntityDetails(id);
                return;
            }

            entities[id].gameObject.transform.position = position;
            entities[id].gameObject.transform.rotation = rotation;
        }

        public static void DestroyNetworkEntity(int id)
        {
            if (!entities.ContainsKey(id))
            {
                Debug.LogWarning($"Tried to destroy entity (ID: {id}), but no such entity with that ID exists!");
                return;
            }

            ClientNetworkEntity entity = entities[id];
            entities.Remove(id);

            Destroy(entity.gameObject);
        }
    }

    public class ClientNetworkEntityManager : MonoBehaviour
    {
        public static Dictionary<string, GameObject> allEntities { get; private set; } = new Dictionary<string, GameObject>();

        [Header("This is where to define your network entities. The EntityID entered must match the ID on the server.")]
        [Header("For example, I can put a cube on the server with entityID \"Cube\"")]
        [Header("Then when that object is spawned, whatever prefab on the client has entityID \"Cube\" (below) will be spawned.")]
        public NetworkEntity[] allNetworkEntities;

        void Awake()
        {
            allEntities.Clear();

            foreach (NetworkEntity entity in allNetworkEntities)
            {
                allEntities.Add(entity.entityID, entity.gameObject);
            }
        }

        public static GameObject SpawnClientNetworkEntity(string entityType, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (allEntities.ContainsKey(entityType))
            {
                GameObject ob = Instantiate(allEntities[entityType], position, rotation);
                ob.transform.localScale = scale;

                return ob;
            }
            else
            {
                Debug.LogError($"ClientNetworkEntityManager does not contain entity with entityId of \"{entityType}\"! Spawning cube...");
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = position;
                cube.transform.rotation = rotation;
                cube.transform.localScale = scale;

                return cube;
            }
        }
    }

    [Serializable]
    public struct NetworkEntity
    {
        public string entityID;
        public GameObject gameObject;
    }
}