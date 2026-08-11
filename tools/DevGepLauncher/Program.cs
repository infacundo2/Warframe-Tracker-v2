using System.Diagnostics;

namespace WarframeTracker.DevGepLauncher;

internal static class Program
{
    private const string PackagesUrl =
        "https://electronapi-qa.overwolf.com/v2/packages";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LauncherForm());
    }

    private sealed class LauncherForm : Form
    {
        private readonly TextBox _key = new()
        {
            UseSystemPasswordChar = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 10),
            MinimumSize = new Size(0, 32)
        };

        public LauncherForm()
        {
            Text = "Warframe Tracker — GEP temporal";
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(580, 320);
            MinimumSize = new Size(520, 350);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(5, 11, 18);
            ForeColor = Color.FromArgb(234, 249, 255);
            Font = new Font("Segoe UI", 10F);

            var title = new Label
            {
                Text = "PRUEBA LOCAL DE OVERWOLF GEP",
                AutoSize = true,
                ForeColor = Color.FromArgb(119, 231, 255),
                Font = new Font(Font, FontStyle.Bold)
            };
            var explanation = new Label
            {
                Text = "Pega tu OW_DEV_KEY temporal. La clave no se escribe en archivos, " +
                       "no se registra y solo existe en el proceso del Tracker mientras está abierto.",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 8)
            };
            var keyLabel = new Label
            {
                Text = "OW_DEV_KEY",
                AutoSize = true,
                ForeColor = Color.FromArgb(216, 184, 106),
                Margin = new Padding(0, 8, 0, 3)
            };
            var launch = new Button
            {
                Text = "INICIAR TRACKER CON GEP",
                Dock = DockStyle.Fill,
                Height = 44,
                BackColor = Color.FromArgb(16, 69, 87),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 12, 0, 0),
                Padding = new Padding(12, 8, 12, 8)
            };
            launch.Click += Launch;
            AcceptButton = launch;

            var safetyNote = new Label
            {
                Text = "Cierra cualquier instancia anterior del Tracker antes de iniciar.",
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(216, 184, 106),
                Margin = new Padding(0, 12, 0, 0)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(24),
                AutoScroll = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var index = 0; index < layout.RowCount; index++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(explanation, 0, 1);
            layout.Controls.Add(keyLabel, 0, 2);
            layout.Controls.Add(_key, 0, 3);
            layout.Controls.Add(launch, 0, 4);
            layout.Controls.Add(safetyNote, 0, 5);
            Controls.Add(layout);
        }

        private void Launch(object? sender, EventArgs args)
        {
            var key = _key.Text.Trim();
            if (!Guid.TryParse(key, out _))
            {
                MessageBox.Show(this, "La OW_DEV_KEY no tiene un formato válido.",
                    "Clave inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _key.Focus();
                return;
            }

            var runningTracker = Process.GetProcesses().Any(process =>
            {
                try
                {
                    if (process.ProcessName.Equals(
                            "Warframe Tracker", StringComparison.OrdinalIgnoreCase))
                        return true;
                    return process.ProcessName.Equals("electron", StringComparison.OrdinalIgnoreCase)
                           && (process.MainModule?.FileName?.Contains(
                               "@overwolf\\ow-electron", StringComparison.OrdinalIgnoreCase) ?? false);
                }
                catch
                {
                    return false;
                }
            });
            if (runningTracker)
            {
                MessageBox.Show(this,
                    "Warframe Tracker ya está abierto. Ciérralo completamente y vuelve a pulsar iniciar.\n\n" +
                    "La clave solamente puede entrar en la primera instancia del Tracker.",
                    "Tracker ya iniciado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var runtime = LocateRuntime();
            if (runtime is null)
            {
                MessageBox.Show(this,
                    "No se encontró el runtime local de ow-electron. No muevas este ejecutable fuera de la carpeta QA portátil ni de Warframe-Tracker-v2.",
                    "Runtime de desarrollo no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = runtime.Value.Executable,
                    Arguments = $"\"{runtime.Value.DesktopDirectory}\" --owepm-packages-url={PackagesUrl}",
                    WorkingDirectory = runtime.Value.DesktopDirectory,
                    UseShellExecute = false
                };
                start.Environment.Remove("ELECTRON_RUN_AS_NODE");
                start.Environment["OW_DEV_KEY"] = key;
                if (runtime.Value.BackendExecutable is not null)
                    start.Environment["WARFRAME_TRACKER_BACKEND_EXE"] = runtime.Value.BackendExecutable;
                var trackerProcess = Process.Start(start);
                start.Environment.Remove("OW_DEV_KEY");
                start.Environment.Remove("WARFRAME_TRACKER_BACKEND_EXE");
                _key.Clear();
                key = string.Empty;
                if (trackerProcess is null)
                    throw new InvalidOperationException("Windows no devolvió un proceso para ow-electron.");
                if (trackerProcess.WaitForExit(1800))
                    throw new InvalidOperationException(
                        $"ow-electron terminó inmediatamente con código {trackerProcess.ExitCode}.");
                Close();
            }
            catch (Exception exception)
            {
                _key.Clear();
                MessageBox.Show(this,
                    $"No se pudo iniciar Warframe Tracker:\n\n{exception.Message}",
                    "Error al iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static (string Executable, string DesktopDirectory, string? BackendExecutable)? LocateRuntime()
        {
            var portableRoot = AppContext.BaseDirectory;
            var portableDesktop = Path.Combine(portableRoot, "desktop");
            var portableRuntime = Path.Combine(
                portableDesktop, "node_modules", "electron", "dist", "electron.exe");
            var portableBackend = Path.Combine(portableRoot, "backend", "WarframeInventory.exe");
            if (File.Exists(portableRuntime)
                && File.Exists(Path.Combine(portableDesktop, "package.json"))
                && File.Exists(portableBackend))
            {
                return (portableRuntime, portableDesktop, portableBackend);
            }

            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (var depth = 0; depth < 7 && directory is not null; depth++, directory = directory.Parent)
            {
                var desktop = Path.Combine(directory.FullName, "desktop-electron");
                var executable = Path.Combine(
                    desktop, "node_modules", "@overwolf", "ow-electron", "dist", "electron.exe");
                if (File.Exists(Path.Combine(desktop, "package.json")) && File.Exists(executable))
                    return (executable, desktop, null);
            }
            return null;
        }
    }
}
