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
using System.ComponentModel;

namespace VideoManip {
    public partial class MainWindow : Window {
        public bool isPaused = true;
        public bool isDragging = false;

        private DispatcherTimer timer;
        public float frameRate;
        public TimeSpan startTime;
        public TimeSpan endTime;
        public OpenFileDialog dialog;
        public Process process;

        public MainWindow() {
            InitializeComponent();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(250);
            timer.Tick += Timer_Tick;

            TrimErrorMsg.Visibility = Visibility.Hidden;
            ProgBar.Visibility = Visibility.Hidden;
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e) {
            TrimErrorMsg.Visibility = Visibility.Hidden;
            dialog = new OpenFileDialog();
            if ((bool)dialog.ShowDialog()) {

                try {
                    ShellFile shellFile = ShellFile.FromFilePath(dialog.FileName);
                    frameRate = (float)(shellFile.Properties.System.Video.FrameRate.Value / 1000);
                } catch (Exception ex) {
                    ReportTrimError("Unsupported File Type");
                    return;
                }

                FilePathBox.Text = Directory.GetParent(dialog.FileName).FullName + "\\output.mp4";
                MediaPlayer.Source = new Uri(dialog.FileName);
                PlayContent();
            }
        }

        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e) {
            Scrubber.Minimum = 0;
            Scrubber.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;

            startTime = TimeSpan.FromSeconds(0f);
            endTime = MediaPlayer.NaturalDuration.TimeSpan;
            StartTimeTextBox.Text = TimeStampToDisplayString(startTime);
            EndTimeTextBox.Text = TimeStampToDisplayString(endTime);
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
                if (!timestampBox.IsFocused)
                    timestampBox.Text = TimeStampToDisplayString(MediaPlayer.Position);
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

            if (!timestampBox.IsFocused)
                timestampBox.Text = TimeStampToDisplayString(time);
        } 

        private void PauseContent() {
            PauseButton.Content = "⏵";
            MediaPlayer.Pause();
            timer.Stop();
            if (!timestampBox.IsFocused)
                timestampBox.Text = TimeStampToDisplayString(MediaPlayer.Position); //force timestamp box to be displayed properly
        }

        private void PlayContent() {
            PauseButton.Content = "||";    //"⏸︎";
            MediaPlayer.Play();
            timer.Start();
        }

        private string TimeStampToDisplayString(TimeSpan time) {
            double totalMilliseconds = Math.Round(time.TotalMilliseconds, 3);
            double numMs = totalMilliseconds - Math.Floor(totalMilliseconds / 1000) * 1000;
            return string.Format("{0:00}:{1:00}:{2:00}.{3:000}", time.Hours, time.Minutes, time.Seconds, numMs);
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
            TimeSpan jumpTime = (endTime < MediaPlayer.NaturalDuration.TimeSpan) ? endTime : MediaPlayer.NaturalDuration.TimeSpan;
            UpdateMediaPosition(jumpTime);
        }

        private void JumpToBeginningButton_Click(object sender, RoutedEventArgs e) {
            TimeSpan jumpTime = (startTime > new TimeSpan(0)) ? startTime : new TimeSpan(0);
            UpdateMediaPosition(jumpTime);
        }

        private void JumpToStartButton_Click(object sender, RoutedEventArgs e) {
            UpdateMediaPosition(new TimeSpan(0));
        }

        private void JumpToTrueEndButton_Click(object sender, RoutedEventArgs e) {
            UpdateMediaPosition(MediaPlayer.NaturalDuration.TimeSpan);
        }

        private void UseCurrentStart_Click(object sender, RoutedEventArgs e) {
            startTime = MediaPlayer.Position;
            StartTimeTextBox.Text = TimeStampToDisplayString(startTime);
        }

        private void UseCurrentEnd_Click(object sender, RoutedEventArgs e) {
            endTime = MediaPlayer.Position;
            EndTimeTextBox.Text = TimeStampToDisplayString(endTime);
        }

        private void TrimButton_Click(object sender, RoutedEventArgs e) {
            TrimErrorMsg.Visibility = Visibility.Hidden;

            if (startTime == endTime) {
                ReportTrimError("Start time and end time cannot be the same.");
                return;
            }

            if (startTime > endTime) {
                ReportTrimError("Start time cannot be after end time.");
                return;
            }

            process = new Process();
            string command = $"-i \"{dialog.FileName}\" -ss {startTime} -to {endTime} -c:v libx264 -c:a copy {FilePathBox.Text}";
            process.StartInfo.FileName = "C:/ffmpeg/ffmpeg.exe";
            process.StartInfo.WorkingDirectory = "C:/Users/Umair/Desktop";
            process.StartInfo.Arguments = command;
            process.StartInfo.UseShellExecute = false;//
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;//
            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.CreateNoWindow = false;
            //process.Start();

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(TrimProcess_DoWork);
            worker.ProgressChanged += new ProgressChangedEventHandler(TrimProcess_UpdateProgressBar);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(TrimProcess_OnWorkerComplete);
            ProgBar.Visibility = Visibility.Visible;
            worker.WorkerReportsProgress = true;
            worker.RunWorkerAsync();

            
        }

        private void TrimProcess_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = process.Start();
            BackgroundWorker w = (BackgroundWorker) sender;

            //StreamReader sr = process.StandardError;
            //while (!sr.EndOfStream) {
            //    //string line = sr.ReadLine();
            //    //try {
            //    //    string[] split = line.Split(' ');
            //    //    foreach (var row in split) {
            //    //        if (row.StartsWith("time=")) {
            //    //            var time = row.Split('=');
            //    //            int progress = (int) (TimeSpan.Parse(time[1]).TotalSeconds / MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds);
            //    //            w.ReportProgress(progress);
            //    //        }
            //    //    }
            //    //} catch {

            //    //}
            //}

            //for (int i = 0; i <= 100; i++) {
            //    w.ReportProgress(i);
            //}
            //process.WaitForExit();
            
        }

        private void TrimProcess_UpdateProgressBar(object sender, ProgressChangedEventArgs e) {
            ProgBar.Value = e.ProgressPercentage;
        }

        private void TrimProcess_OnWorkerComplete(object sender, RunWorkerCompletedEventArgs e) {
            MessageBox.Show("Video Successfully Trimmed.");
            ProgBar.Value = 0;
            ProgBar.Visibility = Visibility.Hidden;
        }

        private void ReportTrimError(string error) {
            TrimErrorMsg.Text = error;
            TrimErrorMsg.Visibility = Visibility.Visible;
        }
    }

}
