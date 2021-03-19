using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualVoid.Networking
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ClientReceiveAttribute : Attribute
    {
        public string PacketID { get; private set; }

        public ClientReceiveAttribute(string PacketID)
        {
            this.PacketID = PacketID;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ServerReceiveAttribute : Attribute
    {
        public string PacketID { get; private set; }

        public ServerReceiveAttribute(string PacketID)
        {
            this.PacketID = PacketID;
        }
    }
}
