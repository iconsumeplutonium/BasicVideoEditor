using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using Microsoft.WindowsAPICodePack.Shell;


namespace VideoManip {
    public partial class MainWindow : Window {
        public bool isPaused = true;
        public bool isDragging = false;
        private DispatcherTimer timer;
        public float frameRate;

        public MainWindow() {
            InitializeComponent();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            if ((bool)dialog.ShowDialog()) {
                FilePathBox.Text = dialog.FileName;
                MediaPlayer.Source = new Uri(dialog.FileName);
                MediaPlayer.Play();
                timer.Start();
                isPaused = false;

                ShellFile shellFile = ShellFile.FromFilePath(dialog.FileName);
                frameRate = (float)(shellFile.Properties.System.Video.FrameRate.Value / 1000);
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e) {
            isPaused = !isPaused;

            if (isPaused)
                PauseContent();
            else
                PlayContent();

        }

        private void Timer_Tick(object sender, EventArgs e) {
            if (MediaPlayer.Source != null && MediaPlayer.NaturalDuration.HasTimeSpan) {
                Scrubber.Minimum = 0;
                Scrubber.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                UpdateMediaPosition(MediaPlayer.Position);
            }
        }

        private void Scrubber_DragStarted(object sender, DragStartedEventArgs e) {
            timer.Stop();
            MediaPlayer.Pause();
            isDragging = true;
        }

        private void Scrubber_DragCompleted(object sender, DragCompletedEventArgs e) {
            UpdateMediaPosition(TimeSpan.FromSeconds(Scrubber.Value));
            if (!isPaused) {
                timer.Start();
                MediaPlayer.Play();
            }
            isDragging = false;
        }

        //takes care of video position, scrubber position, and textbox display
        private void UpdateMediaPosition(TimeSpan time) {
            MediaPlayer.Position = time;
            Scrubber.Value = MediaPlayer.Position.TotalSeconds;

            if (!timestampBox.IsFocused) {
                double totalMilliseconds = Math.Round(time.TotalMilliseconds, 3);
                double numMillies = totalMilliseconds - Math.Floor(totalMilliseconds / 1000) * 1000;
                string formattedTimeSpan = string.Format("{0:00}:{1:00}:{2:00}.{3:000}", time.Hours, time.Minutes, time.Seconds, numMillies);
                timestampBox.Text = formattedTimeSpan;
            }
        } 

        private void PauseContent() {
            PauseButton.Content = "⏵";
            MediaPlayer.Pause();
            timer.Stop();
        }

        private void PlayContent() {
            PauseButton.Content = "||";    //"⏸︎";
            MediaPlayer.Play();
            timer.Start();
        }

        private void NextFrameButton_Click(object sender, RoutedEventArgs e) {
            UpdateMediaPosition(TimeSpan.FromSeconds((double)MediaPlayer.Position.TotalSeconds + (1 / frameRate)));
        }

        private void PreviousFrameButton_Click(object sender, RoutedEventArgs e) {
            UpdateMediaPosition(TimeSpan.FromSeconds((double)MediaPlayer.Position.TotalSeconds - (1 / frameRate)));
        }

        private void timestampBox_TextChanged(object sender, TextChangedEventArgs e) {
            TimeSpan t;
            if (TimeSpan.TryParse(timestampBox.Text, out t))
                UpdateMediaPosition(t);
        }
        private void timestampBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                // Move focus to next control
                TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
                ((TextBox)sender).MoveFocus(request);
                //PlayContent();

            }
        }

        private void JumpToEndButton_Click(object sender, RoutedEventArgs e) {
            UpdateMediaPosition(MediaPlayer.NaturalDuration.TimeSpan);
        }

        private void JumpToBeginningButton_Click(object sender, RoutedEventArgs e) {
            UpdateMediaPosition(new TimeSpan(0));
        }

    }

}
