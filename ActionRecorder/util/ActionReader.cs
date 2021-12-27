using System;
using System.IO;

namespace ActionRecorder.util
{
    class ActionReader : BinaryReader
    {
        public ActionReader(Stream input) : base(input)
        {
        }

        public override byte[] ReadBytes(int count)
        {
            byte[] res = new byte[count];
            for (int i = 0; i < count; i++)
            {
                string hex = new string(ReadChars(2));
                res[i] = Convert.ToByte(hex, 16);
            }
            return res;
        }

        public override byte ReadByte()
        {
            string hex = new string(ReadChars(2));
            return Convert.ToByte(hex, 16);
        }

        public override bool ReadBoolean()
        {
            return ReadByte() == 0x01 ? true : false;
        }

        public override string ReadString()
        {
            return new string(ReadChars(ReadByte()));
        }

        public override ushort ReadUInt16()
        {
            return BitConverter.ToUInt16(ReadBytes(2), 0);
        }

        public override short ReadInt16()
        {
            return (short)(ReadUInt16() - short.MaxValue - 1);
        }

        public override uint ReadUInt32()
        {
            return BitConverter.ToUInt32(ReadBytes(4), 0);
        }

        public override int ReadInt32()
        {
            return (int)(ReadUInt32() - int.MaxValue - 1);
        }

        public override ulong ReadUInt64()
        {
            ulong value = 0;
            value |= ((ulong)ReadByte() << 54);
            value |= ((ulong)ReadByte() << 48);
            value |= ((ulong)ReadByte() << 40);
            value |= ((ulong)ReadByte() << 32);
            value |= ((ulong)ReadByte() << 24);
            value |= ((ulong)ReadByte() << 16);
            value |= ((ulong)ReadByte() << 8);
            value |= ReadByte();
            return value;
        }

        public override long ReadInt64()
        {
            ulong a = ReadUInt64();
            return (long)(a - long.MaxValue - 1);
        }
    }
}
