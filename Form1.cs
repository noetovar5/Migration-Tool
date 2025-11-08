using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace SSFCU.RoboCopy.Migration
{
    public partial class Form1 : Form
    {
        private readonly Label titleLabel;
        private readonly Label srcLabel;
        private readonly Label dstLabel;
        private readonly TextBox srcText;
        private readonly TextBox dstText;
        private readonly Button submitBtn;
        private readonly Button cancelBtn;
        private readonly Button browseSrc;
        private readonly Button browseDst;
        private readonly ProgressBar progress;
        private readonly StatusStrip statusStrip;
        private readonly ToolStripStatusLabel statusLabel;
        private readonly Label footer;

        private readonly StringBuilder robocopyOutput = new StringBuilder();
        private volatile bool isCopying = false;
        private Process? copyProcess = null;

        public Form1()
        {
            // --- Form setup ---
            this.Text = "SSFCU Robo Copy Migration Tool";
            this.ClientSize = new Size(740, 400);
            this.BackColor = Color.FromArgb(10, 34, 64);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // ✅ Load embedded icon from resources (no external file needed)
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using (Stream? iconStream = asm.GetManifestResourceStream("SSFCU.RoboCopy.Migration.ssfcu-logo.ico"))
            {
                if (iconStream != null)
                    this.Icon = new Icon(iconStream);
            }

            // --- Title ---
            titleLabel = new Label
            {
                Text = "SSFCU Robo Copy Migration Tool",
                ForeColor = Color.White,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 48,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };

            // --- Footer ---
            footer = new Label
            {
                Text = "Design by Noe Tovar-MBA 2025",
                ForeColor = Color.White,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom,
                Height = 28,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };

            // --- Status bar ---
            statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                SizingGrip = false,
                BackColor = Color.FromArgb(18, 50, 90)
            };
            statusLabel = new ToolStripStatusLabel("Ready") { ForeColor = Color.White };
            statusStrip.Items.Add(statusLabel);

            // --- Central layout ---
            var centerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 5,
                Padding = new Padding(24),
            };
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // --- Source and Destination fields ---
            srcLabel = new Label { Text = "Source folder:", ForeColor = Color.White, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 12, 8) };
            dstLabel = new Label { Text = "Destination folder:", ForeColor = Color.White, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 12, 8) };
            srcText = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 480, Margin = new Padding(0, 8, 12, 8) };
            dstText = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 480, Margin = new Padding(0, 8, 12, 8) };

            // --- Browse buttons ---
            browseSrc = CreateButton("Browse…", (s, e) =>
            {
                var p = PickFolder();
                if (!string.IsNullOrWhiteSpace(p)) srcText.Text = p;
            });

            browseDst = CreateButton("Browse…", (s, e) =>
            {
                var p = PickFolder();
                if (!string.IsNullOrWhiteSpace(p)) dstText.Text = p;
            });

            // --- Start Copy button ---
            submitBtn = CreateButton("Start Copy", async (s, e) => await StartCopyAsync());
            submitBtn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            submitBtn.Width = 120;

            // --- Cancel button (bold bright white) ---
            cancelBtn = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Margin = new Padding(12, 8, 12, 8),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Width = 120,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(180, 0, 0),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            cancelBtn.FlatAppearance.BorderColor = Color.White;
            cancelBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 40, 40);
            cancelBtn.Click += (s, e) => CancelCopy();

            // --- Buttons layout ---
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Anchor = AnchorStyles.Top,
                Padding = new Padding(0, 10, 0, 0),
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            buttonPanel.Controls.Add(submitBtn);
            buttonPanel.Controls.Add(cancelBtn);

            // --- Progress bar ---
            progress = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Dock = DockStyle.Bottom,
                Height = 18
            };

            // --- Layout assembly ---
            centerPanel.Controls.Add(srcLabel, 0, 0);
            centerPanel.Controls.Add(srcText, 1, 0);
            centerPanel.Controls.Add(browseSrc, 2, 0);

            centerPanel.Controls.Add(dstLabel, 0, 1);
            centerPanel.Controls.Add(dstText, 1, 1);
            centerPanel.Controls.Add(browseDst, 2, 1);

            centerPanel.Controls.Add(buttonPanel, 0, 2);
            centerPanel.SetColumnSpan(buttonPanel, 3);
            buttonPanel.Anchor = AnchorStyles.Top;
            buttonPanel.Margin = new Padding(0, 20, 0, 0);

            Controls.Add(centerPanel);
            Controls.Add(progress);
            Controls.Add(statusStrip);
            Controls.Add(footer);
            Controls.Add(titleLabel);
        }

        private Button CreateButton(string text, EventHandler clickEvent)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(12, 8, 12, 8),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(28, 80, 130),
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderColor = Color.White;
            btn.Click += clickEvent;
            return btn;
        }

        private string PickFolder()
        {
            using var dlg = new FolderBrowserDialog();
            return dlg.ShowDialog() == DialogResult.OK ? dlg.SelectedPath : string.Empty;
        }

        private async Task StartCopyAsync()
        {
            var src = srcText.Text.Trim();
            var dst = dstText.Text.Trim();

            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst))
            {
                MessageBox.Show("Please provide both Source and Destination folders.", "Missing information",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(src))
            {
                MessageBox.Show("Source folder does not exist.", "Invalid source",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try { Directory.CreateDirectory(dst); } catch { }

            ToggleUi(false);
            cancelBtn.Enabled = true;
            statusLabel.Text = "Starting robocopy…";
            progress.Value = 0;
            robocopyOutput.Clear();
            isCopying = true;

            try
            {
                var logFile = await RunRoboCopyAsync(src, dst);
                statusLabel.Text = "Copy completed successfully.";

                MessageBox.Show($"Process completed.\n\nLog saved at:\n{logFile}",
                    "Robocopy Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);

                try { Process.Start("notepad.exe", logFile); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open log automatically:\n{ex.Message}",
                        "Open Log Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                statusLabel.Text = "Copy cancelled by user.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Copy failed.";
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isCopying = false;
                cancelBtn.Enabled = false;
                ToggleUi(true);
            }
        }

        private void CancelCopy()
        {
            if (isCopying && copyProcess != null && !copyProcess.HasExited)
            {
                try
                {
                    copyProcess.Kill(true);
                    statusLabel.Text = "Cancelling copy…";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to cancel process:\n{ex.Message}", "Cancel Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ToggleUi(bool enabled)
        {
            srcText.Enabled = enabled;
            dstText.Enabled = enabled;
            submitBtn.Enabled = enabled;
            browseSrc.Enabled = enabled;
            browseDst.Enabled = enabled;
        }

        private async Task<string> RunRoboCopyAsync(string src, string dst)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var finalLogPath = Path.Combine(dst, $"SSFCU_Migration_Log_{stamp}.txt");
            var args = $"{Quote(src)} {Quote(dst)} /E /R:1 /W:1 /ETA /MT:16";

            var psi = new ProcessStartInfo
            {
                FileName = "robocopy",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            copyProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var percentRegex = new Regex(@"\b(\d{1,3})%\b", RegexOptions.Compiled);
            var etaRegex = new Regex(@"ETA\s+(\d{1,2}:\d{2}:\d{2})", RegexOptions.Compiled);

            copyProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                robocopyOutput.AppendLine(e.Data);

                var m = percentRegex.Match(e.Data);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var pct))
                {
                    SafeUi(() =>
                    {
                        progress.Value = Math.Min(100, Math.Max(0, pct));
                        statusLabel.Text = $"Copying… {pct}%";
                    });
                }

                var eta = etaRegex.Match(e.Data);
                if (eta.Success)
                {
                    SafeUi(() => statusLabel.Text += $"  ETA {eta.Groups[1].Value}");
                }
            };

            copyProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                robocopyOutput.AppendLine(e.Data);
            };

            copyProcess.Start();
            copyProcess.BeginOutputReadLine();
            copyProcess.BeginErrorReadLine();

            await Task.Run(() => copyProcess.WaitForExit());

            if (copyProcess.ExitCode >= 8)
                throw new Exception("Robocopy finished with errors.");

            var header = "SSFCU migration tool";
            var footerText = "Application design by Noe Tovar-MBA 2025 For more information noetovar.com";
            var now = DateTime.Now;

            var final = new StringBuilder();
            final.AppendLine(header);
            final.AppendLine(new string('=', header.Length));
            final.AppendLine($"Date/Time: {now:yyyy-MM-dd HH:mm:ss}");
            final.AppendLine($"Source: {src}");
            final.AppendLine($"Destination: {dst}");
            final.AppendLine($"Exit Code: {copyProcess.ExitCode}");
            final.AppendLine();
            final.AppendLine("Robocopy Output:");
            final.AppendLine(new string('-', 40));
            final.Append(robocopyOutput.ToString());
            final.AppendLine(new string('-', 40));
            final.AppendLine(footerText);

            File.WriteAllText(finalLogPath, final.ToString(), Encoding.UTF8);

            SafeUi(() =>
            {
                progress.Value = 100;
                statusLabel.Text = "Copy completed. 100%";
            });

            return finalLogPath;
        }

        private static string Quote(string path)
            => path.Contains(' ') ? $"\"{path}\"" : path;

        private void SafeUi(Action a)
        {
            if (InvokeRequired) BeginInvoke(a);
            else a();
        }
    }
}
