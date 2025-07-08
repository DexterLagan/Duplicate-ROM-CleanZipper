# ROM CleanZipper

A robust Windows desktop application for finding and removing duplicate files on any disk drive. The tool identifies files that exist both as compressed `.zip` archives and as standalone files with other extensions, helping you reclaim storage space while maintaining data integrity. Perfect for ROM collections, media archives, and any organized file collections.

## 🎯 Features

### Core Functionality
- **Smart Duplicate Detection**: Finds files with identical basenames where one is a `.zip` and another has a different extension
- **Content Verification**: Validates that duplicate files actually exist inside ZIP archives with matching file sizes
- **Orphan File Compression**: Automatically compresses standalone files (without corresponding ZIPs) into individual ZIP archives
- **Multi-Extension Support**: Handles any file extensions (`.rom`, `.stm`, `.mp3`, `.pdf`, etc.) alongside ZIP files

### Safety & Usability
- **Dry Run Mode**: Preview exactly what will be deleted/compressed before making changes
- **Double Confirmation**: Two-level confirmation system prevents accidental deletions
- **Protected Directory Handling**: Automatically skips system and hidden directories with informative warnings
- **Real-time Progress**: Live progress bars and detailed logging during scan and processing operations
- **Selective Processing**: Choose exactly which duplicates and orphans to process via checkboxes

### User Interface
- **Drive Selection**: GUI dropdown showing available drives (D: and above) with size information
- **Visual Results**: Separate lists for duplicates and orphans with file sizes and paths
- **Live Logging**: Real-time operation log with timestamps
- **Batch Operations**: Select all/none options for quick processing decisions

## 📋 How It Works

### Duplicate Detection Process
1. **File Grouping**: Groups files by basename (filename without extension)
2. **ZIP Matching**: Identifies groups containing both `.zip` files and other extensions
3. **Content Verification**: Extracts and compares file sizes to ensure actual duplicates
4. **Safe Deletion**: Only deletes verified duplicates after user confirmation

### Orphan Processing
1. **Orphan Identification**: Finds files without corresponding ZIP archives
2. **Individual Compression**: Creates separate ZIP file for each orphan
3. **Integrity Verification**: Confirms successful compression before deleting originals
4. **Rollback Protection**: Keeps originals if compression fails

### Example Scenario
```
Before:
E:\ROMS\retro\
├── pacman.zip      (contains pacman.rom)
├── pacman.rom      (duplicate - will be deleted)
├── tetris.zip      (contains tetris.rom) 
├── tetris.rom      (duplicate - will be deleted)
├── solo.rom        (orphan - will be compressed)
└── unique.zip      (no duplicate - kept unchanged)

After:
E:\ROMS\retro\
├── pacman.zip      (unchanged)
├── tetris.zip      (unchanged)
├── solo.zip        (newly created from solo.rom)
└── unique.zip      (unchanged)
```

## 🚀 Quick Start

### Option 1: Download Executable
1. Download the latest `DuplicateROMCleanZipper.exe` from the [Releases](../../releases) page
2. Double-click to run (no installation required)
3. Select your drive and click "Scan"

### Option 2: Build from Source
```bash
# Clone the repository
git clone https://github.com/[your-username]/duplicate-rom-cleanziper.git
cd duplicate-rom-cleanziper

# Build and run
dotnet build
dotnet run

# Create standalone executable
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

## 📖 Usage Instructions

### Basic Workflow
1. **Select Drive**: Choose any drive to scan from the dropdown menu (external drives, internal drives, network drives)
2. **Configure Options**: 
   - ☑️ Delete Duplicates (remove verified duplicate files)
   - ☑️ Compress Orphans (compress standalone files to ZIP)
   - ☑️ Dry Run Mode (preview only, no actual changes)
3. **Scan**: Click "Scan" to analyze the drive
4. **Review Results**: Examine found duplicates and orphans in the lists
5. **Select Items**: Check/uncheck items you want to process
6. **Process**: Click "Process Selected" and confirm the operations

### Common Use Cases
- **ROM Collections**: Clean up duplicate ROM files and compress individual ROMs
- **Media Archives**: Organize music, video, and document collections
- **Backup Drives**: Remove redundant files from backup storage
- **External Storage**: Optimize USB drives, external HDDs, and network drives

### Safety Features
- **Dry Run Default**: Always starts in dry run mode to prevent accidents
- **Double Confirmation**: Requires two "Yes" responses before any destructive operations
- **Selective Processing**: Process only the files you explicitly select
- **Detailed Logging**: Full operation history with timestamps
- **Automatic Verification**: Ensures ZIP contents match before deletion

### Command Line Interface
The original PowerShell version supports command-line usage:
```powershell
# Dry run (default)
.\duplicate-cleaner.ps1

# Execute with orphan compression
.\duplicate-cleaner.ps1 -Execute -CompressOrphans
```

## 🛠️ Technical Specifications

### System Requirements
- **Operating System**: Windows 10/11 (x64)
- **Framework**: .NET 8.0 Runtime
- **Permissions**: Standard user account (no admin required)
- **Drive Access**: Read/write permissions for target drives (local, external, network)

### File Support
- **ZIP Archives**: Full .NET ZipFile support with integrity checking
- **File Extensions**: Any extension (automatically detected per folder)
- **File Sizes**: No practical size limits (handles files up to available memory)
- **Path Lengths**: Supports Windows long path names
- **Drive Types**: Local drives, external USB/SATA, network mapped drives

### Performance
- **Threading**: Non-blocking UI with background workers
- **Memory Usage**: Efficient streaming for large files
- **Progress Reporting**: Real-time updates during operations
- **Error Handling**: Graceful recovery from access denied errors
- **Drive Compatibility**: Works with any Windows-accessible drive

### Security Features
- **No Network Access**: Completely offline operation
- **No Registry Changes**: Portable application with no system modifications
- **No Data Collection**: Zero telemetry or data transmission
- **Temporary Files**: Automatic cleanup of extraction temp directories

## 🏗️ Building from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 Community (optional, for GUI development)

### Build Steps
```bash
# Clone repository
git clone https://github.com/[your-username]/duplicate-rom-cleanziper.git
cd duplicate-rom-cleanziper

# Restore dependencies
dotnet restore

# Build debug version
dotnet build

# Build release version
dotnet build -c Release

# Create portable executable
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

### Development
```bash
# Run in development mode
dotnet run

# Run with debugging
dotnet run --configuration Debug
```

## 🐛 Troubleshooting

### Common Issues
- **"Access Denied" errors**: Normal for system directories - tool automatically skips and continues
- **Slow scanning**: Large drives with many directories take time - watch progress bar
- **ZIP verification failures**: Corrupted archives are automatically skipped with warnings
- **No drives shown**: Only drives D: and above are displayed by design (excludes system drive C:)
- **Network drive issues**: Ensure proper network permissions and stable connection

### Getting Help
- Check the real-time log window for detailed error messages
- Enable dry run mode to safely test operations
- Review the issues section for known problems and solutions

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

### Development Guidelines
- Follow existing code style and patterns
- Add appropriate error handling and logging
- Test thoroughly with dry run mode
- Update documentation for new features

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
