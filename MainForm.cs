using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DuplicateROMCleanZipper
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

        private ConcurrentQueue<DuplicateFile> foundDuplicates = new ConcurrentQueue<DuplicateFile>();
        private ConcurrentQueue<string> foundOrphans = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> logMessages = new ConcurrentQueue<string>();
        
        private CancellationTokenSource scanCancellationTokenSource;
        private CancellationTokenSource processCancellationTokenSource;
        private System.Windows.Forms.Timer uiUpdateTimer;
        
        private volatile bool isScanRunning = false;
        private volatile bool isProcessRunning = false;
        private volatile bool isPopulatingResults = false;
        private volatile int totalDirectories = 0;
        private volatile int processedDirectories = 0;
        private List<DuplicateFile> allDuplicates = new List<DuplicateFile>();
        private List<string> allOrphans = new List<string>();

        public MainForm()
        {
            InitializeComponent();
            SetupUIUpdateTimer();
            LoadDrives();
        }

        private void InitializeComponent()
        {
            // Form setup
            this.Text = "Duplicate ROM CleanZipper";
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

        private void SetupUIUpdateTimer()
        {
            uiUpdateTimer = new System.Windows.Forms.Timer();
            uiUpdateTimer.Interval = 500; // Update UI every 500ms to reduce overhead
            uiUpdateTimer.Tick += UIUpdateTimer_Tick;
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
                QueueLogMessage($"Error loading drives: {ex.Message}");
            }
        }

        private async void ScanButton_Click(object sender, EventArgs e)
        {
            if (isScanRunning)
            {
                // Cancel scan
                scanCancellationTokenSource?.Cancel();
                return;
            }

            if (driveComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a drive to scan.", "No Drive Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedDrive = ((DriveInfo)driveComboBox.SelectedItem).DeviceID;
            
            // Clear previous results
            ClearResults();

            // Start scan
            await StartScanAsync(selectedDrive);
        }

        private async void ProcessButton_Click(object sender, EventArgs e)
        {
            if (isProcessRunning)
            {
                processCancellationTokenSource?.Cancel();
                return;
            }

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
                await StartProcessAsync(selectedDuplicates, selectedOrphans);
            }
        }

        private async Task StartScanAsync(string drivePath)
        {
            isScanRunning = true;
            scanCancellationTokenSource = new CancellationTokenSource();
            scanButton.Text = "Cancel";
            scanButton.Enabled = true;
            processButton.Enabled = false;
            
            uiUpdateTimer.Start();
            QueueLogMessage($"Starting scan of {drivePath}...");

            try
            {
                await Task.Run(() => ScanDriveAsync(drivePath, scanCancellationTokenSource.Token), scanCancellationTokenSource.Token);
                
                QueueLogMessage("Scan completed - preparing results...");
                await PopulateResultsAsync();
                
                if (allDuplicates.Count > 0 || allOrphans.Count > 0)
                    processButton.Enabled = true;
            }
            catch (OperationCanceledException)
            {
                QueueLogMessage("Scan cancelled by user");
            }
            catch (Exception ex)
            {
                QueueLogMessage($"Scan error: {ex.Message}");
            }
            finally
            {
                isScanRunning = false;
                scanButton.Text = "Scan";
                uiUpdateTimer.Stop();
                
                // Final UI update
                UpdateUI();
            }
        }

        private async Task StartProcessAsync(List<string> selectedDuplicates, List<string> selectedOrphans)
        {
            isProcessRunning = true;
            processCancellationTokenSource = new CancellationTokenSource();
            processButton.Text = "Cancel";
            scanButton.Enabled = false;
            
            uiUpdateTimer.Start();

            try
            {
                await Task.Run(() => ProcessFilesAsync(selectedDuplicates, selectedOrphans, processCancellationTokenSource.Token), processCancellationTokenSource.Token);
                QueueLogMessage("Processing completed successfully");
            }
            catch (OperationCanceledException)
            {
                QueueLogMessage("Processing cancelled by user");
            }
            catch (Exception ex)
            {
                QueueLogMessage($"Processing error: {ex.Message}");
            }
            finally
            {
                isProcessRunning = false;
                processButton.Text = "Process Selected";
                scanButton.Enabled = true;
                uiUpdateTimer.Stop();
                
                // Final UI update
                UpdateUI();
            }
        }

        private async Task ScanDriveAsync(string drivePath, CancellationToken cancellationToken)
        {
            var directories = await Task.Run(() => GetAccessibleDirectories(drivePath, cancellationToken), cancellationToken);
            var allDirectories = directories.ToList();
            allDirectories.Insert(0, drivePath);

            totalDirectories = allDirectories.Count;
            processedDirectories = 0;

            QueueLogMessage($"Found {totalDirectories} directories to scan (including {drivePath}\\)");

            foreach (var directory in allDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Run(() => ScanDirectory(directory, cancellationToken), cancellationToken);
                
                Interlocked.Increment(ref processedDirectories);
                
                // Only log every 10th directory to reduce UI flooding
                if (processedDirectories % 10 == 0 || processedDirectories == totalDirectories)
                {
                    var displayDir = directory == drivePath ? $"{drivePath}\\" : directory;
                    QueueLogMessage($"Scanned: {displayDir} ({processedDirectories}/{totalDirectories})");
                }
            }
        }

        private async Task ProcessFilesAsync(List<string> selectedDuplicates, List<string> selectedOrphans, CancellationToken cancellationToken)
        {
            int totalItems = selectedDuplicates.Count + selectedOrphans.Count;
            int processedItems = 0;

            // Process duplicates
            if (deleteDuplicatesCheckBox.Checked)
            {
                foreach (var duplicateDisplay in selectedDuplicates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var duplicate = FindDuplicateByDisplay(duplicateDisplay);
                    if (duplicate != null)
                    {
                        await Task.Run(() => ProcessDuplicateFile(duplicate, dryRunCheckBox.Checked, cancellationToken), cancellationToken);
                        Interlocked.Increment(ref processedItems);
                    }
                }
            }

            // Process orphans
            if (compressOrphansCheckBox.Checked)
            {
                foreach (var orphanDisplay in selectedOrphans)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var orphan = FindOrphanByDisplay(orphanDisplay);
                    if (orphan != null)
                    {
                        await Task.Run(() => ProcessOrphanFile(orphan, dryRunCheckBox.Checked, cancellationToken), cancellationToken);
                        Interlocked.Increment(ref processedItems);
                    }
                }
            }
        }

        private void ProcessDuplicateFile(DuplicateFile duplicate, bool dryRun, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (dryRun)
            {
                QueueLogMessage($"[DRY RUN] Would delete: {duplicate.FilePath}");
            }
            else
            {
                try
                {
                    File.Delete(duplicate.FilePath);
                    QueueLogMessage($"[DELETED] {duplicate.FilePath}");
                }
                catch (Exception ex)
                {
                    QueueLogMessage($"[ERROR] Failed to delete {duplicate.FilePath}: {ex.Message}");
                }
            }
        }

        private void ProcessOrphanFile(string orphanPath, bool dryRun, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (dryRun)
            {
                QueueLogMessage($"[DRY RUN] Would compress: {orphanPath}");
            }
            else
            {
                try
                {
                    var zipPath = Path.Combine(Path.GetDirectoryName(orphanPath), Path.GetFileNameWithoutExtension(orphanPath) + ".zip");
                    
                    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(orphanPath, Path.GetFileName(orphanPath));
                    }

                    // Verify compression
                    if (IsFileInZip(zipPath, Path.GetFileName(orphanPath), new FileInfo(orphanPath).Length))
                    {
                        File.Delete(orphanPath);
                        QueueLogMessage($"[COMPRESSED] {orphanPath} -> {Path.GetFileName(zipPath)}");
                    }
                    else
                    {
                        File.Delete(zipPath);
                        QueueLogMessage($"[ERROR] Compression verification failed: {orphanPath}");
                    }
                }
                catch (Exception ex)
                {
                    QueueLogMessage($"[ERROR] Failed to compress {orphanPath}: {ex.Message}");
                }
            }
        }

        private IEnumerable<string> GetAccessibleDirectories(string rootPath, CancellationToken cancellationToken)
        {
            var directories = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
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
                        QueueLogMessage($"Skipping protected directory: {dirDisplayName}");
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
                    QueueLogMessage($"Access denied (skipping): {dirDisplayName}");
                }
                catch (Exception ex)
                {
                    var dirDisplayName = string.IsNullOrEmpty(Path.GetFileName(currentDir)) ? currentDir : Path.GetFileName(currentDir);
                    QueueLogMessage($"Error accessing {dirDisplayName}: {ex.Message}");
                }
            }

            return directories;
        }

        private void ScanDirectory(string directoryPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
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
                    cancellationToken.ThrowIfCancellationRequested();

                    var zipFile = group.FirstOrDefault(f => f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase));
                    var otherFiles = group.Where(f => !f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase));

                    if (zipFile != null && otherFiles.Any())
                    {
                        foreach (var otherFile in otherFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (IsFileInZip(zipFile.FullName, otherFile.Name, otherFile.Length))
                            {
                                var duplicate = new DuplicateFile
                                {
                                    ZipFile = zipFile.FullName,
                                    FilePath = otherFile.FullName,
                                    Size = otherFile.Length
                                };
                                foundDuplicates.Enqueue(duplicate);
                                QueueLogMessage($"Duplicate found: {otherFile.Name}");
                            }
                        }
                    }
                }

                // Find orphans
                var nonZipFiles = files.Where(f => !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                foreach (var file in nonZipFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var basename = Path.GetFileNameWithoutExtension(file);
                    var correspondingZip = Path.Combine(directoryPath, basename + ".zip");

                    if (!File.Exists(correspondingZip))
                    {
                        foundOrphans.Enqueue(file);
                        QueueLogMessage($"Orphan found: {Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                QueueLogMessage($"Error scanning {directoryPath}: {ex.Message}");
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

        private void UIUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateUI));
                return;
            }

            // Update progress
            if (totalDirectories > 0)
            {
                int progress = (processedDirectories * 100) / totalDirectories;
                progressBar.Value = Math.Min(100, Math.Max(0, progress));
                
                if (isScanRunning)
                    statusLabel.Text = $"Scanning: {processedDirectories}/{totalDirectories} directories ({progress}%)";
                else if (isProcessRunning)
                    statusLabel.Text = $"Processing files...";
                else if (isPopulatingResults)
                    statusLabel.Text = $"Loading results...";
                else
                    statusLabel.Text = "Ready";
            }

            // Update log (batch process with better throttling)
            var messagesToAdd = new List<string>();
            while (logMessages.TryDequeue(out string message) && messagesToAdd.Count < 5) // Reduced from 10 to 5
            {
                messagesToAdd.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            }

            if (messagesToAdd.Count > 0)
            {
                // Limit total lines in log to prevent memory issues
                var currentLines = logTextBox.Lines.Length;
                if (currentLines > 1000)
                {
                    var recentLines = logTextBox.Lines.Skip(currentLines - 800).ToArray();
                    logTextBox.Lines = recentLines;
                }

                logTextBox.AppendText(string.Join("\r\n", messagesToAdd) + "\r\n");
                logTextBox.ScrollToCaret();
            }
        }

        private void ClearResults()
        {
            // Clear collections
            while (foundDuplicates.TryDequeue(out _)) { }
            while (foundOrphans.TryDequeue(out _)) { }
            while (logMessages.TryDequeue(out _)) { }
            
            allDuplicates.Clear();
            allOrphans.Clear();

            // Clear UI
            duplicatesListBox.Items.Clear();
            orphansListBox.Items.Clear();
            logTextBox.Clear();
            processButton.Enabled = false;
            progressBar.Value = 0;
            statusLabel.Text = "Ready";

            // Reset counters
            totalDirectories = 0;
            processedDirectories = 0;
        }

        private async Task PopulateResultsAsync()
        {
            if (isPopulatingResults) return;
            isPopulatingResults = true;

            try
            {
                // First, collect all results from queues
                await Task.Run(() =>
                {
                    allDuplicates.Clear();
                    allOrphans.Clear();

                    while (foundDuplicates.TryDequeue(out DuplicateFile dup))
                    {
                        allDuplicates.Add(dup);
                    }

                    while (foundOrphans.TryDequeue(out string orphan))
                    {
                        allOrphans.Add(orphan);
                    }
                });

                // Clear the UI lists first
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        duplicatesListBox.Items.Clear();
                        orphansListBox.Items.Clear();
                    }));
                }
                else
                {
                    duplicatesListBox.Items.Clear();
                    orphansListBox.Items.Clear();
                }

                // Populate duplicates in batches of 50
                const int batchSize = 50;
                for (int i = 0; i < allDuplicates.Count; i += batchSize)
                {
                    var batch = allDuplicates.Skip(i).Take(batchSize).ToList();
                    var displayItems = batch.Select(dup =>
                    {
                        var sizeMB = Math.Round(dup.Size / (1024.0 * 1024.0), 2);
                        return $"{Path.GetFileName(dup.FilePath)} ({sizeMB} MB) - {Path.GetDirectoryName(dup.FilePath)}";
                    }).ToList();

                    if (InvokeRequired)
                    {
                        await Task.Run(() => Invoke(new Action(() =>
                        {
                            foreach (var item in displayItems)
                            {
                                duplicatesListBox.Items.Add(item, true);
                            }
                        })));
                    }
                    else
                    {
                        foreach (var item in displayItems)
                        {
                            duplicatesListBox.Items.Add(item, true);
                        }
                    }

                    // Small delay to keep UI responsive
                    await Task.Delay(10);
                }

                // Populate orphans in batches
                for (int i = 0; i < allOrphans.Count; i += batchSize)
                {
                    var batch = allOrphans.Skip(i).Take(batchSize).ToList();
                    var displayItems = batch.Select(orphan =>
                    {
                        var fileInfo = new FileInfo(orphan);
                        var sizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2);
                        return $"{fileInfo.Name} ({sizeMB} MB) - {fileInfo.DirectoryName}";
                    }).ToList();

                    if (InvokeRequired)
                    {
                        await Task.Run(() => Invoke(new Action(() =>
                        {
                            foreach (var item in displayItems)
                            {
                                orphansListBox.Items.Add(item, true);
                            }
                        })));
                    }
                    else
                    {
                        foreach (var item in displayItems)
                        {
                            orphansListBox.Items.Add(item, true);
                        }
                    }

                    // Small delay to keep UI responsive
                    await Task.Delay(10);
                }

                QueueLogMessage($"Results loaded: {allDuplicates.Count} duplicates, {allOrphans.Count} orphans");
            }
            finally
            {
                isPopulatingResults = false;
            }
        }

        private DuplicateFile FindDuplicateByDisplay(string displayText)
        {
            var fileName = displayText.Split('(')[0].Trim();
            return allDuplicates.FirstOrDefault(d => Path.GetFileName(d.FilePath) == fileName);
        }

        private string FindOrphanByDisplay(string displayText)
        {
            var fileName = displayText.Split('(')[0].Trim();
            return allOrphans.FirstOrDefault(o => Path.GetFileName(o) == fileName);
        }

        private void ToggleAllItems(CheckedListBox listBox)
        {
            bool checkAll = listBox.CheckedItems.Count < listBox.Items.Count;
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                listBox.SetItemChecked(i, checkAll);
            }
        }

        private void QueueLogMessage(string message)
        {
            logMessages.Enqueue(message);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            scanCancellationTokenSource?.Cancel();
            processCancellationTokenSource?.Cancel();
            uiUpdateTimer?.Stop();
            uiUpdateTimer?.Dispose();
            base.OnFormClosing(e);
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
    }
}