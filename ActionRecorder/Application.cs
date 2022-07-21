using Gma.System.MouseKeyHook;
using Loamen.KeyMouseHook;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ActionRecorder
{
    public class Application
    {
        private readonly MainWindow _mainWindow;
        private Thread _playbackThread;
        private Thread _parseThread;
        private Thread _exportThread;

        private ActionFile _actionFile = null;

        private readonly KeyMouseFactory _eventHookFactory = new KeyMouseFactory(Hook.GlobalEvents());
        private readonly KeyboardWatcher _keyboardWatcher;
        private readonly KeyboardWatcher _shortcutWatcher;
        private readonly MouseWatcher _mouseWatcher;

        public const Keys PLAY_KEY_HOOK = Keys.F10;
        public const Keys RECORD_KEY_HOOK = Keys.F9;

        private int _simulateAction = 0;

        public bool IsPlaying { get; set; }

        private bool _loop;
        public bool Loop
        {
            get => _loop;
            set
            {
                _loop = value;
                _mainWindow.Update();
            }
        }

        private bool _suppressMouseMovePath;
        public bool SuppressMouseMovePath
        {
            get => _suppressMouseMovePath;
            set
            {
                _suppressMouseMovePath = value;
                _mainWindow.Update();
            }
        }

        public double? SpeedMultiplier { get; set; }
        public int? FixedSpeed { get; set; }

        public bool IsRecording { get; private set; } = false;

        public static Application Instance { get; private set; }

        public Application(MainWindow mainWindow)
        {
            Instance = this;
            _mainWindow = mainWindow;
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
            if (IsPlaying)
            {
                Log("You can't record actions while playing!");
                return;
            }

            if (!IsRecording)
            {
                _mainWindow.ClearLog();
                _actionFile = new ActionFile
                {
                    RecordedDate = DateTime.Now
                };
                StartWatch(Hook.GlobalEvents());
                Info("You are now recording...");
            }
            else
                Info("End recording");
            IsRecording = !IsRecording;
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
                case RECORD_KEY_HOOK:
                    _mainWindow.RecordHook();
                    break;
                case PLAY_KEY_HOOK:
                    _mainWindow.PlayOrStopHook();
                    break;
            }
        }

        private void GlobalHookHandler(object sender, MacroEvent e)
        {
            if (!IsRecording)
                return;

            if (e.KeyMouseEventType == MacroEventType.KeyUp || e.KeyMouseEventType == MacroEventType.KeyDown)
            {
                var keyEvent = (KeyEventArgs)e.EventArgs;
                switch (keyEvent.KeyCode)
                {
                    case RECORD_KEY_HOOK:
                    case PLAY_KEY_HOOK:
                        return;
                }
            }

            switch (e.EventArgs)
            {
                case MouseEventExtArgs mouseEvent:
                    e.EventArgs = new MouseEventArgs(mouseEvent.Button, mouseEvent.Clicks, mouseEvent.X, mouseEvent.Y, mouseEvent.Delta);
                    break;
                case KeyEventArgsExt keyEvent:
                    e.EventArgs = new KeyEventArgs(keyEvent.KeyData);
                    break;
                case KeyPressEventArgsExt keyPressEvent:
                    e.EventArgs = new KeyPressEventArgs(keyPressEvent.KeyChar);
                    break;
            }

            var lastAction = _actionFile.Actions.LastOrDefault();
            _actionFile.Actions.Add(e);
            var timeSinceLastEvent = lastAction == null ? "0" : lastAction.TimeSinceLastEvent.ToString();
            Log($"[A:{_actionFile.Actions.Count}] [LT:{timeSinceLastEvent}] {e.KeyMouseEventType} recorded.");
        }

        public void Playback()
        {
            if (IsRecording || _actionFile == null)
            {
                Warn(_actionFile != null ? "Hey! First stop recording to execute this action." : "Hey! First record your actions to play.");
                return;
            }

            if (!IsPlaying)
            {
                Info("You are now playing...");
                
                _mainWindow.ClearLog();
                _playbackThread = new Thread(() =>
                {
                    var sim = new InputSimulator();
                    sim.OnPlayback += OnPlayback;
                    if (Loop)
                    {
                        do
                        {
                            _simulateAction = 0;
                            var actions = GetActions();
                            sim.PlayBack(actions);
                        } while (IsPlaying);
                    }
                    else
                    {
                        _simulateAction = 0;
                        var actions = GetActions();
                        sim.PlayBack(actions);
                        Thread.Sleep(200);
                        IsPlaying = false;
                        _mainWindow.Update();
                    }
                    Info("End playing");
                });
                _playbackThread?.Start();
            }
            else
            {
                _playbackThread?.Abort();
                Info("Playback aborted");
            }
            IsPlaying = !IsPlaying;
        }

        private List<MacroEvent> GetActions()
        {
            // For some reason sometimes a null action is recorded
            var actions = _actionFile.Actions.Where(x => x != null).ToList();

            bool isMouseMove(MacroEventType type) =>
                (MacroEventType.MouseMove | MacroEventType.MouseMoveExt).HasFlag(type);
            bool isKeyDown(MacroEventType type) =>
                (MacroEventType.MouseDown | MacroEventType.MouseDownExt | MacroEventType.KeyDown).HasFlag(type);
            bool isKeyUp(MacroEventType type) =>
                (MacroEventType.MouseUp | MacroEventType.MouseUpExt | MacroEventType.KeyUp).HasFlag(type);

            string eventToKeyString(EventArgs eventArgs)
            {
                switch (eventArgs)
                {
                    case MouseEventArgs mouseEvent:
                        return $"Mouse_{mouseEvent.Button}";
                    case KeyEventArgs keyEvent:
                        return $"Key_{keyEvent.KeyCode}";
                    case KeyPressEventArgs keyPressEvent:
                        return $"KeyPress_{keyPressEvent.KeyChar}";
                    default:
                        return null;
                }
            }

            var keysDown = new HashSet<string>();
            bool suppressMouseMovePath(MacroEvent e)
            {
                var idx = actions.IndexOf(e);
                var hasNext = idx < actions.Count - 1;
                var nextIsMouseMove = hasNext && isMouseMove(actions[idx + 1].KeyMouseEventType);

                var keyStr = eventToKeyString(e.EventArgs);
                if (isKeyDown(e.KeyMouseEventType))
                    _ = keysDown.Add(keyStr);
                else if (isKeyUp(e.KeyMouseEventType))
                    _ = keysDown.Remove(keyStr);

                var isHoldingAnyKey = keysDown.Any();

                return !isMouseMove(e.KeyMouseEventType) || !nextIsMouseMove || isHoldingAnyKey;
            }

            var actionsResult = actions.AsEnumerable();

            if (SuppressMouseMovePath)
                actionsResult = actionsResult.Where(x => suppressMouseMovePath(x));

            if (SpeedMultiplier != null || FixedSpeed != null)
            {
                actionsResult = actionsResult.Select(x =>
                {
                    var clone = new MacroEvent(x.KeyMouseEventType, x.EventArgs, x.TimeSinceLastEvent);
                    if (SpeedMultiplier != null)
                        clone.TimeSinceLastEvent = (int)Math.Round(x.TimeSinceLastEvent * SpeedMultiplier.Value);

                    if (FixedSpeed != null)
                        clone.TimeSinceLastEvent = FixedSpeed.Value;

                    return clone;
                });
            }

            return actionsResult.ToList();
        }

        private void OnPlayback(object sender, MacroEvent e) =>
            Log($"[A:{_simulateAction++}] Simulating {e.KeyMouseEventType}!");

        public void ImportAction()
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = @"Recorded Action|*.ra";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                var bytes = File.ReadAllBytes(openFileDialog.FileName);
                Log($"Trying to import Action File from {openFileDialog.FileName}...");
                try
                {
                    Parse(bytes);
                    Log($"{bytes} bytes were read!");
                }
                catch (Exception e)
                {
                    Error($"Not is possible to load action file, reason: {e.Message}");
                    _parseThread?.Abort();
                }
            }
        }

        public void Parse(byte[] content)
        {
            _parseThread?.Abort();
            _parseThread = new Thread(() =>
            {
                using (var stream = new MemoryStream(content))
                using (var reader = new BinaryReader(stream))
                    _actionFile = reader.ReadActionFile();

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
            var saveFileDialog1 = new SaveFileDialog
            {
                Filter = @"Recorded Action|*.ra",
                Title = @"Exporting...",
                RestoreDirectory = true
            };

            if (saveFileDialog1.ShowDialog() != DialogResult.OK)
                return;

            _exportThread?.Abort();
            _exportThread = new Thread(() =>
            {
                using (var stream = new MemoryStream())
                using (var bw = new BinaryWriter(stream))
                {
                    bw.Write(_actionFile);
                    using (var fs = saveFileDialog1.OpenFile())
                        fs.Write(stream.GetBuffer(), 0, (int)stream.Position);
                }

                Log("Exported!");
                _exportThread.Abort();
            });
            _exportThread?.Start();
        }

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