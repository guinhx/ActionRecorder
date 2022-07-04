using MaterialDesignThemes.Wpf;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ActionRecorder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly Application _application;

        public MainWindow()
        {
            InitializeComponent();
            _application = new Application(this);
            Update();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        public void Update()
        {
            if (_application == null)
                return;

            Dispatcher.Invoke(() =>
            {
                if (_application.IsRecording)
                {
                    _recordIcon.Kind = PackIconKind.Stop;
                    _recordTxt.Text = $"Stop Recording ({Application.RECORD_KEY_HOOK})";
                }
                else
                {
                    _recordIcon.Kind = PackIconKind.Record;
                    _recordTxt.Text = $"Record ({Application.RECORD_KEY_HOOK})";
                }

                if (_application.IsPlaying)
                {
                    _playIcon.Kind = PackIconKind.Stop;
                    _playTxt.Text = $"Stop ({Application.PLAY_KEY_HOOK})";
                }
                else
                {
                    _playIcon.Kind = PackIconKind.PlayCircle;
                    _playTxt.Text = $"Play ({Application.PLAY_KEY_HOOK})";
                }

                _loopCheckBox.IsChecked = _application.Loop;

                _suppressMouseMovePathCheckBox.IsChecked = _application.SuppressMouseMovePath;

                var speedType = ((ComboBoxItem)_speedType.SelectedValue).Content.ToString();

                if (speedType == "Multiplier")
                {

                    _application.SpeedMultiplier = Math.Round(Math.Pow(_speedMultiplier.Value / 50, 2.33), 2); // Non linear slider
                    _application.SpeedMultiplier = _application.SpeedMultiplier < .01 ? .01 : _application.SpeedMultiplier > 5 ? 5 : _application.SpeedMultiplier;
                    _speedMultiplierText.Content = $"Action Elapsed Time x {_application.SpeedMultiplier}";
                    _speedMultiplierContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    _speedMultiplierContainer.Visibility = Visibility.Collapsed;
                    _application.SpeedMultiplier = null;
                }

                if (speedType == "Fixed")
                {
                    _application.FixedSpeed = (int)Math.Round(Math.Pow(_speedFixed.Value, 2.924) / 70); // Non linear slider
                    _application.FixedSpeed = _application.FixedSpeed < 1 ? 1 : _application.FixedSpeed > 10000 ? 10000 : _application.FixedSpeed;
                    _speedFixedText.Content = $"Action Elapsed Time {_application.FixedSpeed}ms";
                    _speedFixedContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    _speedFixedContainer.Visibility = Visibility.Collapsed;
                    _application.FixedSpeed = null;
                }
            });
        }

        public void RecordHook()
        {
            _application.Record();
            Update();
        }

        private void OnClickRecord(object sender, RoutedEventArgs e) =>
            RecordHook();

        public void PlayOrStopHook()
        {
            _application.Playback();
            Update();
        }

        private void OnClickPlayOrStop(object sender, RoutedEventArgs e) =>
            PlayOrStopHook();

        private void OnClickImport(object sender, RoutedEventArgs e) =>
            _application.ImportAction();

        private void OnClickExport(object sender, RoutedEventArgs e) =>
            _application.ExportAction();

        public void ClearLog() =>
            _logger.Clear();

        public async Task LogMessage(string message) =>
            await Task.Run(() => _logger.Dispatcher.Invoke(() =>
            {
                _logger.AppendText(message + Environment.NewLine);
                _logger.ScrollToEnd();
            }, DispatcherPriority.Background));

        private void Close(object sender, MouseButtonEventArgs e) =>
            System.Windows.Application.Current.Shutdown();

        private void OnClickLoop(object sender, RoutedEventArgs e) =>
            _application.Loop = !_application.Loop;

        private void OnClickSuppressMouseMovePath(object sender, RoutedEventArgs e) =>
            _application.SuppressMouseMovePath = !_application.SuppressMouseMovePath;

        private void OnChangeSpeedMultiplier(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            Update();

        private void OnChangeSpeedType(object sender, SelectionChangedEventArgs e) =>
            Update();

        private void OnChangeSpeedFixed(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            Update();
    }
}