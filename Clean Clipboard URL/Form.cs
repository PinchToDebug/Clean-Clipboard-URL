using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Clean_Clipboard_URL
{
    public partial class Form : System.Windows.Forms.Form
    {
        const string mutexId = "{3801f550-1a5e-6597-a6e5-fb960c5d9516}";
        Mutex _mutex;
        IntPtr _nextClipboardViewer;
        int cleanedCount = 0;

        List<string> patterns = new List<string>
        {
            "si",                "aku",             "ak",          "rh",
            "skuId",             "kampan",          "utm_",        "tag",
            "aam_uuid",          "refRID",          "refID",       "ref",
            "ad_id",             @"\.*clid",        "cid",         "at",
            @"\.*_referrer",     @"\.*clkid",       @"\.*_ref",    "source",
            "campaign",          "content",         "term",        "medium",
            "is_from_webapp",    "sender_device",   "web_id",      "crid",
            "dib",               @"\.*_rd_",        "dib_tag",
            "campaign_id",       "crid",
            "[?&$%/]s=[^?&$%/]+[^?&$%/]" // For X
        };
        private void writeCount(int count)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Clean Clipboard URL"))
                {
                    key.SetValue("Cleaned URL Count", cleanedCount);
                }
            }
            catch { }
        }
        private async Task<string> TrackerRemover(string text)
        {
            string[] parts = text.Split(' ', '\n');
            bool cleanedUrl = false;
            for (int i = 0; i < parts.Length; i++)
            {
                string x = parts[i];
                if (!Regex.IsMatch(parts[i], $@"((http\:\/\/|https\:\/\/)|(www(?:s)?\d*\.)).*\."))
                {
                    continue;
                }
                foreach (var pattern in patterns)
                {
                    if (Regex.IsMatch(parts[i], pattern))
                    {
                        parts[i] = Regex.Replace(parts[i], $@"((\?|\&|\$|\=|\%){pattern}(\?|\&|\$|\=|\%)).*", "");
                        cleanedUrl = true;

                    }
                }
                foreach (var pattern in patterns) // Checking for trailing trackers
                {
                    if (Regex.IsMatch(parts[i], pattern))
                    {
                        parts[i] = Regex.Replace(parts[i], $@"\/{pattern}.*", "");
                        cleanedUrl = true;
                    }
                }
                if (cleanedUrl)
                {
                    cleanedCount++;
                    writeCount(cleanedCount);
                }
            }

            return await Task.FromResult(string.Join(' ', parts));
        }
        private ContextMenuStrip contextMenuStrip;
        private ToolStripMenuItem statisticsMenuItem;
        private ToolStripMenuItem autostartMenuItem;
        public Form()
        {
            bool optionToRemove = false;
            _mutex = new Mutex(true, mutexId, out bool createdNew);
            if (!createdNew)
            {
                optionToRemove = true;
            }

            Top = -1000;
            Left = -1000;
            InitializeComponent();

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Clean Clipboard URL")!)
            {
                if (key != null && key.GetValue("Cleaned URL Count") != null)
                {
                    cleanedCount = (int)key.GetValue("Cleaned URL Count")!;
                }
            }
            string name = "Clean Clipboard URL";
            string path = Application.ExecutablePath;

            RegistryKey? rkey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            string value = rkey?.GetValue(name)?.ToString() ?? "";
            Debug.WriteLine(path);

            contextMenuStrip = new ContextMenuStrip();

            ToolStripMenuItem exitMenuitem = new ToolStripMenuItem("Exit");
            exitMenuitem.Click += (sender, e) => this.Close();

            autostartMenuItem = new ToolStripMenuItem("Auto start");
            autostartMenuItem.CheckOnClick = true;
            autostartMenuItem.Checked = !string.IsNullOrEmpty(value);
            autostartMenuItem.CheckedChanged += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(rkey?.GetValue(name)?.ToString())) rkey.DeleteValue(name);
                else rkey.SetValue(name, path);
            };

            string githubUrl = "https://github.com/PinchToDebug/Clean-Clipboard-URL";

            ToolStripMenuItem versionMenuItem = new ToolStripMenuItem("v" + Process.GetCurrentProcess().MainModule?.FileVersionInfo?.FileVersion);
            versionMenuItem.Click += (sender, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = githubUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open the link: " + ex.Message);
                }
            };

            versionMenuItem.MouseEnter += (sender, e) =>
            {
                versionMenuItem.Font = new Font(autostartMenuItem.Font, FontStyle.Underline);
                versionMenuItem.ForeColor = Color.Blue;
            };

            versionMenuItem.MouseLeave += (sender, e) =>
            {
                versionMenuItem.Font = autostartMenuItem.Font;
                versionMenuItem.ForeColor = SystemColors.ControlText;
            };

            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.ContextMenuStrip.Opening += (sender, e) =>
            {
                contextMenuStrip.Items.Clear();

                statisticsMenuItem = new ToolStripMenuItem($"{cleanedCount} links cleaned");
                contextMenuStrip.Items.Add(statisticsMenuItem);
                statisticsMenuItem.Enabled = false;
                contextMenuStrip.Items.Add(new ToolStripSeparator());
                contextMenuStrip.Items.Add(versionMenuItem);
                contextMenuStrip.Items.Add(autostartMenuItem);
                contextMenuStrip.Items.Add(exitMenuitem);
            };

            if (rkey != null && optionToRemove && value != "")
            {
                DialogResult result = MessageBox.Show("Application is already running.\nWould you like remove the application from startup?", "Clean Clipboard URL", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    rkey.DeleteValue(name);
                    MessageBox.Show("Application removed from startup successfully.", "Clean Clipboard URL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                DialogResult result2 = MessageBox.Show("Would you like to close all of the instances?", "Clean Clipboard URL", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result2 == DialogResult.Yes)
                {
                    foreach (var process in Process.GetProcessesByName("Clean Clipboard URL"))
                    {
                        process.Kill();
                    }
                }
                Close();
                return;
            }
            else if (optionToRemove)
            {
                DialogResult result = MessageBox.Show("Application is already running.\nWould you like to close all of the instances?", "Clean Clipboard URL", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    foreach (var process in Process.GetProcessesByName("Clean Clipboard URL"))
                    {
                        process.Kill();
                    }
                }
                Close();
                return;
            }
            _nextClipboardViewer = SetClipboardViewer(this.Handle);
            this.Hide();
            this.Visible = false;
            Hide();
        }
        private void Clipstuff_Load(object sender, EventArgs e)
        {
            this.Hide();
            this.Visible = false;
            Hide();
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0308: // WM_DRAWCLIPBOARD
                    CheckForTrackersInClipboardText();
                    SendMessage(_nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    break;

                case 0x030D: // WM_CHANGECBCHAIN
                    if (m.WParam == _nextClipboardViewer)
                    {
                        _nextClipboardViewer = m.LParam;
                    }
                    else
                    {
                        SendMessage(_nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    }
                    break;
            }
            base.WndProc(ref m);
        }

        private async void CheckForTrackersInClipboardText()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    Clipboard.SetText(await TrackerRemover(Clipboard.GetText()));
                }
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ChangeClipboardChain(this.Handle, _nextClipboardViewer);
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnFormClosed(e);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private void Clipstuff_Activated(object sender, EventArgs e)
        {
            this.Hide();
        }
    }
}
