using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DuplicateCleaner
{
    public partial class MainForm : Form
    {
        private ComboBox driveComboBox;
        private CheckBox deleteDuplicatesCheckBox;
        private CheckBox compressOrphansCheckBox;
        private CheckBox dryRunCheckBox;
        private Button scanButton;
        private Button processButton;
        private CheckedListBox duplicatesListBox;
        private CheckedListBox orphansListBox;
        private ProgressBar progressBar;
        private TextBox logTextBox;
        private Label statusLabel;
        private Button selectAllDuplicatesButton;
        private Button selectAllOrphansButton;

        private List<DuplicateFile> foundDuplicates = new List<DuplicateFile>();
        private List<string> foundOrphans = new List<string>();
        private BackgroundWorker scanWorker;
        private BackgroundWorker processWorker;

        public MainForm()
        {
            InitializeComponent();
            SetupBackgroundWorkers();
            LoadDrives();
        }

        private void InitializeComponent()
        {
            // Form setup
            this.Text = "SD Card Duplicate File Cleaner";
            this.Size = new Size(800, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(750, 600);

            // Drive selection
            var driveLabel = new Label
            {
                Text = "Drive:",
                Location = new Point(10, 15),
                Size = new Size(40, 20)
            };
            this.Controls.Add(driveLabel);

            driveComboBox = new ComboBox
            {
                Location = new Point(55, 12),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(driveComboBox);

            scanButton = new Button
            {
                Text = "Scan",
                Location = new Point(270, 10),
                Size = new Size(75, 30),
                UseVisualStyleBackColor = true
            };
            scanButton.Click += ScanButton_Click;
            this.Controls.Add(scanButton);

            // Options
            deleteDuplicatesCheckBox = new CheckBox
            {
                Text = "Delete Duplicates",
                Location = new Point(10, 50),
                Size = new Size(130, 20),
                Checked = true
            };
            this.Controls.Add(deleteDuplicatesCheckBox);

            compressOrphansCheckBox = new CheckBox
            {
                Text = "Compress Orphans",
                Location = new Point(150, 50),
                Size = new Size(130, 20),
                Checked = true
            };
            this.Controls.Add(compressOrphansCheckBox);

            dryRunCheckBox = new CheckBox
            {
                Text = "Dry Run Mode",
                Location = new Point(290, 50),
                Size = new Size(100, 20),
                Checked = true
            };
            this.Controls.Add(dryRunCheckBox);

            // Duplicates section
            var duplicatesLabel = new Label
            {
                Text = "Duplicates Found:",
                Location = new Point(10, 80),
                Size = new Size(120, 20),
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(duplicatesLabel);

            selectAllDuplicatesButton = new Button
            {
                Text = "Select All",
                Location = new Point(140, 78),
                Size = new Size(70, 23),
                UseVisualStyleBackColor = true
            };
            selectAllDuplicatesButton.Click += (s, e) => ToggleAllItems(duplicatesListBox);
            this.Controls.Add(selectAllDuplicatesButton);

            duplicatesListBox = new CheckedListBox
            {
                Location = new Point(10, 105),
                Size = new Size(760, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CheckOnClick = true
            };
            this.Controls.Add(duplicatesListBox);

            // Orphans section
            var orphansLabel = new Label
            {
                Text = "Orphans Found:",
                Location = new Point(10, 235),
                Size = new Size(120, 20),
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(orphansLabel);

            selectAllOrphansButton = new Button
            {
                Text = "Select All",
                Location = new Point(140, 233),
                Size = new Size(70, 23),
                UseVisualStyleBackColor = true
            };
            selectAllOrphansButton.Click += (s, e) => ToggleAllItems(orphansListBox);
            this.Controls.Add(selectAllOrphansButton);

            orphansListBox = new CheckedListBox
            {
                Location = new Point(10, 260),
                Size = new Size(760, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CheckOnClick = true
            };
            this.Controls.Add(orphansListBox);

            // Progress
            progressBar = new ProgressBar
            {
                Location = new Point(10, 390),
                Size = new Size(760, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(progressBar);

            statusLabel = new Label
            {
                Text = "Ready",
                Location = new Point(10, 420),
                Size = new Size(760, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(statusLabel);

            // Log
            var logLabel = new Label
            {
                Text = "Log:",
                Location = new Point(10, 445),
                Size = new Size(40, 20),
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(logLabel);

            logTextBox = new TextBox
            {
                Location = new Point(10, 470),
                Size = new Size(760, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };
            this.Controls.Add(logTextBox);

            // Process button
            processButton = new Button
            {
                Text = "Process Selected",
                Location = new Point(10, 600),
                Size = new Size(120, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            processButton.Click += ProcessButton_Click;
            this.Controls.Add(processButton);
        }

        private void SetupBackgroundWorkers()
        {
            // Scan worker
            scanWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            scanWorker.DoWork += ScanWorker_DoWork;
            scanWorker.ProgressChanged += ScanWorker_ProgressChanged;
            scanWorker.RunWorkerCompleted += ScanWorker_RunWorkerCompleted;

            // Process worker
            processWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            processWorker.DoWork += ProcessWorker_DoWork;
            processWorker.ProgressChanged += ProcessWorker_ProgressChanged;
            processWorker.RunWorkerCompleted += ProcessWorker_RunWorkerCompleted;
        }

        private void LoadDrives()
        {
            try
            {
                var drives = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType = 3").Get()
                    .Cast<ManagementObject>()
                    .Where(drive => string.Compare(drive["DeviceID"].ToString(), "D:", StringComparison.Ordinal) >= 0)
                    .Select(drive => new
                    {
                        DeviceID = drive["DeviceID"].ToString(),
                        Size = Convert.ToInt64(drive["Size"]),
                        VolumeName = drive["VolumeName"]?.ToString()
                    })
                    .OrderBy(d => d.DeviceID);

                foreach (var drive in drives)
                {
                    var sizeGB = Math.Round(drive.Size / (1024.0 * 1024.0 * 1024.0), 2);
                    var displayText = $"{drive.DeviceID} - {sizeGB} GB";
                    if (!string.IsNullOrEmpty(drive.VolumeName))
                        displayText += $" ({drive.VolumeName})";

                    driveComboBox.Items.Add(new DriveInfo { DisplayText = displayText, DeviceID = drive.DeviceID });
                }

                if (driveComboBox.Items.Count > 0)
                    driveComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading drives: {ex.Message}");
            }
        }

        private void ScanButton_Click(object sender, EventArgs e)
        {
            if (driveComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a drive to scan.", "No Drive Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedDrive = ((DriveInfo)driveComboBox.SelectedItem).DeviceID;
            
            // Clear previous results
            foundDuplicates.Clear();
            foundOrphans.Clear();
            duplicatesListBox.Items.Clear();
            orphansListBox.Items.Clear();
            logTextBox.Clear();
            processButton.Enabled = false;

            // Start scan
            scanButton.Enabled = false;
            LogMessage($"Starting scan of {selectedDrive}...");
            scanWorker.RunWorkerAsync(selectedDrive);
        }

        private void ProcessButton_Click(object sender, EventArgs e)
        {
            var selectedDuplicates = duplicatesListBox.CheckedItems.Cast<string>().ToList();
            var selectedOrphans = orphansListBox.CheckedItems.Cast<string>().ToList();

            if (selectedDuplicates.Count == 0 && selectedOrphans.Count == 0)
            {
                MessageBox.Show("No items selected for processing.", "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var message = "Process selected items?\n\n";
            if (selectedDuplicates.Count > 0)
                message += $"- Delete {selectedDuplicates.Count} duplicates\n";
            if (selectedOrphans.Count > 0)
                message += $"- Compress {selectedOrphans.Count} orphans\n";

            if (!dryRunCheckBox.Checked)
                message += "\nWARNING: Files will be permanently modified/deleted!";

            if (MessageBox.Show(message, "Confirm Processing", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (!dryRunCheckBox.Checked)
                {
                    if (MessageBox.Show("Are you REALLY sure? This action cannot be undone!", "Final Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
                        return;
                }

                // Start processing
                processButton.Enabled = false;
                scanButton.Enabled = false;
                var processData = new ProcessData
                {
                    SelectedDuplicates = selectedDuplicates,
                    SelectedOrphans = selectedOrphans,
                    DryRun = dryRunCheckBox.Checked,
                    DeleteDuplicates = deleteDuplicatesCheckBox.Checked,
                    CompressOrphans = compressOrphansCheckBox.Checked
                };
                processWorker.RunWorkerAsync(processData);
            }
        }

        private void ScanWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var drivePath = (string)e.Argument;
            var worker = (BackgroundWorker)sender;

            try
            {
                var directories = GetAccessibleDirectories(drivePath, worker).ToList();
                directories.Insert(0, drivePath); // Include root directory

                worker.ReportProgress(0, $"Found {directories.Count} directories to scan (including {drivePath}\\)");

                for (int i = 0; i < directories.Count; i++)
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    var directory = directories[i];
                    var displayDir = directory == drivePath ? $"{drivePath}\\" : directory;
                    worker.ReportProgress((i * 100) / directories.Count, $"Scanning: {displayDir}");

                    try
                    {
                        ScanDirectory(directory, worker);
                    }
                    catch (Exception ex)
                    {
                        worker.ReportProgress(-1, $"Error scanning {displayDir}: {ex.Message}");
                    }
                }

                worker.ReportProgress(100, "Scan completed");
            }
            catch (Exception ex)
            {
                worker.ReportProgress(-1, $"Scan error: {ex.Message}");
            }
        }

        private IEnumerable<string> GetAccessibleDirectories(string rootPath, BackgroundWorker worker)
        {
            var directories = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                var currentDir = queue.Dequeue();
                
                try
                {
                    var dirInfo = new DirectoryInfo(currentDir);
                    var dirDisplayName = string.IsNullOrEmpty(dirInfo.Name) ? currentDir : dirInfo.Name;
                    
                    // Skip hidden and system directories, but NOT the root directory
                    if (currentDir != rootPath && 
                        ((dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                         (dirInfo.Attributes & FileAttributes.System) == FileAttributes.System))
                    {
                        worker.ReportProgress(-1, $"Skipping protected directory: {dirDisplayName}");
                        continue;
                    }

                    var subdirs = Directory.GetDirectories(currentDir);
                    foreach (var subdir in subdirs)
                    {
                        directories.Add(subdir);
                        queue.Enqueue(subdir);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    var dirDisplayName = string.IsNullOrEmpty(Path.GetFileName(currentDir)) ? currentDir : Path.GetFileName(currentDir);
                    worker.ReportProgress(-1, $"Access denied (skipping): {dirDisplayName}");
                }
                catch (Exception ex)
                {
                    var dirDisplayName = string.IsNullOrEmpty(Path.GetFileName(currentDir)) ? currentDir : Path.GetFileName(currentDir);
                    worker.ReportProgress(-1, $"Error accessing {dirDisplayName}: {ex.Message}");
                }
            }

            return directories;
        }

        private void ScanDirectory(string directoryPath, BackgroundWorker worker)
        {
            var files = Directory.GetFiles(directoryPath);
            if (files.Length == 0) return;

            // Group files by basename
            var fileGroups = files
                .Select(f => new FileInfo(f))
                .GroupBy(f => Path.GetFileNameWithoutExtension(f.Name))
                .Where(g => g.Count() > 1);

            foreach (var group in fileGroups)
            {
                var zipFile = group.FirstOrDefault(f => f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase));
                var otherFiles = group.Where(f => !f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase));

                if (zipFile != null && otherFiles.Any())
                {
                    foreach (var otherFile in otherFiles)
                    {
                        worker.ReportProgress(-1, $"Checking: {otherFile.Name} vs {zipFile.Name}");

                        if (IsFileInZip(zipFile.FullName, otherFile.Name, otherFile.Length))
                        {
                            var duplicate = new DuplicateFile
                            {
                                ZipFile = zipFile.FullName,
                                FilePath = otherFile.FullName,
                                Size = otherFile.Length
                            };
                            foundDuplicates.Add(duplicate);
                            worker.ReportProgress(-1, $"  [MATCH] Duplicate found: {otherFile.Name}");
                        }
                        else
                        {
                            worker.ReportProgress(-1, $"  [NO MATCH] File differs or not in ZIP");
                        }
                    }
                }
            }

            // Find orphans
            var nonZipFiles = files.Where(f => !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            foreach (var file in nonZipFiles)
            {
                var basename = Path.GetFileNameWithoutExtension(file);
                var correspondingZip = Path.Combine(directoryPath, basename + ".zip");

                if (!File.Exists(correspondingZip))
                {
                    foundOrphans.Add(file);
                    worker.ReportProgress(-1, $"Orphan found: {Path.GetFileName(file)}");
                }
            }
        }

        private bool IsFileInZip(string zipPath, string fileName, long expectedSize)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    return entry != null && entry.Length == expectedSize;
                }
            }
            catch
            {
                return false;
            }
        }

        private void ScanWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage >= 0)
            {
                progressBar.Value = e.ProgressPercentage;
                statusLabel.Text = $"Progress: {e.ProgressPercentage}%";
            }

            if (e.UserState != null)
            {
                LogMessage(e.UserState.ToString());
            }
        }

        private void ScanWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressBar.Value = 0;
            statusLabel.Text = "Scan completed";
            scanButton.Enabled = true;

            // Populate results
            duplicatesListBox.Items.Clear();
            foreach (var dup in foundDuplicates)
            {
                var sizeMB = Math.Round(dup.Size / (1024.0 * 1024.0), 2);
                var displayText = $"{Path.GetFileName(dup.FilePath)} ({sizeMB} MB) - {Path.GetDirectoryName(dup.FilePath)}";
                duplicatesListBox.Items.Add(displayText, true);
            }

            orphansListBox.Items.Clear();
            foreach (var orphan in foundOrphans)
            {
                var fileInfo = new FileInfo(orphan);
                var sizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2);
                var displayText = $"{fileInfo.Name} ({sizeMB} MB) - {fileInfo.DirectoryName}";
                orphansListBox.Items.Add(displayText, true);
            }

            LogMessage($"Scan results: {foundDuplicates.Count} duplicates, {foundOrphans.Count} orphans");
            
            if (foundDuplicates.Count > 0 || foundOrphans.Count > 0)
                processButton.Enabled = true;
        }

        private void ProcessWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var data = (ProcessData)e.Argument;
            var worker = (BackgroundWorker)sender;

            int totalItems = data.SelectedDuplicates.Count + data.SelectedOrphans.Count;
            int processedItems = 0;

            // Process duplicates
            if (data.DeleteDuplicates)
            {
                foreach (var duplicateDisplay in data.SelectedDuplicates)
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    var duplicate = FindDuplicateByDisplay(duplicateDisplay);
                    if (duplicate != null)
                    {
                        worker.ReportProgress((processedItems * 100) / totalItems, $"Processing duplicate: {Path.GetFileName(duplicate.FilePath)}");

                        if (data.DryRun)
                        {
                            worker.ReportProgress(-1, $"[DRY RUN] Would delete: {duplicate.FilePath}");
                        }
                        else
                        {
                            try
                            {
                                File.Delete(duplicate.FilePath);
                                worker.ReportProgress(-1, $"[DELETED] {duplicate.FilePath}");
                            }
                            catch (Exception ex)
                            {
                                worker.ReportProgress(-1, $"[ERROR] Failed to delete {duplicate.FilePath}: {ex.Message}");
                            }
                        }
                    }
                    processedItems++;
                }
            }

            // Process orphans
            if (data.CompressOrphans)
            {
                foreach (var orphanDisplay in data.SelectedOrphans)
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    var orphan = FindOrphanByDisplay(orphanDisplay);
                    if (orphan != null)
                    {
                        worker.ReportProgress((processedItems * 100) / totalItems, $"Processing orphan: {Path.GetFileName(orphan)}");

                        if (data.DryRun)
                        {
                            worker.ReportProgress(-1, $"[DRY RUN] Would compress: {orphan}");
                        }
                        else
                        {
                            try
                            {
                                var zipPath = Path.Combine(Path.GetDirectoryName(orphan), Path.GetFileNameWithoutExtension(orphan) + ".zip");
                                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                                {
                                    archive.CreateEntryFromFile(orphan, Path.GetFileName(orphan));
                                }

                                // Verify compression
                                if (IsFileInZip(zipPath, Path.GetFileName(orphan), new FileInfo(orphan).Length))
                                {
                                    File.Delete(orphan);
                                    worker.ReportProgress(-1, $"[COMPRESSED] {orphan} -> {Path.GetFileName(zipPath)}");
                                }
                                else
                                {
                                    File.Delete(zipPath);
                                    worker.ReportProgress(-1, $"[ERROR] Compression verification failed: {orphan}");
                                }
                            }
                            catch (Exception ex)
                            {
                                worker.ReportProgress(-1, $"[ERROR] Failed to compress {orphan}: {ex.Message}");
                            }
                        }
                    }
                    processedItems++;
                }
            }

            worker.ReportProgress(100, "Processing completed");
        }

        private void ProcessWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage >= 0)
            {
                progressBar.Value = e.ProgressPercentage;
                statusLabel.Text = $"Processing: {e.ProgressPercentage}%";
            }

            if (e.UserState != null)
            {
                LogMessage(e.UserState.ToString());
            }
        }

        private void ProcessWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressBar.Value = 0;
            statusLabel.Text = "Processing completed";
            scanButton.Enabled = true;
            processButton.Enabled = true;

            LogMessage("Processing completed!");
        }

        private DuplicateFile FindDuplicateByDisplay(string displayText)
        {
            var fileName = displayText.Split('(')[0].Trim();
            return foundDuplicates.FirstOrDefault(d => Path.GetFileName(d.FilePath) == fileName);
        }

        private string FindOrphanByDisplay(string displayText)
        {
            var fileName = displayText.Split('(')[0].Trim();
            return foundOrphans.FirstOrDefault(o => Path.GetFileName(o) == fileName);
        }

        private void ToggleAllItems(CheckedListBox listBox)
        {
            bool checkAll = listBox.CheckedItems.Count < listBox.Items.Count;
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                listBox.SetItemChecked(i, checkAll);
            }
        }

        private void LogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(LogMessage), message);
                return;
            }

            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            logTextBox.ScrollToCaret();
        }

        public class DriveInfo
        {
            public string DisplayText { get; set; }
            public string DeviceID { get; set; }

            public override string ToString() => DisplayText;
        }

        public class DuplicateFile
        {
            public string ZipFile { get; set; }
            public string FilePath { get; set; }
            public long Size { get; set; }
        }

        public class ProcessData
        {
            public List<string> SelectedDuplicates { get; set; }
            public List<string> SelectedOrphans { get; set; }
            public bool DryRun { get; set; }
            public bool DeleteDuplicates { get; set; }
            public bool CompressOrphans { get; set; }
        }
    }
}