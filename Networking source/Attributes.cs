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
        public PacketID PacketID { get; private set; }
        public PacketVerification ExpectedVerification { get; private set; }

        public ClientReceiveAttribute(string packetID, PacketVerification expectedVerification = PacketVerification.NONE)
        {
            PacketID = packetID;
            ExpectedVerification = expectedVerification;
        }

        public ClientReceiveAttribute(short packetID, PacketVerification expectedVerification = PacketVerification.NONE)
        {
            PacketID = packetID;
            ExpectedVerification = expectedVerification;
        }

        public ClientReceiveAttribute(string string_packetID, short short_packetID, PacketVerification expectedVerification = PacketVerification.NONE)
        {
            PacketID = new PacketID(string_packetID, short_packetID);
            ExpectedVerification = expectedVerification;
        }

        public ClientReceiveAttribute(PacketID packetID, PacketVerification expectedVerification = PacketVerification.NONE)
        {
            PacketID = packetID;
            ExpectedVerification = expectedVerification;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ServerReceiveAttribute : Attribute
    {
        public PacketID PacketID { get; private set; }
        public PacketVerification ExpectedVerification { get; private set; }

        public ServerReceiveAttribute(string packetID, PacketVerification expectedVerification = PacketVerification.NONE)
        {
            PacketID = packetID;
            ExpectedVerification = expectedVerification;
        }

        public ServerReceiveAttribute(short packetID, PacketVerification expectedVerification = PacketVerification.NONE)
        {
            PacketID = packetID;
            ExpectedVerification = expectedVerification;
        }

        public ServerReceiveAttribute(string string_packetID, short short_packetID, PacketVerification expectedVerification = PacketVerification.NONE)
        {
            PacketID = new PacketID(string_packetID, short_packetID);
            ExpectedVerification = expectedVerification;
        }

        public ServerReceiveAttribute(PacketID packetID, PacketVerification expectedVerification = PacketVerification.NONE)
        {
            PacketID = packetID;
            ExpectedVerification = expectedVerification;
        }
    }
}
