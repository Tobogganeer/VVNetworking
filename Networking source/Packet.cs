using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualVoid.Networking
{
    public enum PacketVerification
    {
        NONE = 0,
        STRINGS = 1,
        HASH = 2
    }

    public enum PacketIDType
    {
        STRING,
        SHORT
    }

    public struct PacketID// : IEquatable<PacketID>
    {
        public string string_ID;
        public short short_ID;

        public PacketID(string string_ID)
        {
            this.string_ID = string_ID;
            this.short_ID = -1;
        }

        public PacketID(short short_ID)
        {
            this.string_ID = "";
            this.short_ID = short_ID;
        }

        public PacketID(string string_ID, short short_ID)
        {
            this.string_ID = string_ID;
            this.short_ID = short_ID;
        }

        public override bool Equals(object obj)
        {
            if (obj is PacketID id)
            {
                return IsSameAs(id);
            }
            return false;
        }

        public bool Equals(PacketID other)
        {
            return IsSameAs(other);
        }

        private bool IsSameAs(PacketID id)
        {
            if (id.string_ID == "" || this.string_ID == "")
            {
                return id.short_ID == this.short_ID;
            }
            else if (id.short_ID == -1 || this.short_ID == -1)
            {
                return id.string_ID == this.string_ID;
            }
            else
            {
                return id.short_ID == this.short_ID &&
                    id.string_ID == this.string_ID;
            }
        }

        public override int GetHashCode()
        {
            int hashCode = 80902019;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(string_ID);
            hashCode = hashCode * -1521134295 + short_ID.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return $"STRING_ID: {string_ID} - SHORT_ID: {short_ID}";
        }


        public static bool operator ==(PacketID id1, PacketID id2)
        {
            return id1.Equals(id2);
        }

        public static bool operator !=(PacketID id1, PacketID id2)
        {
            return !id1.Equals(id2);
        }


        public static explicit operator string(PacketID id)
        {
            return id.string_ID;
        }

        public static explicit operator short(PacketID id)
        {
            return id.short_ID;
        }

        public static implicit operator PacketID(string id)
        {
            return new PacketID(id);
        }

        public static implicit operator PacketID(short id)
        {
            return new PacketID(id);
        }
    }

    //public class PacketIDEqualityComparer : IEqualityComparer<PacketID>
    //{
    //    public bool Equals(PacketID x, PacketID y)
    //    {
    //        return x.Equals(y);
    //    }
    //
    //    public int GetHashCode(PacketID obj)
    //    {
    //        return obj.GetHashCode();
    //    }
    //}

    public class Packet : IDisposable
    {
        private List<byte> buffer;
        private byte[] readableBuffer;
        private int readPos;

        /// <summary>Creates a new empty packet (without an ID).</summary>
        public Packet()
        {
            buffer = new List<byte>(); // Intitialize buffer
            readPos = 0; // Set readPos to 0
        }

        /// <summary>Creates a new packet with a given ID. Used for sending.</summary>
        /// <param name="_id">The packet ID.</param>
        public Packet(PacketID _id, PacketVerification verification)
        {
            //if (NetworkManager.GetDefaultPacketIDs().Contains()) // Doesn't work, default packets are made this way too :P

            buffer = new List<byte>(); // Intitialize buffer
            readPos = 0; // Set readPos to 0

            if (!LegalPacketID(_id, NetworkManager.instance.packetIDType)) throw new InvalidOperationException("Invalid packet ID for set PacketIDType!");

            if (NetworkManager.instance.packetIDType == PacketIDType.SHORT) Write(_id.short_ID); // Write packet id to the buffer
            else Write(_id.string_ID);
            PackVerification(verification);
        }

        /// <summary>Creates a packet from which data can be read. Used for receiving.</summary>
        /// <param name="_data">The bytes to add to the packet.</param>
        public Packet(byte[] _data)
        {
            buffer = new List<byte>(); // Intitialize buffer
            readPos = 0; // Set readPos to 0

            SetBytes(_data);
        }

        private bool LegalPacketID(PacketID _id, PacketIDType _packetIDType)
        {
            if (_packetIDType == PacketIDType.SHORT && _id.short_ID == -1)
            {
                Debug.LogError($"Packet with string ID ({_id.string_ID}) has short ID of -1 despite NetworkManager.packetIDType being set to PacketIDType.SHORT!");
                return false;
            }
            else if (_packetIDType == PacketIDType.STRING && _id.string_ID == "")
            {
                Debug.LogError($"Packet with short ID ({_id.short_ID}) has string ID of null despite NetworkManager.packetIDType being set to PacketIDType.STRING!");
                return false;
            }
            return true;
        }

        private void PackVerification(PacketVerification verification)
        {
            Write((byte)verification);

            switch (verification)
            {
                case PacketVerification.NONE:
                    break;
                case PacketVerification.STRINGS:
                    Write(NetworkManager.instance.APPLICATION_ID);
                    Write(NetworkManager.instance.VERSION);
                    break;
                case PacketVerification.HASH:
                    Write(NetworkManager.instance.APPLICATION_ID.GetHashCode());
                    Write(NetworkManager.instance.VERSION.GetHashCode());
                    break;
            }
        }

        #region Functions
        /// <summary>Sets the packet's content and prepares it to be read.</summary>
        /// <param name="_data">The bytes to add to the packet.</param>
        public void SetBytes(byte[] _data)
        {
            Write(_data);
            readableBuffer = buffer.ToArray();
        }

        /// <summary>Inserts the length of the packet's content at the start of the buffer.</summary>
        public void WriteLength()
        {
            buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count)); // Insert the byte length of the packet at the very beginning
        }

        /// <summary>Inserts the given int at the start of the buffer.</summary>
        /// <param name="_value">The int to insert.</param>
        public void InsertInt(int _value)
        {
            buffer.InsertRange(0, BitConverter.GetBytes(_value)); // Insert the int at the start of the buffer
        }

        /// <summary>Gets the packet's content in array form.</summary>
        public byte[] ToArray()
        {
            readableBuffer = buffer.ToArray();
            return readableBuffer;
        }

        /// <summary>Gets the length of the packet's content.</summary>
        public int Length()
        {
            return buffer.Count; // Return the length of buffer
        }

        /// <summary>Gets the length of the unread data contained in the packet.</summary>
        public int UnreadLength()
        {
            return Length() - readPos; // Return the remaining length (unread)
        }

        /// <summary>Resets the packet instance to allow it to be reused.</summary>
        /// <param name="_shouldReset">Whether or not to reset the packet.</param>
        public void Reset(bool _shouldReset = true)
        {
            if (_shouldReset)
            {
                buffer.Clear(); // Clear buffer
                readableBuffer = null;
                readPos = 0; // Reset readPos
            }
            else
            {
                readPos -= 4; // "Unread" the last read int
            }
        }

        public void Encrypt(Encryption.NetworkEncryptionType encryptionType, string key)
        {
            switch (encryptionType)
            {
                case Encryption.NetworkEncryptionType.NONE:
                    return;
                case Encryption.NetworkEncryptionType.AES256:
                    if (key.Length != 32)
                        key = "00000000000000000000000000000000";
                    byte[] contents = buffer.ToArray();
                    buffer.Clear();
                    buffer.AddRange(Encryption.NetworkEncryption.EncryptBytes(key, contents));
                    break;
            }
        }

        public void Decrypt(Encryption.NetworkEncryptionType encryptionType, string key)
        {
            switch (encryptionType)
            {
                case Encryption.NetworkEncryptionType.NONE:
                    return;
                case Encryption.NetworkEncryptionType.AES256:
                    if (key.Length != 32)
                        key = "00000000000000000000000000000000";
                    byte[] contents = buffer.ToArray();
                    buffer.Clear();
                    buffer.AddRange(Encryption.NetworkEncryption.DecryptBytes(key, contents));
                    break;
            }
        }
        #endregion

        #region Write Data
        /// <summary>Adds a byte to the packet.</summary>
        /// <param name="_value">The byte to add.</param>
        public void Write(byte _value)
        {
            buffer.Add(_value);
        }
        /// <summary>Adds an array of bytes to the packet.</summary>
        /// <param name="_value">The byte array to add.</param>
        public void Write(byte[] _value)
        {
            buffer.AddRange(_value);
        }
        /// <summary>Adds a short to the packet.</summary>
        /// <param name="_value">The short to add.</param>
        public void Write(short _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds an int to the packet.</summary>
        /// <param name="_value">The int to add.</param>
        public void Write(int _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds a long to the packet.</summary>
        /// <param name="_value">The long to add.</param>
        public void Write(long _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds a float to the packet.</summary>
        /// <param name="_value">The float to add.</param>
        public void Write(float _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds a bool to the packet.</summary>
        /// <param name="_value">The bool to add.</param>
        public void Write(bool _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds a string to the packet.</summary>
        /// <param name="_value">The string to add.</param>
        public void Write(string _value)
        {
            Write(_value.Length); // Add the length of the string to the packet
            buffer.AddRange(Encoding.ASCII.GetBytes(_value)); // Add the string itself
        }
        /// <summary>Adds a Vector3 to the packet.</summary>
        /// <param name="_value">The Vector3 to add.</param>
        public void Write(Vector3 _value)
        {
            Write(_value.x);
            Write(_value.y);
            Write(_value.z);
        }
        /// <summary>Adds a Quaternion to the packet.</summary>
        /// <param name="_value">The Quaternion to add.</param>
        public void Write(Quaternion _value, bool _compress = true)
        {
            Write(_compress);

            if (_compress)
            {
                Write(_value.eulerAngles.x);
                Write(_value.eulerAngles.y);
                Write(_value.eulerAngles.z);
            }
            else
            {
                Write(_value.x);
                Write(_value.y);
                Write(_value.z);
                Write(_value.w);
            }
        }
        #endregion

        #region Read Data
        /// <summary>Reads a byte from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public byte ReadByte(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                byte _value = readableBuffer[readPos]; // Get the byte at readPos' position
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 1; // Increase readPos by 1
                }
                return _value; // Return the byte
            }
            else
            {
                throw new Exception("Could not read value of type 'byte'!");
            }
        }

        /// <summary>Reads an array of bytes from the packet.</summary>
        /// <param name="_length">The length of the byte array.</param>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public byte[] ReadBytes(int _length, bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                byte[] _value = buffer.GetRange(readPos, _length).ToArray(); // Get the bytes at readPos' position with a range of _length
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += _length; // Increase readPos by _length
                }
                return _value; // Return the bytes
            }
            else
            {
                throw new Exception("Could not read value of type 'byte[]'!");
            }
        }

        /// <summary>Reads a short from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public short ReadShort(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                short _value = BitConverter.ToInt16(readableBuffer, readPos); // Convert the bytes to a short
                if (_moveReadPos)
                {
                    // If _moveReadPos is true and there are unread bytes
                    readPos += 2; // Increase readPos by 2
                }
                return _value; // Return the short
            }
            else
            {
                throw new Exception("Could not read value of type 'short'!");
            }
        }

        /// <summary>Reads an int from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public int ReadInt(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                int _value = BitConverter.ToInt32(readableBuffer, readPos); // Convert the bytes to an int
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 4; // Increase readPos by 4
                }
                return _value; // Return the int
            }
            else
            {
                throw new Exception("Could not read value of type 'int'!");
            }
        }

        /// <summary>Reads a long from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public long ReadLong(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                long _value = BitConverter.ToInt64(readableBuffer, readPos); // Convert the bytes to a long
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 8; // Increase readPos by 8
                }
                return _value; // Return the long
            }
            else
            {
                throw new Exception("Could not read value of type 'long'!");
            }
        }

        /// <summary>Reads a float from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public float ReadFloat(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                float _value = BitConverter.ToSingle(readableBuffer, readPos); // Convert the bytes to a float
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 4; // Increase readPos by 4
                }
                return _value; // Return the float
            }
            else
            {
                throw new Exception("Could not read value of type 'float'!");
            }
        }

        /// <summary>Reads a bool from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public bool ReadBool(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                bool _value = BitConverter.ToBoolean(readableBuffer, readPos); // Convert the bytes to a bool
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 1; // Increase readPos by 1
                }
                return _value; // Return the bool
            }
            else
            {
                throw new Exception("Could not read value of type 'bool'!");
            }
        }

        /// <summary>Reads a string from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public string ReadString(bool _moveReadPos = true)
        {
            try
            {
                int _length = ReadInt(); // Get the length of the string
                string _value = Encoding.ASCII.GetString(readableBuffer, readPos, _length); // Convert the bytes to a string
                if (_moveReadPos && _value.Length > 0)
                {
                    // If _moveReadPos is true string is not empty
                    readPos += _length; // Increase readPos by the length of the string
                }
                return _value; // Return the string
            }
            catch
            {
                throw new Exception("Could not read value of type 'string'!");
            }
        }

        public Vector3 ReadVector3(bool _moveReadPos = true)
        {
            return new Vector3(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos));
        }

        public Quaternion ReadQuaternion(bool _moveReadPos = true)
        {
            bool compress = ReadBool();

            if (compress) return Quaternion.Euler(new Vector3(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos)));

            else return new Quaternion(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos));
        }
        #endregion

        private bool disposed = false;

        protected virtual void Dispose(bool _disposing)
        {
            if (!disposed)
            {
                if (_disposing)
                {
                    buffer = null;
                    readableBuffer = null;
                    readPos = 0;
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
