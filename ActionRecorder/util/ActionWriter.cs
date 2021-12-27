using System.IO;
using System.Text;

namespace ActionRecorder.util
{
    class ActionWriter : BinaryWriter
    {

        public ActionWriter(Stream output, Encoding encoding): base(output, encoding)
        {
        }

        public override void Write(byte value)
        {
            string hex = value.ToString("X2");
            Write(hex.ToCharArray());
        }

        public override void Write(bool value)
        {
            Write((byte)(value ? 0x01 : 0x00));
        }

        public override void Write(ushort value)
        {
            Write((byte)value);
            Write((byte)(value >> 8));
        }

        public override void Write(short value)
        {
            ushort c = (ushort)(value + short.MaxValue + 1);
            Write(c);
        }

        public override void Write(uint value)
        {
            Write((byte)value);
            Write((byte)(value >> 8));
            Write((byte)(value >> 16));
            Write((byte)(value >> 24));
        }

        public override void Write(int value)
        {
            uint c = (uint)(value + int.MaxValue + 1);
            Write(c);
        }

        public override void Write(ulong value)
        {
            Write((byte)(value >> 54));
            Write((byte)(value >> 48));
            Write((byte)(value >> 40));
            Write((byte)(value >> 32));
            Write((byte)(value >> 24));
            Write((byte)(value >> 16));
            Write((byte)(value >> 8));
            Write((byte)value);
        }

        public override void Write(long value)
        {
            ulong c = (ulong)(value + long.MaxValue + 1);
            Write(c);
        }
    }
}
