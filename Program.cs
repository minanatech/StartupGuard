using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StartupGuard
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class StartupEntry
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Location { get; set; }
        public string Hive { get; set; }
        public string ValueName { get; set; }
        public bool Enabled { get; set; }
        public bool NeedsAdmin { get { return Hive == "HKLM"; } }
    }

    internal sealed class MainForm : Form
    {
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string BackupPath = @"Software\MinanaTech\StartupGuard\DisabledStartupItems";

        private readonly DataGridView grid = new DataGridView();
        private readonly Button refreshButton = new Button();
        private readonly Button disableButton = new Button();
        private readonly Button enableButton = new Button();
        private readonly Button openLocationButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly List<StartupEntry> entries = new List<StartupEntry>();

        public MainForm()
        {
            Text = "Startup Guard";
            Width = 980;
            Height = 620;
            MinimumSize = new Size(780, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            var header = new Label
            {
                Text = "Startup Guard",
                Font = new Font("Segoe UI Semibold", 18F),
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(12, 8, 0, 0)
            };

            var description = new Label
            {
                Text = "Review startup apps and disable only the entries you choose. Disabled entries are kept so they can be restored later.",
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(14, 0, 12, 0)
            };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(10, 6, 10, 4),
                FlowDirection = FlowDirection.LeftToRight
            };

            ConfigureButton(refreshButton, "Refresh");
            ConfigureButton(disableButton, "Disable");
            ConfigureButton(enableButton, "Enable");
            ConfigureButton(openLocationButton, "Open File Location");

            refreshButton.Click += delegate { LoadEntries(); };
            disableButton.Click += delegate { DisableSelected(); };
            enableButton.Click += delegate { EnableSelected(); };
            openLocationButton.Click += delegate { OpenSelectedLocation(); };

            toolbar.Controls.Add(refreshButton);
            toolbar.Controls.Add(disableButton);
            toolbar.Controls.Add(enableButton);
            toolbar.Controls.Add(openLocationButton);

            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoGenerateColumns = false;
            grid.RowHeadersVisible = false;
            grid.BackgroundColor = SystemColors.Window;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.CellDoubleClick += delegate { OpenSelectedLocation(); };
            grid.SelectionChanged += delegate { UpdateButtonState(); };

            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 190 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Location", DataPropertyName = "Location", Width = 140 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Command", DataPropertyName = "Command", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.Height = 28;
            statusLabel.Padding = new Padding(12, 5, 0, 0);

            Controls.Add(grid);
            Controls.Add(statusLabel);
            Controls.Add(toolbar);
            Controls.Add(description);
            Controls.Add(header);

            LoadEntries();
        }

        private static void ConfigureButton(Button button, string text)
        {
            button.Text = text;
            button.AutoSize = true;
            button.Height = 28;
            button.Margin = new Padding(4, 0, 4, 0);
        }

        private void LoadEntries()
        {
            entries.Clear();
            ReadRunEntries(Registry.CurrentUser, "HKCU", RunPath, true);
            ReadRunEntries(Registry.LocalMachine, "HKLM", RunPath, true);
            ReadDisabledEntries(Registry.CurrentUser, "HKCU");
            ReadDisabledEntries(Registry.LocalMachine, "HKLM");
            BindGrid();
        }

        private void ReadRunEntries(RegistryKey root, string hive, string path, bool enabled)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(path, false))
                {
                    if (key == null) return;
                    foreach (string name in key.GetValueNames())
                    {
                        object raw = key.GetValue(name);
                        if (raw == null) continue;
                        entries.Add(new StartupEntry
                        {
                            Name = name,
                            Command = raw.ToString(),
                            Hive = hive,
                            ValueName = name,
                            Location = hive + @"\...\Run",
                            Enabled = enabled
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Could not read " + hive + " startup entries.", ex);
            }
        }

        private void ReadDisabledEntries(RegistryKey root, string hive)
        {
            try
            {
                using (RegistryKey backup = root.OpenSubKey(BackupPath, false))
                {
                    if (backup == null) return;
                    foreach (string name in backup.GetValueNames())
                    {
                        object raw = backup.GetValue(name);
                        if (raw == null) continue;
                        entries.Add(new StartupEntry
                        {
                            Name = name,
                            Command = raw.ToString(),
                            Hive = hive,
                            ValueName = name,
                            Location = hive + @"\...\StartupGuard backup",
                            Enabled = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Could not read " + hive + " disabled startup entries.", ex);
            }
        }

        private void BindGrid()
        {
            var rows = new List<object>();
            foreach (StartupEntry entry in entries)
            {
                rows.Add(new
                {
                    Status = entry.Enabled ? "Enabled" : "Disabled",
                    entry.Name,
                    entry.Location,
                    entry.Command,
                    Tag = entry
                });
            }

            grid.DataSource = rows;
            for (int i = 0; i < rows.Count; i++)
            {
                grid.Rows[i].Tag = entries[i];
                grid.Rows[i].DefaultCellStyle.ForeColor = entries[i].Enabled ? SystemColors.ControlText : Color.FromArgb(110, 110, 110);
            }

            statusLabel.Text = entries.Count + " startup item(s) found" + (IsAdministrator() ? "" : " - run as administrator to manage all users entries");
            UpdateButtonState();
        }

        private StartupEntry SelectedEntry()
        {
            if (grid.SelectedRows.Count == 0) return null;
            return grid.SelectedRows[0].Tag as StartupEntry;
        }

        private void UpdateButtonState()
        {
            StartupEntry entry = SelectedEntry();
            disableButton.Enabled = entry != null && entry.Enabled;
            enableButton.Enabled = entry != null && !entry.Enabled;
            openLocationButton.Enabled = entry != null;
        }

        private void DisableSelected()
        {
            StartupEntry entry = SelectedEntry();
            if (entry == null || !entry.Enabled) return;
            if (!ConfirmAdminRequirement(entry)) return;

            try
            {
                RegistryKey root = RootFor(entry.Hive);
                using (RegistryKey run = root.OpenSubKey(RunPath, true))
                using (RegistryKey backup = root.CreateSubKey(BackupPath))
                {
                    if (run == null || backup == null) return;
                    object value = run.GetValue(entry.ValueName);
                    if (value == null)
                    {
                        LoadEntries();
                        return;
                    }
                    backup.SetValue(entry.ValueName, value.ToString(), RegistryValueKind.String);
                    run.DeleteValue(entry.ValueName, false);
                }
                LoadEntries();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Administrator permission is required for this startup entry.", "Startup Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                ShowError("Could not disable this startup entry.", ex);
            }
        }

        private void EnableSelected()
        {
            StartupEntry entry = SelectedEntry();
            if (entry == null || entry.Enabled) return;
            if (!ConfirmAdminRequirement(entry)) return;

            try
            {
                RegistryKey root = RootFor(entry.Hive);
                using (RegistryKey run = root.CreateSubKey(RunPath))
                using (RegistryKey backup = root.OpenSubKey(BackupPath, true))
                {
                    if (run == null || backup == null) return;
                    object value = backup.GetValue(entry.ValueName);
                    if (value == null)
                    {
                        LoadEntries();
                        return;
                    }
                    run.SetValue(entry.ValueName, value.ToString(), RegistryValueKind.String);
                    backup.DeleteValue(entry.ValueName, false);
                }
                LoadEntries();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Administrator permission is required for this startup entry.", "Startup Guard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                ShowError("Could not enable this startup entry.", ex);
            }
        }

        private bool ConfirmAdminRequirement(StartupEntry entry)
        {
            if (!entry.NeedsAdmin || IsAdministrator()) return true;
            MessageBox.Show("This is an all users startup entry. Please restart Startup Guard as administrator to change it.", "Startup Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private static RegistryKey RootFor(string hive)
        {
            return hive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void OpenSelectedLocation()
        {
            StartupEntry entry = SelectedEntry();
            if (entry == null) return;

            string path = TryExtractExecutablePath(entry.Command);
            if (path == null || !File.Exists(path))
            {
                MessageBox.Show("Startup Guard could not find the file path for this command.", "Startup Guard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        private static string TryExtractExecutablePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            command = Environment.ExpandEnvironmentVariables(command.Trim());

            if (command.StartsWith("\""))
            {
                int end = command.IndexOf('"', 1);
                if (end > 1) return command.Substring(1, end - 1);
            }

            int exeIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0) return command.Substring(0, exeIndex + 4).Trim();

            string first = command.Split(new[] { ' ' }, 2)[0];
            return first.Length > 0 ? first : null;
        }

        private static void ShowError(string message, Exception ex)
        {
            MessageBox.Show(message + Environment.NewLine + Environment.NewLine + ex.Message, "Startup Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
