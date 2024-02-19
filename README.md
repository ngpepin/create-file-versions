# Simple File Versioning for Linux (Ext4)

## Description
This program monitors a specified directory for file changes and creates versioned copies of files. 
It's designed to work in tandem with companion Bash scripts to manage file versioning state and purge old file versions.

## How It Is Used
- Monitors a target directory for changes to files.
- Creates a versioned copy of a file when a change is detected, based on a set of criteria.
- Respects a configurable environment setting for file versioning, read from an external file.
- Prevents creating a version for a file if a version has been created within the last 5 minutes.
- Uses companion Bash scripts to manage the versioning state and purge old versioned files.

## Technical Summary

### 1. File Monitoring
- Utilizes `FileSystemWatcher` to monitor file changes.
- Filters changes based on file extension, temporary file patterns, and a versioning pattern.

### 2. Environment File Checking
- Regularly checks an external file to determine if file versioning is enabled or disabled.

### 3. Multitasking and Task Management
- Manages versioning processes using `Task` and `ConcurrentDictionary`.
- Tracks the last versioning time for each file to enforce a cooldown period.

### 4. File Versioning
- Creates versioned copies following a specific naming pattern.

### 5. Error Handling and Logging
- Implements robust error handling and logging for tracking activities and issues.

## Usage Scenario
Ideal for environments where tracking changes to files is essential, and maintaining recent versions of files is necessary for backup, audit, or recovery purposes.

## Exclusion File Usage

### Overview

The `FileVersionManager` program supports the use of an external file, `exclusions.txt`, for defining regex patterns to exclude certain files and directories from versioning. This allows for flexible and dynamic control over which files are monitored for changes.

### Location

The exclusions file is located in a specific directory:

```
/home/npepin/.create-file-versions/exclusions.txt
```

When setting up the file path in the program, use the full path instead of shorthand notations like `~/create-file-versions`. This is important, especially when the application is running as `root` under systemd, as the root user's home directory (`/root`) is different from that of a regular user.

### Format

Each line in the `exclusions.txt` file represents a regex pattern that defines a set of files or directories to be excluded. The program reads these patterns and ignores any file or directory that matches them.

### Examples

1. **Ignore Specific Directories**:

   ```
   ^/mnt/mydrive/nextcloud/files/Junk/.*
   ```

   This pattern excludes any changes within the `/mnt/mydrive/nextcloud/files/Junk` directory.

2. **Ignore Specific File Extensions in a Directory**:

   ```
   ^/mnt/mydrive/Finances/.+\.(xlsx|xlsm)$
   ```

   This pattern will ignore `.xlsx` and `.xlsm` files within the `Finances` directory.

3. **Ignore Backup Directories**:

   ```
   .*/[^/]*_BAK/$
   ```

   This pattern excludes directories ending with `_BAK`.

4. **Complex Path Exclusions**:

   ```
   this is a directory/_____New/Apps/.*
   ```

   This pattern excludes any file or directory under `this is a directory/_____New/Apps`.

### Notes

* Ensure the regex patterns accurately reflect the paths as they are represented in the program.
* Test the patterns to confirm they are working as expected, especially if the file paths are complex.

## Companion Scripts

### 1. `purge-file-versions.sh`
- **Usage**: `/usr/bin/purge-file-versions <directory> [options]`
- **Example**: `purge-file-versions /mnt/myvolume/files -a -d`
- **Options**:
  - `[days]`: Age of the file in days. Default is 1 day.
  - `-d|--delete`: If provided, deletes the files found.
  - `-a|--all`: Targets all versioned files, irrespective of their age.

### 2. `set-file-versioning.sh`
- **Example**:
  - `set-file-versioning.sh` (Displays current state)
  - `set-file-versioning enabled` (Enables file versioning)
  - `set-file-versioning disabled` (Disables file versioning)

## Author
[Your Name]

## Version
1.0

## License
[Specify License Here]
