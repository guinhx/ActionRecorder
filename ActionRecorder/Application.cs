using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ActionRecorder.scheduler;
using ActionRecorder.structure;
using Gma.System.MouseKeyHook;
using Loamen.KeyMouseHook;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        private ActionFile _actionFile = null;

        private readonly KeyMouseFactory _eventHookFactory = new KeyMouseFactory(Hook.GlobalEvents());
        private readonly KeyboardWatcher _keyboardWatcher;
        private readonly KeyboardWatcher _shortcutWatcher;
        private readonly MouseWatcher _mouseWatcher;


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
            var actionCount = _actionFile.Actions.Count;
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
            _actionFile.Actions.Add(e);
            var timeSinceLastEvent = lastAction == null ? "0" : lastAction.TimeSinceLastEvent.ToString();
            Log($"[A:{actionCount}] [LT:{timeSinceLastEvent}] {e.KeyMouseEventType.ToString()} recorded.");
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
                                sim.PlayBack(_actionFile.Actions);
                            } while (isPlaying);
                        }
                        else
                        {
                            sim.PlayBack(_actionFile.Actions);
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
            Log($"Simulating {e.KeyMouseEventType.ToString()}!");
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
                    string fileContent = String.Empty;
                    var fileStream = openFileDialog.OpenFile();

                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        fileContent = reader.ReadToEnd();
                        Log($"Trying to import Action File from {filePath}...");
                        try
                        {
                            var content = Encoding.Default.GetString(Convert.FromBase64String(fileContent));
                            //_actionFile = JsonConvert.DeserializeObject<ActionFile>(content);
                            JObject jsonObject = JObject.Parse(content);
                            _actionFile = Parse(jsonObject);
                            Log("Action File Loaded and able to play!");
                        }
                        catch (Exception e)
                        {
                            Error($"Not is possible to load action file, reason: {e.Message}");
                        }
                    }
                }
            }
        }

        public ActionFile Parse(JObject jsonObj)
        {
            ActionFile actionFile = new ActionFile();

            JArray jActions = (JArray)jsonObj["Actions"];

            actionFile.Name = (string)jsonObj["Name"];
            actionFile.RecordedDate = (DateTime)jsonObj["RecordedDate"];
            actionFile.Actions = jActions.Select(token =>
            {
                MacroEventType macroEventType = JsonConvert.DeserializeObject<MacroEventType>(token["KeyMouseEventType"].ToString());

                EventArgs eventArgs = null;
                switch (macroEventType)
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
                        eventArgs = JsonConvert.DeserializeObject<MouseEventArgs>(token["EventArgs"].ToString());
                        break;
                    case MacroEventType.KeyUp:
                    case MacroEventType.KeyDown:
                        eventArgs = JsonConvert.DeserializeObject<KeyEventArgs>(token["EventArgs"].ToString());
                        break;
                    case MacroEventType.KeyPress:
                        eventArgs = JsonConvert.DeserializeObject<KeyPressEventArgs>(token["EventArgs"].ToString());
                        break;
                }
                int timeSinceLastEvent = (int)token["TimeSinceLastEvent"];
                return new MacroEvent(macroEventType, eventArgs, timeSinceLastEvent);
            }).ToList();
            actionFile.Loop = (bool)jsonObj["Loop"];

            return actionFile;
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
            Stream myStream;
            if ((myStream = saveFileDialog1.OpenFile()) != null)
            {
                var content = JsonConvert.SerializeObject(_actionFile);
                content = Convert.ToBase64String(Encoding.Default.GetBytes(content), 0, content.Length);
                myStream.Write(Encoding.Default.GetBytes(content), 0, content.Length);
                myStream.Close();
            }
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