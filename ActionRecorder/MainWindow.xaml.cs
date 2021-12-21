using System;
using System.Collections;
using System.Linq;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace ActionRecorder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private Application _application;
        public MainWindow()
        {
            InitializeComponent();
            _application = new Application(this);
        }
        
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        public void Update()
        {
            Dispatcher.Invoke(() =>
            {
                if (_application.IsRecording)
                {
                    _recordIcon.Kind = PackIconKind.Stop;
                    _recordTxt.Text = "Stop Recording";
                }
                else
                {
                    _recordIcon.Kind = PackIconKind.Record;
                    _recordTxt.Text = "Record";   
                }
            
                if (_application.IsPlaying)
                {
                    _playIcon.Kind = PackIconKind.Stop;
                    _playTxt.Text = "Stop";
                }
                else
                {
                    _playIcon.Kind = PackIconKind.PlayCircle;
                    _playTxt.Text = "Play";
                }
            });
        }

        public void RecordHook()
        {
            _application.Record();
            Update();
            if (_application.IsRecording)
            {
                _application.Info("You are now recording...");
            }
        }
        
        private void Record(object sender, MouseButtonEventArgs e)
        {
            RecordHook();
        }

        public void PlayOrStopHook()
        {
            _application.Playback();
            Update();
            if (_application.IsPlaying)
            {
                _application.Info("You are now playing...");
            }
        }
        
        private void PlayOrStop(object sender, MouseButtonEventArgs e)
        {
            PlayOrStopHook();
        }

        private void ImportAction(object sender, MouseButtonEventArgs e)
        {
            _application.ImportAction();
        }

        private void ExportAction(object sender, MouseButtonEventArgs e)
        {
            _application.ExportAction();
        }
        
        public void ClearLog() => _logger.Clear();
        
        public void LogMessage(string message)
        {
            _logger.Dispatcher.Invoke(() =>
            {
                var lines = _logger.Text.Split($"{System.Environment.NewLine}".ToArray());
                if (lines.Length > 0 && _logger.Text.Length > 0)
                {
                    _logger.AppendText(System.Environment.NewLine);
                }
                _logger.AppendText(message);
            });
        }
        
        private void Close(object sender, MouseButtonEventArgs e)
        {
            Application.Instance.Running = false;
            System.Windows.Application.Current.Shutdown();
        }

        private void OnCommand(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (!_command.Text.StartsWith("/")) return;
                var args = new Queue(_command.Text.Split(' ').ToList());
                var command = args.Dequeue().ToString().Replace("/", "");
                switch (command.ToLower())
                {
                    case "loop":
                        if (args.Count > 0)
                        {
                            try
                            {
                                _application.RecordWithLoop = bool.Parse(args.Peek().ToString());
                            }
                            catch (Exception ignored)
                            {
                                // ignored
                            }
                        }
                        else
                        {
                            _application.RecordWithLoop = !_application.RecordWithLoop;
                        }

                        if (_application.RecordWithLoop)
                        {
                            _application.Log("When you record an next action list, the same can be reproduced in loop.");
                        }
                        else
                        {
                            _application.Log("When you record an next action list, the same cannot be reproduced in loop.");
                        }
                        break;
                    case "clear":
                        ClearLog();
                        break;
                }
                _command.Text = "";
            }
        }

        private void OnCommandClick(object sender, MouseButtonEventArgs e)
        {
            if (_command.Text.ToLower().StartsWith("/command"))
            {
                _command.Text = "";
            }
        }
    }
}