using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ActionRecorder.scheduler;
using ActionRecorder.structure;
using Gma.System.MouseKeyHook;
using Loamen.KeyMouseHook;

namespace ActionRecorder
{
    public class Application
    {
        private static Application _instance;
        private Scheduler _scheduler;
        private MainWindow _mainWindow;
        private bool _running = false;
        private int _tickRate = 60;

        private bool isRecording = false;
        private bool isPlaying = false;

        private bool recordingWithLoop = false;

        private Thread _playbackThread;
        private Thread _parseThread;
        private Thread _exportThread;

        private ActionFile _actionFile = null;

        private readonly KeyMouseFactory _eventHookFactory = new KeyMouseFactory(Hook.GlobalEvents());
        private readonly KeyboardWatcher _keyboardWatcher;
        private readonly KeyboardWatcher _shortcutWatcher;
        private readonly MouseWatcher _mouseWatcher;

        public const int ACTIONS_SIZE = 4 * 1024;

        // a mouse event is 22 bytes, so I think that number wont be a problem
        public const int ACTION_BUFFER_SIZE = 2 * 22 * ACTIONS_SIZE;

        private int _simulateAction = 0;

        public Application(MainWindow mainWindow)
        {
            _instance = this;
            _mainWindow = mainWindow;
            _scheduler = new Scheduler();
            _scheduler.Start();
            _keyboardWatcher = _eventHookFactory.GetKeyboardWatcher();
            _keyboardWatcher.OnKeyboardInput += GlobalHookHandler;
            _shortcutWatcher = new KeyMouseFactory(Hook.GlobalEvents()).GetKeyboardWatcher();
            _shortcutWatcher.OnKeyboardInput += ShortcutHandler;
            _shortcutWatcher.Start(Hook.GlobalEvents());
            _mouseWatcher = _eventHookFactory.GetMouseWatcher();
            _mouseWatcher.OnMouseInput += GlobalHookHandler;
            StartWatch(Hook.GlobalEvents());
        }

        public void Record()
        {
            if (isPlaying)
            {
                Log("You can't record actions while playing!");
                return;
            }
            if (!isRecording)
            {
                _mainWindow.ClearLog();
                _actionFile = new ActionFile
                {
                    RecordedDate = DateTime.Now,
                    Loop = RecordWithLoop
                };
                StartWatch(Hook.GlobalEvents());
            }
            isRecording = !isRecording;
        }

        private void StartWatch(IKeyboardMouseEvents events = null)
        {
            _keyboardWatcher.Start(events);
            _mouseWatcher.Start(events);
        }

        private void ShortcutHandler(object sender, MacroEvent e)
        {
            if (e.KeyMouseEventType != MacroEventType.KeyUp) return;
            var keyEvent = (KeyEventArgs)e.EventArgs;
            switch (keyEvent.KeyCode)
            {
                case Keys.F9:
                    _mainWindow.RecordHook();
                    break;
                case Keys.F10:
                    _mainWindow.PlayOrStopHook();
                    break;
            }
        }

        private void GlobalHookHandler(object sender, MacroEvent e)
        {
            if (!isRecording)
            {
                return;
            }
            if (e.KeyMouseEventType == MacroEventType.KeyUp || e.KeyMouseEventType == MacroEventType.KeyDown)
            {
                var keyEvent = (KeyEventArgs)e.EventArgs;
                switch (keyEvent.KeyCode)
                {
                    case Keys.F9:
                    case Keys.F10:
                        return;
                }
            }
            var actionCount = _actionFile.ActionsSize;

            if (actionCount < ACTIONS_SIZE)
            {
                var lastAction = actionCount < 1 ? null : _actionFile.Actions[actionCount - 1];

                if (e.EventArgs is MouseEventExtArgs)
                {
                    MouseEventExtArgs extArgs = (MouseEventExtArgs)e.EventArgs;
                    e.EventArgs = new MouseEventArgs(extArgs.Button, extArgs.Clicks, extArgs.X, extArgs.Y, extArgs.Delta);
                }
                else if (e.EventArgs is KeyEventArgsExt)
                {
                    KeyEventArgsExt extArgs = (KeyEventArgsExt)e.EventArgs;
                    e.EventArgs = new KeyEventArgs(extArgs.KeyData);
                }
                else if (e.EventArgs is KeyPressEventArgsExt)
                {
                    KeyPressEventArgsExt extArgs = (KeyPressEventArgsExt)e.EventArgs;
                    e.EventArgs = new KeyPressEventArgs(extArgs.KeyChar);
                }
                _actionFile.Actions[_actionFile.ActionsSize++] = e;
                var timeSinceLastEvent = lastAction == null ? "0" : lastAction.TimeSinceLastEvent.ToString();
                Log($"[A:{actionCount}] [LT:{timeSinceLastEvent}] {e.KeyMouseEventType.ToString()} recorded.");
            }
            else
            {
                Log($"You have reached the limit of {ACTIONS_SIZE} actions.");
                _mainWindow.RecordHook();
            }
        }

        public void Playback()
        {
            if (!isRecording && _actionFile != null)
            {
                if (!isPlaying)
                {
                    _mainWindow.ClearLog();
                    _playbackThread = new Thread(() =>
                    {
                        var sim = new InputSimulator();
                        sim.OnPlayback += OnPlayback;
                        if (_actionFile.Loop)
                        {
                            do
                            {
                                try
                                {
                                    _simulateAction = 0;
                                    sim.PlayBack(_actionFile.Actions);
                                }
                                catch (NullReferenceException)
                                {
                                    continue;
                                }
                            } while (isPlaying);
                        }
                        else
                        {
                            try
                            {
                                _simulateAction = 0;
                                sim.PlayBack(_actionFile.Actions);
                            }
                            catch (NullReferenceException) { }
                            Thread.Sleep(200);
                            isPlaying = false;
                            _mainWindow.Update();
                            _playbackThread.Abort();
                        }
                    });
                    _playbackThread?.Start();
                }
                else
                {
                    _playbackThread?.Abort();
                }
                isPlaying = !isPlaying;
            }
            else
            {
                Warn(_actionFile != null ? "Hey! First stop recording to execute this action." : "Hey! First record your actions to play.");
            }
        }

        private void OnPlayback(object sender, MacroEvent e)
        {
            Log($"[A:{_simulateAction++}] Simulating {e.KeyMouseEventType}!");
        }

        public void ImportAction()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = @"Recorded Action|*.ra";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var filePath = openFileDialog.FileName;
                    byte[] buffer = new byte[ACTION_BUFFER_SIZE];
                    var fileStream = openFileDialog.OpenFile();

                    using (Stream reader = File.OpenRead(filePath))
                    {
                        int bytes = reader.Read(buffer, 0, ACTION_BUFFER_SIZE);
                        Log($"Trying to import Action File from {filePath}...");
                        try
                        {
                            Parse(buffer);
                            Log($"{bytes} bytes were read!");
                        }
                        catch (Exception e)
                        {
                            Error($"Not is possible to load action file, reason: {e.Message}");
                            _parseThread?.Abort();
                        }
                    }
                }
            }
        }

        public void Parse(byte[] content)
        {
            _parseThread?.Abort();
            _parseThread = new Thread(() =>
            {
                ActionFile actionFile = new ActionFile();

                using (MemoryStream stream = new MemoryStream(content))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        actionFile.Name = reader.ReadString();
                        actionFile.RecordedDate = DateTime.FromBinary(reader.ReadInt64());
                        actionFile.Loop = reader.ReadBoolean();
                        int countMacros = reader.ReadInt32();
                        actionFile.ActionsSize = countMacros;
                        while (countMacros-- != 0)
                        {
                            MacroEventType eventType = (MacroEventType)Enum.Parse(typeof(MacroEventType), reader.ReadUInt16().ToString());
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
                            actionFile.Actions[actionFile.ActionsSize - countMacros - 1] = new MacroEvent(eventType, eventArgs, reader.ReadInt32());
                        }
                    }
                }
                _actionFile = actionFile;
                Log("Action File Loaded and able to play!");
                _parseThread.Abort();
            });
            _parseThread?.Start();
        }

        public void ExportAction()
        {
            if (_actionFile == null)
            {
                Warn("Please, first record an action to export!");
                return;
            }
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = @"Recorded Action|*.ra";
            saveFileDialog1.Title = @"Exporting...";
            saveFileDialog1.RestoreDirectory = true;

            if (saveFileDialog1.ShowDialog() != DialogResult.OK) return;

            _exportThread?.Abort();
            _exportThread = new Thread(() =>
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(stream))
                    {
                        bw.Write(_actionFile.Name);
                        bw.Write(_actionFile.RecordedDate.ToBinary());
                        bw.Write(_actionFile.Loop);
                        bw.Write(_actionFile.ActionsSize);

                        for (int i = 0; i < _actionFile.ActionsSize; i++)
                        {
                            MacroEvent macroEvent = _actionFile.Actions[i];
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
                                    MouseEventArgs mouseEvent = (MouseEventArgs)macroEvent.EventArgs;
                                    bw.Write((uint)mouseEvent.Button);
                                    bw.Write(mouseEvent.Clicks);
                                    bw.Write(mouseEvent.X);
                                    bw.Write(mouseEvent.Y);
                                    bw.Write(mouseEvent.Delta);
                                    break;
                                case MacroEventType.KeyUp:
                                case MacroEventType.KeyDown:
                                    KeyEventArgs keyEvent = (KeyEventArgs)macroEvent.EventArgs;
                                    bw.Write((int)keyEvent.KeyData);
                                    break;
                                case MacroEventType.KeyPress:
                                    KeyPressEventArgs keyPressEvent = (KeyPressEventArgs)macroEvent.EventArgs;
                                    bw.Write(keyPressEvent.KeyChar);
                                    break;
                            }
                            bw.Write(macroEvent.TimeSinceLastEvent);
                        }
                        using (Stream fs = saveFileDialog1.OpenFile())
                        {
                            fs.Write(stream.GetBuffer(), 0, (int)stream.Position);
                        }
                    }
                }
                Log("Exported!");
                _exportThread.Abort();
            });
            _exportThread?.Start();
        }

        public bool Running
        {
            get => _running;
            set => _running = value;
        }

        public bool IsPlaying
        {
            get => isPlaying;
            set => isPlaying = value;
        }

        public bool RecordWithLoop
        {
            get => recordingWithLoop;
            set => recordingWithLoop = value;
        }

        public bool IsRecording => isRecording;

        public Scheduler Scheduler => _scheduler;

        public int TickRate => _tickRate;

        public static Application Instance => _instance;

        public void Log(string message)
        {
            message = $@"[LOG] {message}";
            _mainWindow.LogMessage(message);
            Console.WriteLine(message);
        }

        public void Info(string message)
        {
            message = $@"[INFO] {message}";
            _mainWindow.LogMessage(message);
            Console.WriteLine(message);
        }

        public void Warn(string message)
        {
            message = $@"[WARN] {message}";
            _mainWindow.LogMessage(message);
            Console.WriteLine(message);
        }

        public void Error(string message)
        {
            message = $@"[ERROR] {message}";
            _mainWindow.LogMessage(message);
            Console.WriteLine(message);
        }
    }
}