using Loamen.KeyMouseHook;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ActionRecorder
{
    public class ActionFile
    {
        public const string NAME = "RecordedAction";
        // To allow compatibility for onlder versions when changing the data structure
        public const string VERSION = "1.0.0";
        public DateTime RecordedDate = DateTime.Now;
        public List<MacroEvent> Actions { get; } = new List<MacroEvent>();
    }

    public static class BinaryActionFile
    {
        public static void Write(this BinaryWriter bw, ActionFile actionFile)
        {
            bw.Write(ActionFile.NAME);
            bw.Write(ActionFile.VERSION);
            bw.Write(actionFile.RecordedDate.ToBinary());
            bw.Write(actionFile.Actions.Count);

            foreach (var macroEvent in actionFile.Actions)
            {
                bw.Write((ushort)macroEvent.KeyMouseEventType);
                switch (macroEvent.KeyMouseEventType)
                {
                    case MacroEventType.MouseMove:
                    case MacroEventType.MouseMoveExt:
                    case MacroEventType.MouseDown:
                    case MacroEventType.MouseDownExt:
                    case MacroEventType.MouseUp:
                    case MacroEventType.MouseUpExt:
                    case MacroEventType.MouseWheel:
                    case MacroEventType.MouseWheelExt:
                    case MacroEventType.MouseDragStarted:
                    case MacroEventType.MouseDragFinished:
                    case MacroEventType.MouseClick:
                    case MacroEventType.MouseDoubleClick:
                        var mouseEvent = (MouseEventArgs)macroEvent.EventArgs;
                        bw.Write((uint)mouseEvent.Button);
                        bw.Write(mouseEvent.Clicks);
                        bw.Write(mouseEvent.X);
                        bw.Write(mouseEvent.Y);
                        bw.Write(mouseEvent.Delta);
                        break;
                    case MacroEventType.KeyUp:
                    case MacroEventType.KeyDown:
                        var keyEvent = (KeyEventArgs)macroEvent.EventArgs;
                        bw.Write((int)keyEvent.KeyData);
                        break;
                    case MacroEventType.KeyPress:
                        var keyPressEvent = (KeyPressEventArgs)macroEvent.EventArgs;
                        bw.Write(keyPressEvent.KeyChar);
                        break;
                }
                bw.Write(macroEvent.TimeSinceLastEvent);
            }
        }

        public static ActionFile ReadActionFile(this BinaryReader reader)
        {
            if (ActionFile.NAME != reader.ReadString())
                throw new InvalidOperationException("This is not a valid action file.");

            var version = reader.ReadString();
            if (ActionFile.VERSION != version)
                throw new InvalidOperationException($"File version {version} is not supported.");

            var actionFile = new ActionFile
            {
                RecordedDate = DateTime.FromBinary(reader.ReadInt64())
            };

            var countMacros = reader.ReadInt32();
            while (countMacros-- != 0)
            {
                var eventType = (MacroEventType)Enum.Parse(typeof(MacroEventType), reader.ReadUInt16().ToString());
                EventArgs eventArgs = null;
                switch (eventType)
                {
                    case MacroEventType.MouseMove:
                    case MacroEventType.MouseMoveExt:
                    case MacroEventType.MouseDown:
                    case MacroEventType.MouseDownExt:
                    case MacroEventType.MouseUp:
                    case MacroEventType.MouseUpExt:
                    case MacroEventType.MouseWheel:
                    case MacroEventType.MouseWheelExt:
                    case MacroEventType.MouseDragStarted:
                    case MacroEventType.MouseDragFinished:
                    case MacroEventType.MouseClick:
                    case MacroEventType.MouseDoubleClick:
                        eventArgs = new MouseEventArgs(
                            (MouseButtons)Enum.Parse(typeof(MouseButtons), reader.ReadUInt32().ToString()),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32()
                            );
                        break;
                    case MacroEventType.KeyUp:
                    case MacroEventType.KeyDown:
                        eventArgs = new KeyEventArgs((Keys)Enum.Parse(typeof(Keys), reader.ReadInt32().ToString()));
                        break;
                    case MacroEventType.KeyPress:
                        eventArgs = new KeyPressEventArgs(reader.ReadChar());
                        break;
                }
                actionFile.Actions.Add(new MacroEvent(eventType, eventArgs, reader.ReadInt32()));
            }

            return actionFile;
        }
    }
}