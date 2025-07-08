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
        private Panel topPanel;
        private Panel bottomPanel;
        private Panel logPanel;
        private Panel mainPanel;
        private ComboBox driveComboBox;
        private Button refreshDrivesButton;
        private CheckBox deleteDuplicatesCheckBox;
        private CheckBox compressOrphansCheckBox;
        private CheckBox dryRunCheckBox;
        private Button scanButton;
        private ListView filesListView;
        private Button processButton;
        private Button selectAllButton;
        private ProgressBar progressBar;
        private TextBox logTextBox;
        private Label statusLabel;
        private Label logLabel;
        private bool isLogCollapsed = false;
        private int collapsedLogHeight = 25;
        private int expandedLogHeight = 150;

        private ConcurrentQueue<FileItem> foundFiles = new ConcurrentQueue<FileItem>();
        private ConcurrentQueue<string> logMessages = new ConcurrentQueue<string>();
        
        private CancellationTokenSource scanCancellationTokenSource;
        private CancellationTokenSource processCancellationTokenSource;
        private System.Windows.Forms.Timer uiUpdateTimer;
        
        private volatile bool isScanRunning = false;
        private volatile bool isProcessRunning = false;
        private volatile bool isPopulatingResults = false;
        private volatile int totalDirectories = 0;
        private volatile int processedDirectories = 0;
        private List<FileItem> allFiles = new List<FileItem>();

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
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(750, 500);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;

            // IMPORTANT: Create panels in reverse order (bottom to top) for proper layout
            
            // Bottom panel for status and progress
            bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };
            this.Controls.Add(bottomPanel);

            statusLabel = new Label
            {
                Text = "Ready",
                Location = new Point(10, 5),
                Size = new Size(200, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            bottomPanel.Controls.Add(statusLabel);

            progressBar = new ProgressBar
            {
                Location = new Point(10, 25),
                Size = new Size(200, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            bottomPanel.Controls.Add(progressBar);

            // Log panel (collapsible) - must be added after bottom panel
            logPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = expandedLogHeight
            };
            this.Controls.Add(logPanel);

            logLabel = new Label
            {
                Text = "▼ Log:",
                Location = new Point(10, 5),
                Size = new Size(60, 20),
                Font = new Font(this.Font, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            logLabel.Click += LogLabel_Click;
            logPanel.Controls.Add(logLabel);

            logTextBox = new TextBox
            {
                Location = new Point(10, 25),
                Size = new Size(200, expandedLogHeight - 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };
            logPanel.Controls.Add(logTextBox);

            // Top panel for drive selection
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80
            };
            this.Controls.Add(topPanel);

            var driveLabel = new Label
            {
                Text = "Drive:",
                Location = new Point(10, 15),
                Size = new Size(40, 20)
            };
            topPanel.Controls.Add(driveLabel);

            driveComboBox = new ComboBox
            {
                Location = new Point(55, 12),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            topPanel.Controls.Add(driveComboBox);

            refreshDrivesButton = new Button
            {
                Text = "↻",
                Location = new Point(260, 11),
                Size = new Size(30, 27),
                UseVisualStyleBackColor = true,
                Font = new Font("Arial", 12, FontStyle.Bold)
            };
            refreshDrivesButton.Click += RefreshDrivesButton_Click;
            var toolTip = new ToolTip();
            toolTip.SetToolTip(refreshDrivesButton, "Refresh drive list");
            topPanel.Controls.Add(refreshDrivesButton);

            scanButton = new Button
            {
                Text = "Scan",
                Location = new Point(300, 10),
                Size = new Size(75, 30),
                UseVisualStyleBackColor = true
            };
            scanButton.Click += ScanButton_Click;
            topPanel.Controls.Add(scanButton);

            deleteDuplicatesCheckBox = new CheckBox
            {
                Text = "Delete Duplicates",
                Location = new Point(10, 50),
                Size = new Size(130, 20),
                Checked = true
            };
            topPanel.Controls.Add(deleteDuplicatesCheckBox);

            compressOrphansCheckBox = new CheckBox
            {
                Text = "Compress Orphans",
                Location = new Point(150, 50),
                Size = new Size(130, 20),
                Checked = true
            };
            topPanel.Controls.Add(compressOrphansCheckBox);

            dryRunCheckBox = new CheckBox
            {
                Text = "Dry Run Mode",
                Location = new Point(290, 50),
                Size = new Size(100, 20),
                Checked = true
            };
            topPanel.Controls.Add(dryRunCheckBox);

            // Main content panel - DO NOT use Dock.Fill
            mainPanel = new Panel
            {
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.None
            };
            this.Controls.Add(mainPanel);

            // Select All button
            selectAllButton = new Button
            {
                Text = "Select All",
                Location = new Point(10, 10),
                Size = new Size(80, 25),
                UseVisualStyleBackColor = true
            };
            selectAllButton.Click += SelectAllButton_Click;
            mainPanel.Controls.Add(selectAllButton);

            // Process button
            processButton = new Button
            {
                Text = "Delete Duplicates and Zip Orphans",
                Size = new Size(200, 35),
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            processButton.Click += ProcessButton_Click;
            mainPanel.Controls.Add(processButton);

            // Files ListView
            filesListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true
            };

            // Add columns
            filesListView.Columns.Add("Name", 300);
            filesListView.Columns.Add("Size", 80, HorizontalAlignment.Right);
            filesListView.Columns.Add("File Type", 150);
            filesListView.Columns.Add("Path", 400);

            mainPanel.Controls.Add(filesListView);

            // Setup form events
            this.Load += MainForm_Load;
            this.Resize += MainForm_Resize;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Ensure controls fill their panels properly
            statusLabel.Width = bottomPanel.ClientSize.Width - 20;
            progressBar.Width = bottomPanel.ClientSize.Width - 20;
            logTextBox.Width = logPanel.ClientSize.Width - 20;
            
            CalculateLayout();
            DebugLayout();
            ValidateLayout();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            CalculateLayout();
        }

        private void CalculateLayout()
        {
            // Calculate main panel position and size
            int topOffset = topPanel.Height;
            int bottomOffset = bottomPanel.Height + logPanel.Height;
            
            mainPanel.Location = new Point(0, topOffset);
            mainPanel.Size = new Size(
                this.ClientSize.Width,
                this.ClientSize.Height - topOffset - bottomOffset
            );

            // Calculate positions for controls inside main panel
            if (mainPanel.Height > 100) // Only layout if we have reasonable space
            {
                // Select All button stays at top
                selectAllButton.Location = new Point(10, 10);

                // Process button at bottom of main panel
                processButton.Location = new Point(
                    10,
                    mainPanel.Height - processButton.Height - 10
                );

                // ListView fills the space between
                filesListView.Location = new Point(10, selectAllButton.Bottom + 10);
                filesListView.Size = new Size(
                    mainPanel.Width - 20,
                    processButton.Top - filesListView.Top - 10
                );
            }
        }

        private void DebugLayout()
        {
            Console.WriteLine("=== LAYOUT DEBUG ===");
            Console.WriteLine($"Form ClientSize: {this.ClientSize}");
            Console.WriteLine($"TopPanel: Location={topPanel.Location}, Size={topPanel.Size}, Bottom={topPanel.Bottom}");
            Console.WriteLine($"  - DeleteDuplicates CB: Location={deleteDuplicatesCheckBox.Location}, Checked={deleteDuplicatesCheckBox.Checked}");
            Console.WriteLine($"  - CompressOrphans CB: Location={compressOrphansCheckBox.Location}, Checked={compressOrphansCheckBox.Checked}");
            Console.WriteLine($"  - DryRun CB: Location={dryRunCheckBox.Location}, Checked={dryRunCheckBox.Checked}");
            Console.WriteLine($"BottomPanel: Location={bottomPanel.Location}, Size={bottomPanel.Size}, Top={bottomPanel.Top}");
            Console.WriteLine($"LogPanel: Location={logPanel.Location}, Size={logPanel.Size}, Top={logPanel.Top}, Collapsed={isLogCollapsed}");
            Console.WriteLine($"MainPanel: Location={mainPanel.Location}, Size={mainPanel.Size}");
            Console.WriteLine($"  - SelectAll: Location={selectAllButton.Location}, Size={selectAllButton.Size}");
            Console.WriteLine($"  - ListView: Location={filesListView.Location}, Size={filesListView.Size}");
            Console.WriteLine($"  - ProcessBtn: Location={processButton.Location}, Size={processButton.Size}");
            
            // Calculate actual screen positions
            var listViewScreenPos = mainPanel.PointToScreen(filesListView.Location);
            var formScreenPos = this.PointToScreen(Point.Empty);
            Console.WriteLine($"ListView screen position relative to form: ({listViewScreenPos.X - formScreenPos.X}, {listViewScreenPos.Y - formScreenPos.Y})");
            
            Console.WriteLine($"Available space calculation:");
            Console.WriteLine($"  - Form height: {this.ClientSize.Height}");
            Console.WriteLine($"  - Top used: {topPanel.Height}");
            Console.WriteLine($"  - Bottom used: {bottomPanel.Height + logPanel.Height}");
            Console.WriteLine($"  - Main panel space: {this.ClientSize.Height - topPanel.Height - bottomPanel.Height - logPanel.Height}");
            Console.WriteLine("===================");
        }

        private bool ValidateLayout()
        {
            var errors = new List<string>();
            
            // Check main panel positioning
            if (mainPanel.Top < topPanel.Bottom)
                errors.Add($"Main panel overlaps top panel (MainPanel.Top={mainPanel.Top}, TopPanel.Bottom={topPanel.Bottom})");
            
            if (mainPanel.Bottom > logPanel.Top)
                errors.Add($"Main panel overlaps log panel (MainPanel.Bottom={mainPanel.Bottom}, LogPanel.Top={logPanel.Top})");
            
            // Check controls within main panel
            if (filesListView.Top < selectAllButton.Bottom)
                errors.Add("ListView overlaps Select All button");
            
            if (processButton.Bottom > mainPanel.ClientSize.Height)
                errors.Add($"Process button extends beyond main panel (Button.Bottom={processButton.Bottom}, Panel.Height={mainPanel.ClientSize.Height})");
            
            if (filesListView.Bottom > processButton.Top)
                errors.Add("ListView overlaps Process button");
            
            // Check visibility
            if (filesListView.Height < 50)
                errors.Add($"ListView too small (Height={filesListView.Height})");
            
            if (errors.Any())
            {
                Console.WriteLine("LAYOUT VALIDATION ERRORS:");
                foreach (var error in errors)
                    Console.WriteLine($"  - {error}");
                return false;
            }
            else
            {
                Console.WriteLine("Layout validation: PASSED");
                return true;
            }
        }

        private void RefreshDrivesButton_Click(object sender, EventArgs e)
        {
            LoadDrives();
            QueueLogMessage("Drive list refreshed");
        }

        private void LogLabel_Click(object sender, EventArgs e)
        {
            isLogCollapsed = !isLogCollapsed;
            
            if (isLogCollapsed)
            {
                logPanel.Height = collapsedLogHeight;
                logLabel.Text = "▶ Log:";
                logTextBox.Visible = false;
            }
            else
            {
                logPanel.Height = expandedLogHeight;
                logLabel.Text = "▼ Log:";
                logTextBox.Visible = true;
            }
            
            // Recalculate layout after changing log panel size
            CalculateLayout();
            Console.WriteLine($"Log panel toggled. New height: {logPanel.Height}");
            DebugLayout();
        }

        private void SelectAllButton_Click(object sender, EventArgs e)
        {
            bool shouldCheck = filesListView.CheckedItems.Count < filesListView.Items.Count;
            foreach (ListViewItem item in filesListView.Items)
            {
                item.Checked = shouldCheck;
            }
        }

        private void SetupUIUpdateTimer()
        {
            uiUpdateTimer = new System.Windows.Forms.Timer();
            uiUpdateTimer.Interval = 500;
            uiUpdateTimer.Tick += UIUpdateTimer_Tick;
        }

        private void LoadDrives()
        {
            try
            {
                var selectedDrive = (driveComboBox.SelectedItem as DriveInfo)?.DeviceID;
                driveComboBox.Items.Clear();

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

                    var driveInfo = new DriveInfo { DisplayText = displayText, DeviceID = drive.DeviceID };
                    driveComboBox.Items.Add(driveInfo);

                    if (driveInfo.DeviceID == selectedDrive)
                        driveComboBox.SelectedItem = driveInfo;
                }

                if (driveComboBox.SelectedItem == null && driveComboBox.Items.Count > 0)
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
                scanCancellationTokenSource?.Cancel();
                return;
            }

            if (driveComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a drive to scan.", "No Drive Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedDrive = ((DriveInfo)driveComboBox.SelectedItem).DeviceID;
            
            ClearResults();
            await StartScanAsync(selectedDrive);
        }

        private async void ProcessButton_Click(object sender, EventArgs e)
        {
            if (isProcessRunning)
            {
                processCancellationTokenSource?.Cancel();
                return;
            }

            var selectedItems = filesListView.CheckedItems.Cast<ListViewItem>()
                .Select(item => item.Tag as FileItem)
                .Where(file => file != null)
                .ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("No items selected for processing.", "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Filter based on checkboxes
            var duplicatesToProcess = deleteDuplicatesCheckBox.Checked 
                ? selectedItems.Where(f => f.FileType == FileType.Duplicate).ToList()
                : new List<FileItem>();
            
            var orphansToProcess = compressOrphansCheckBox.Checked
                ? selectedItems.Where(f => f.FileType == FileType.Orphan).ToList()
                : new List<FileItem>();

            if (duplicatesToProcess.Count == 0 && orphansToProcess.Count == 0)
            {
                MessageBox.Show("No operations selected. Please check 'Delete Duplicates' and/or 'Compress Orphans'.", 
                    "No Operations", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var message = "Process selected items?\n\n";
            if (duplicatesToProcess.Count > 0)
                message += $"- Delete {duplicatesToProcess.Count} duplicates\n";
            if (orphansToProcess.Count > 0)
                message += $"- Compress {orphansToProcess.Count} orphans\n";

            if (!dryRunCheckBox.Checked)
                message += "\nWARNING: Files will be permanently modified/deleted!";

            if (MessageBox.Show(message, "Confirm Processing", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (!dryRunCheckBox.Checked)
                {
                    if (MessageBox.Show("Are you REALLY sure? This action cannot be undone!", "Final Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
                        return;
                }

                var itemsToProcess = duplicatesToProcess.Concat(orphansToProcess).ToList();
                await StartProcessAsync(itemsToProcess);
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
                
                if (allFiles.Count > 0)
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
                UpdateUI();
            }
        }

        private async Task StartProcessAsync(List<FileItem> selectedItems)
        {
            isProcessRunning = true;
            processCancellationTokenSource = new CancellationTokenSource();
            processButton.Text = "Cancel";
            scanButton.Enabled = false;
            
            uiUpdateTimer.Start();

            try
            {
                await Task.Run(() => ProcessFilesAsync(selectedItems, processCancellationTokenSource.Token), processCancellationTokenSource.Token);
                QueueLogMessage("Processing completed successfully");
                
                if (!dryRunCheckBox.Checked)
                {
                    foreach (var item in selectedItems)
                    {
                        var listItem = filesListView.Items.Cast<ListViewItem>().FirstOrDefault(i => i.Tag == item);
                        if (listItem != null)
                            filesListView.Items.Remove(listItem);
                    }
                    allFiles.RemoveAll(f => selectedItems.Contains(f));
                }
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
                processButton.Text = "Delete Duplicates and Zip Orphans";
                processButton.Enabled = allFiles.Count > 0;
                scanButton.Enabled = true;
                uiUpdateTimer.Stop();
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
                
                if (processedDirectories % 10 == 0 || processedDirectories == totalDirectories)
                {
                    var displayDir = directory == drivePath ? $"{drivePath}\\" : directory;
                    QueueLogMessage($"Scanned: {displayDir} ({processedDirectories}/{totalDirectories})");
                }
            }
        }

        private async Task ProcessFilesAsync(List<FileItem> selectedItems, CancellationToken cancellationToken)
        {
            int totalItems = selectedItems.Count;
            int processedItems = 0;

            foreach (var item in selectedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.FileType == FileType.Duplicate && deleteDuplicatesCheckBox.Checked)
                {
                    await Task.Run(() => ProcessDuplicateFile(item, dryRunCheckBox.Checked, cancellationToken), cancellationToken);
                }
                else if (item.FileType == FileType.Orphan && compressOrphansCheckBox.Checked)
                {
                    await Task.Run(() => ProcessOrphanFile(item, dryRunCheckBox.Checked, cancellationToken), cancellationToken);
                }

                Interlocked.Increment(ref processedItems);
            }
        }

        private void ProcessDuplicateFile(FileItem duplicate, bool dryRun, CancellationToken cancellationToken)
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

        private void ProcessOrphanFile(FileItem orphan, bool dryRun, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (dryRun)
            {
                QueueLogMessage($"[DRY RUN] Would compress: {orphan.FilePath}");
            }
            else
            {
                try
                {
                    var zipPath = Path.Combine(Path.GetDirectoryName(orphan.FilePath), Path.GetFileNameWithoutExtension(orphan.FilePath) + ".zip");
                    
                    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(orphan.FilePath, Path.GetFileName(orphan.FilePath));
                    }

                    if (IsFileInZip(zipPath, Path.GetFileName(orphan.FilePath), orphan.Size))
                    {
                        File.Delete(orphan.FilePath);
                        QueueLogMessage($"[COMPRESSED] {orphan.FilePath} -> {Path.GetFileName(zipPath)}");
                    }
                    else
                    {
                        File.Delete(zipPath);
                        QueueLogMessage($"[ERROR] Compression verification failed: {orphan.FilePath}");
                    }
                }
                catch (Exception ex)
                {
                    QueueLogMessage($"[ERROR] Failed to compress {orphan.FilePath}: {ex.Message}");
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
                                var fileItem = new FileItem
                                {
                                    FileName = otherFile.Name,
                                    FilePath = otherFile.FullName,
                                    Size = otherFile.Length,
                                    FileType = FileType.Duplicate,
                                    ZipFile = zipFile.FullName
                                };
                                foundFiles.Enqueue(fileItem);
                                QueueLogMessage($"Duplicate found: {otherFile.Name}");
                            }
                        }
                    }
                }

                var nonZipFiles = files.Where(f => !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                foreach (var file in nonZipFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var basename = Path.GetFileNameWithoutExtension(file);
                    var correspondingZip = Path.Combine(directoryPath, basename + ".zip");

                    if (!File.Exists(correspondingZip))
                    {
                        var fileInfo = new FileInfo(file);
                        var fileItem = new FileItem
                        {
                            FileName = fileInfo.Name,
                            FilePath = fileInfo.FullName,
                            Size = fileInfo.Length,
                            FileType = FileType.Orphan
                        };
                        foundFiles.Enqueue(fileItem);
                        QueueLogMessage($"Orphan found: {fileInfo.Name}");
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

            var messagesToAdd = new List<string>();
            while (logMessages.TryDequeue(out string message) && messagesToAdd.Count < 5)
            {
                messagesToAdd.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            }

            if (messagesToAdd.Count > 0 && !isLogCollapsed)
            {
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
            while (foundFiles.TryDequeue(out _)) { }
            while (logMessages.TryDequeue(out _)) { }
            
            allFiles.Clear();

            filesListView.Items.Clear();
            logTextBox.Clear();
            processButton.Enabled = false;
            progressBar.Value = 0;
            statusLabel.Text = "Ready";

            totalDirectories = 0;
            processedDirectories = 0;
        }

        private async Task PopulateResultsAsync()
        {
            if (isPopulatingResults) return;
            isPopulatingResults = true;

            try
            {
                await Task.Run(() =>
                {
                    allFiles.Clear();

                    while (foundFiles.TryDequeue(out FileItem file))
                    {
                        allFiles.Add(file);
                    }
                });

                if (InvokeRequired)
                {
                    Invoke(new Action(() => filesListView.Items.Clear()));
                }
                else
                {
                    filesListView.Items.Clear();
                }

                const int batchSize = 50;
                for (int i = 0; i < allFiles.Count; i += batchSize)
                {
                    var batch = allFiles.Skip(i).Take(batchSize).ToList();
                    var listItems = new List<ListViewItem>();

                    foreach (var file in batch)
                    {
                        var item = new ListViewItem(file.FileName);
                        var sizeMB = Math.Round(file.Size / (1024.0 * 1024.0), 2);
                        item.SubItems.Add($"{sizeMB} MB");
                        item.SubItems.Add(file.FileType == FileType.Orphan ? "Orphan" : "Zipped Version Exists");
                        item.SubItems.Add(Path.GetDirectoryName(file.FilePath));
                        item.Tag = file;
                        item.Checked = true;
                        listItems.Add(item);
                    }

                    if (InvokeRequired)
                    {
                        await Task.Run(() => Invoke(new Action(() =>
                        {
                            filesListView.Items.AddRange(listItems.ToArray());
                        })));
                    }
                    else
                    {
                        filesListView.Items.AddRange(listItems.ToArray());
                    }

                    await Task.Delay(10);
                }

                // Auto-resize columns
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        foreach (ColumnHeader column in filesListView.Columns)
                        {
                            column.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                            if (column.Width < 80 && column.Index == 1) // Size column
                                column.Width = 80;
                            if (column.Width < 150 && column.Index == 2) // File Type column
                                column.Width = 150;
                        }
                    }));
                }
                else
                {
                    foreach (ColumnHeader column in filesListView.Columns)
                    {
                        column.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                        if (column.Width < 80 && column.Index == 1) // Size column
                            column.Width = 80;
                        if (column.Width < 150 && column.Index == 2) // File Type column
                            column.Width = 150;
                    }
                }

                QueueLogMessage($"Results loaded: {allFiles.Count(f => f.FileType == FileType.Duplicate)} duplicates, {allFiles.Count(f => f.FileType == FileType.Orphan)} orphans");
            }
            finally
            {
                isPopulatingResults = false;
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

        public class FileItem
        {
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public long Size { get; set; }
            public FileType FileType { get; set; }
            public string ZipFile { get; set; }
        }

        public enum FileType
        {
            Duplicate,
            Orphan
        }
    }
}