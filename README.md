# ROM CleanZipper

A Windows utility for managing ROM file collections by identifying and cleaning up duplicate files and compressing orphaned files.

## Overview

ROM CleanZipper scans selected drives to find:
- **Duplicate ROM files**: Files that exist in both compressed (.zip) and uncompressed formats
- **Orphaned ROM files**: Uncompressed files that don't have a corresponding .zip archive

The tool helps maintain an organized ROM collection by removing redundant files and ensuring all ROMs are properly archived.

## Features

### Core Functionality
- **Duplicate Detection**: Identifies files that exist in both zipped and unzipped formats
- **Orphan Detection**: Finds uncompressed files without corresponding zip archives
- **Batch Processing**: Process multiple files at once with a single click
- **Dry Run Mode**: Preview what will be changed before committing to any file operations
- **Selective Operations**: Choose to delete duplicates, compress orphans, or both

### User Interface
- **Single Unified List**: All detected files displayed in one sortable list with columns:
  - Name: The filename
  - Size: File size in MB
  - File Type: "Orphan" or "Zipped Version Exists"
  - Path: Directory location
- **Resizable Window**: Fully resizable and maximizable interface
- **Collapsible Console Log**: Click the log header to expand/collapse the console output
- **Drive Selector**: Dropdown list of available drives (D: and higher)
- **Refresh Button**: Update the drive list without restarting the application
- **Select All Button**: Quickly select/deselect all items in the list

### Safety Features
- **Dry Run Mode** (enabled by default): Shows what would be done without making changes
- **Double Confirmation**: Requires two confirmations before performing destructive operations
- **Verification**: Verifies zip contents match original files before deletion
- **Detailed Logging**: Complete operation log with timestamps

## System Requirements

- Windows 7 or later
- .NET Framework 4.5 or higher
- Administrator privileges (recommended for accessing all directories)

## Installation

1. Download the latest release of ROM CleanZipper
2. Extract to your preferred location
3. Run `ROMCleanZipper.exe`

## Usage

### Basic Operation

1. **Select a Drive**: Choose the drive to scan from the dropdown menu
2. **Configure Options**: 
   - ✓ Delete Duplicates: Remove files that already exist in zip archives
   - ✓ Compress Orphans: Create zip archives for uncompressed files
   - ✓ Dry Run Mode: Preview changes without modifying files
3. **Click Scan**: Starts scanning the selected drive
4. **Review Results**: Check/uncheck files you want to process
5. **Click "Delete Duplicates and Zip Orphans"**: Process selected files

### Understanding File Types

- **"Zipped Version Exists"**: The file exists in both compressed and uncompressed formats. The uncompressed version can be safely deleted.
- **"Orphan"**: The file only exists in uncompressed format. It will be compressed into a zip archive.

### Operation Details

#### Duplicate Deletion
- Verifies the file exists inside the zip with matching size
- Deletes only the uncompressed version
- Keeps the zip archive intact

#### Orphan Compression
- Creates a new zip archive with the same base name
- Verifies successful compression
- Deletes the original uncompressed file only after verification
- If compression fails, the original file is preserved

## Technical Details

### Scanning Logic
- Recursively scans all accessible directories
- Skips system and hidden directories
- Groups files by base name (filename without extension)
- Compares file sizes to verify true duplicates

### File Matching
- Case-insensitive filename comparison
- Matches files with identical base names (e.g., "Game.rom" matches "Game.zip")
- Verifies file size within zip matches uncompressed file size

### Performance
- Asynchronous scanning with progress reporting
- Batch UI updates for better performance with large file sets
- Cancellable operations

## Safety Considerations

1. **Always start with Dry Run Mode** to preview changes
2. **Backup important files** before running in live mode
3. **Review the file list** before processing
4. The tool permanently deletes files - this cannot be undone

## Troubleshooting

### "Access Denied" Messages
- Run as Administrator for full directory access
- Some system directories will always be skipped

### Missing Files in Results
- Hidden files are not processed
- System files are skipped
- Ensure the files aren't already in zip format

### Compression Failures
- Ensure sufficient disk space for temporary zip creation
- Check file permissions
- Very large files may take time to compress

## Development

Built with:
- C# / .NET Framework
- Windows Forms
- System.IO.Compression for zip operations

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Changelog

### Version 2.0
- Renamed from "Duplicate ROM CleanZipper" to "ROM CleanZipper"
- Redesigned UI with single unified file list
- Added resizable/maximizable window support
- Added collapsible console log
- Added refresh button for drive list
- Improved layout system with proper docking
- Added column-based file display with sortable headers

### Version 1.0
- Initial release with basic duplicate detection and orphan compression
