using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KioskTimer
{
    // Main form: shows a big full-screen countdown and force-restarts
    // the PC either when time runs out, or if this window is closed
    // early (Alt+F4, taskbar close, etc).
    public class TimerForm : Form
    {
        // ---- CONFIGURATION ----
        // Session length.
        private static readonly TimeSpan SessionLength = TimeSpan.FromHours(2);

        // Threshold to trigger the warning label (5 minutes remaining).
        private static readonly TimeSpan WarningThreshold = TimeSpan.FromMinutes(5);
        
        // How much time is left, counted down every second.
        private TimeSpan _timeRemaining;

        // Windows Forms Timer that fires once per second.
        private readonly System.Windows.Forms.Timer _tickTimer;

        // Big label showing the countdown, e.g. "01:59:58"
        private readonly Label _countdownLabel;

        // Non-blocking warning label displayed at the top when time is low
        private readonly Label _warningLabel;

        // "Close Session" button allowing the user to voluntarily end their session early
        private readonly Button _closeSessionButton;

        // Guard flag so we don't trigger the shutdown command twice.
        private bool _shutdownTriggered = false;

        public TimerForm()
        {
            _timeRemaining = SessionLength;

            // ---- WINDOW SETUP ----
            // Small fixed-size widget, always on top.
            this.Text = "Session Timer";
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow; // thin border, no minimize/maximize buttons
            this.ControlBox = false;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.Size = new Size(220, 130);
            this.ShowInTaskbar = false; // keeps the taskbar clean and prevents closing via taskbar context menu

            // Position in the bottom-right corner of the primary screen,
            // with a small margin so it doesn't touch the very edge.
            var workArea = Screen.PrimaryScreen.WorkingArea;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(
                workArea.Right - this.Width - 16,
                workArea.Bottom - this.Height - 16);

                // ---- WARNING LABEL (Top) ----
            _warningLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Text = string.Empty,
                Visible = false
            };
            this.Controls.Add(_warningLabel);

            // ---- CLOSE SESSION BUTTON (Bottom) ----
            _closeSessionButton = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Text = "Close Session",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            _closeSessionButton.FlatAppearance.BorderSize = 1;
            _closeSessionButton.FlatAppearance.BorderColor = Color.Gray;
            _closeSessionButton.Click += (s, e) => TriggerRestart();
            this.Controls.Add(_closeSessionButton);

            // ---- COUNTDOWN LABEL (Center) ----
            _countdownLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Text = FormatTime(_timeRemaining)
            };
            this.Controls.Add(_countdownLabel);

            // ---- TICK TIMER ----
            // Refresh the label. When remaining time hits zero we force
            // a restart.
            _tickTimer = new System.Windows.Forms.Timer();
            _tickTimer.Interval = 1000; // milliseconds
            _tickTimer.Tick += TickTimer_Tick;
            _tickTimer.Start();

            // ---- CLOSE HOOK ----
            this.FormClosing += TimerForm_FormClosing;
        }

        // Intercepts system keys before they are dispatched.
        // Swallows Alt+F4 so the user cannot manually terminate the form via keyboard.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.F4))
            {
                return true; // Key handled/swallowed
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Runs once per second while the app is open.
        private void TickTimer_Tick(object sender, EventArgs e)
        {
            _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));

            if (_timeRemaining <= TimeSpan.Zero)
            {
                // Time's up. Stop ticking so this doesn't fire again,
                // then force the restart.
                _tickTimer.Stop();
                TriggerRestart();
                return;
            }

            // Show non-blocking warning when time drops to 5 minutes or less
            if (_timeRemaining <= WarningThreshold && !_warningLabel.Visible)
            {
                _warningLabel.Text = "⚠ Save your work now";
                _warningLabel.Visible = true;
            }

            _countdownLabel.Text = FormatTime(_timeRemaining);
        }

        // Fires when the form is closing for ANY reason: the user hit
        // Alt+F4, closed it from the taskbar, or our own code called
        // this.Close() from TriggerRestart() after time ran out.
        private void TimerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            TriggerRestart();
        }

        // Runs the actual Windows restart command and closes this app.
        //   /r   = restart the machine (not a full shutdown)
        //   /f   = force-close all open applications without waiting
        //          on "save your work?" prompts -- this is what
        //          satisfies "all accesses (windows, files, etc) are closed"
        //   /t 0 = restart immediately, with no countdown/delay
        private void TriggerRestart()
        {
            if (_shutdownTriggered)
            {
                // Already triggered once -- skip to avoid launching
                // shutdown.exe twice (see the guard-flag comment above).
                return;
            }
            _shutdownTriggered = true;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/r /f /t 0",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
               
                MessageBox.Show("Failed to restart the machine: " + ex.Message);
            }

            this.Close();
        }

        // Formats a TimeSpan as HH:MM:SS for display, e.g. "01:59:58".
        private static string FormatTime(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
        }

        // Entry point. [STAThread] is required for Windows Forms apps.
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TimerForm());
        }
    }
}
