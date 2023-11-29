
using System;
using System.Collections.Concurrent;
using System.Collections.Generic; // For HashSet<>
using System.IO;
using System.Linq; // For LINQ methods like Any()
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


/*
File Version Manager
--------------------

Description:
This program monitors a specified directory for file changes and creates versioned copies of files. 
It is designed to work in tandem with companion Bash scripts for managing file versioning state and 
purging old file versions.

How It Is Used:
- The program monitors a target directory for changes to files.
- When a file change is detected, it checks against a set of criteria (file type, versioning pattern, etc.) to decide whether to create a versioned copy.
- The program respects a configurable environment setting (enabled/disabled) for file versioning, read from an external file.
- It also ensures that no version is created for a file if a version has been created within the last 5 minutes.
- Companion Bash scripts are used to manage the versioning state (enabled/disabled) and to purge old versioned files.

Technical Summary:

1. File Monitoring:
   - Uses `FileSystemWatcher` to monitor file changes in the specified directory.
   - Filters changes based on file extension, temporary file patterns, and a versioning pattern.

2. Environment File Checking:
   - A `Timer` regularly checks an external file to determine if file versioning is enabled or disabled.
   - This allows dynamic control of versioning activity without needing to restart the program.

3. Multitasking and Task Management:
   - When a file change event occurs, a new task (`Task`) is created to handle the versioning process.
   - `ConcurrentDictionary` named `fileTasks` tracks these tasks, ensuring that only one task per file is active at any given time.
   - This prevents duplicate versioning operations for the same file, particularly important for files that are temporarily locked or being frequently accessed.
   - Another `ConcurrentDictionary` named `lastVersionTimes` tracks the last versioning time for each file, enforcing a 5-minute cooldown period between version creations for any given file.
   - The `CleanupCompletedTasks` method periodically clears completed tasks from `fileTasks` to manage memory and resource usage efficiently.

4. File Versioning:
   - For eligible files, the program creates a versioned copy following a specific naming pattern (e.g., 'myfile~~~~123.txt').
   - Implements timeout logic to wait for file locks to be released before creating a version.

5. Error Handling and Logging:
   - Robust error handling and logging are in place to track the program's activity and any issues encountered during execution.

Usage Scenario:
This program is ideal for environments where tracking changes to files is crucial, and maintaining recent versions of files is necessary for backup, audit, or recovery purposes.

Companion Scripts:
- `purge-file-versions.sh`: Used to delete old versioned files based on age.
- `set-file-versioning.sh`: Used to enable or disable file versioning dynamically.

*/


// COMPANION SCRIPTS:
//
// 1. purge-file-versions.sh
//
// Usage:    /usr/bin/purge-file-versions <directory> [-h|--help] [days] [-d|--delete] [-a|--all]
// Example:  purge-file-versions /mnt/drive2/nextcloud/local-cache/Files -a -d
//
// Arguments:
//
//   < directory > : The directory path where the script will search for versioned files.
//   [days]      : Optional.Integer representing the age of the file in days. Defaults to 1 day.
//                 - ignored if -a|--all is provided.
//                 - a value of '0' will match files modified within the last 24 hours.
//   -d|--delete : Optional.If provided, the script will delete the files it finds.
//   -a|--all    : Optional.If provided, the script will target all versioned files,
//                 irrespective of their age.
//
// 2. set-file-versioning.sh
// 
// Example:
//           set-file-versioning.sh
//           > Current file versioning state: disabled
//
//           set-file-versioning enabled
//           > File versioning set to enabled
//
//           set-file-versioning disabled
//           > File versioning set to disabled
//


class FileVersionManager
{
    private Timer checkEnvVariableTimer;
    private bool versioningEnabled = false; // Start disabled
    private bool old_versioningEnabled = true; // set old status to opposite to force an initial log messae

    private FileSystemWatcher watcher;
    private string targetDirectory = "/mnt/drive2/nextcloud/local-cache/Files"; // Set your target directory
    private string logFile = "/var/log/create-file-versions.log"; // Set path to your log file
    private Timer cleanupTimer;

    private ConcurrentDictionary<string, DateTime> lastVersionTimes = new ConcurrentDictionary<string, DateTime>();
    private ConcurrentDictionary<string, Task> fileTasks = new ConcurrentDictionary<string, Task>();
    private HashSet<string> allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    // Microsoft Office
    ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
    ".mdb", ".accdb", ".pub", ".one", // Legacy Microsoft Office
    ".docm", ".dotx", ".dotm", ".xlsm", ".xltm", ".pptm", ".osts",
    
    // Microsoft OneNote
    ".one",  // OneNote Notebook File
    ".onetoc2", // OneNote Table of Contents File
    ".onepkg", // OneNote Package File

    // Microsoft Project
    ".mpp",  // Microsoft Project File
    ".mpt",  // Microsoft Project Template

    // Microsoft Visio
    ".vsd",  // Visio Drawing File (Legacy)
    ".vsdx", // Visio Drawing File
    ".vst",  // Visio Template File (Legacy)
    ".vstx", // Visio Template File
    ".vss",  // Visio Stencil File (Legacy)
    ".vssx", // Visio Stencil File
    ".vsw",  // Visio Workspace File (Legacy)

    // Microsoft Publisher
    ".pub",  // Publisher Document
    
    // Email Formats
    ".eml", ".msg", ".pst", ".ost", ".mbox",
    
    // Markdown, Code Files
    ".md", ".cs", ".sh", ".js", ".java", ".cpp", ".py", ".rb",
    ".php", ".html", ".htm", ".css", ".sln", ".csproj",
    
    // Configuration Files
    ".conf", // Configuration File
    ".config", // Configuration File
    ".ini",  // Initialization File
    ".yaml", // YAML File
    ".yml",  // YAML File (alternative extension)
    ".json", // JSON File
    ".xml",  // XML File
    
    // Image Types
    ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".psd", ".svg",
    
    // LibreOffice
    ".odt", ".ods", ".odp", ".odg", ".odf",
    
    // Others
    ".csv", ".txt", ".rtf",
    
    // Web Archives
    ".mhtml", ".mht", ".webarchive",

    // CorelDRAW
    ".cdr",  // CorelDRAW Drawing File
    ".cdt",  // CorelDRAW Template File
    ".cmx",  // Corel Presentation Exchange File
    ".csl",  // Corel Symbol Library
    ".cpt",  // Corel Photo-Paint Document
    ".clr",  // Corel Color Palette

    // Batch Files
    ".bat",

    // Adobe Products
    ".pdf",  // Adobe PDF (already included)
    ".psd",  // Photoshop Document
    ".ai",   // Adobe Illustrator Document
    ".indd", // Adobe InDesign Document
    ".aep",  // Adobe After Effects Project
    ".prel", // Adobe Premiere Elements Project File
    ".prproj", // Adobe Premiere Pro Project
    ".ae",   // Adobe After Effects Script
    ".fla",  // Adobe Animate Animation
    ".swf",  // Adobe Flash SWF

    // Sony Vegas
    ".veg",  // Vegas Project File
    ".vf",   // Vegas Movie Studio Project File

    // Video File Formats
    ".mkv",  // Matroska Video File
    ".mp4",  // MPEG-4 Video File
    ".avi",  // Audio Video Interleave File
    ".mov",  // Apple QuickTime Movie
    ".wmv",  // Windows Media Video
    ".flv",  // Flash Video
    ".mpeg", // MPEG Movie
    ".mpg",  // MPEG Video File
    ".m4v",  // iTunes Video File
    ".svi",  // Samsung Video File
    ".3gp",  // 3GPP Multimedia File
    ".m2ts", // MPEG-2 Transport Stream
    ".mts",  // AVCHD Video File
    ".vob",  // DVD Video Object File
    ".webm", // WebM Video File

    // draw.io
    ".drawio", // draw.io File
};

    public FileVersionManager()
    {
        watcher = new FileSystemWatcher(targetDirectory)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            Filter = "*.*",
            IncludeSubdirectories = true // Enable recursive monitoring
        };

        watcher.Changed += OnChanged;
        watcher.EnableRaisingEvents = true;
        LogAndConsole($"Monitoring started on: {targetDirectory}");
        checkEnvVariableTimer = new Timer(CheckVersioningStatus, null, TimeSpan.Zero, TimeSpan.FromMinutes(1)); // Check every minute
        cleanupTimer = new Timer(_ => CleanupCompletedTasks(), null, TimeSpan.Zero, TimeSpan.FromMinutes(10)); // Every 10 minutes
    }

    private void OnChanged(object source, FileSystemEventArgs e)
    {
        LogAndConsole($"Change detected: {e.FullPath}");

        string fileName = Path.GetFileName(e.FullPath);
        if (IsTemporaryFile(fileName) || !allowedExtensions.Contains(Path.GetExtension(fileName)) || IsInDotDirectory(e.FullPath))
        {
            // LogAndConsole($"Ignoring temporary or versioned file: {e.Name}");
            return;
        }

        CleanupCompletedTasks();

        if (!fileTasks.ContainsKey(e.FullPath))
        {
            var task = ProcessFileChangeAsync(e.FullPath);
            if (fileTasks.TryAdd(e.FullPath, task))
            {
                // If successfully added, await the task's completion and then remove it from the dictionary
                task.ContinueWith(_ => fileTasks.TryRemove(e.FullPath, out _));
            }
        }
    }
    private bool IsTemporaryFile(string fileName)
    {
        // Check for standard temporary file patterns
        if (fileName.StartsWith("~$") || fileName.StartsWith(".~"))
        {
            return true;
        }

        // Regular expression to match the pattern '~~~~123' before the file extension
        var versionPattern = new Regex(@"~~~~\d{3}(?=\.[^\.]+$)");
        return versionPattern.IsMatch(fileName);
    }
    private void CleanupCompletedTasks()
    {
        foreach (var key in fileTasks.Keys)
        {
            if (fileTasks[key].IsCompleted)
            {
                fileTasks.TryRemove(key, out _);
            }
        }
    }
    private void CheckVersioningStatus(object state)
    {
        try
        {
            string statusFilePath = "/etc/default/file-versioning-state.txt"; // Path to the status file
            if (File.Exists(statusFilePath))
            {
                string status = File.ReadAllText(statusFilePath).Trim();
                versioningEnabled = (status.ToLower() == "enabled");
                if (versioningEnabled != old_versioningEnabled)
                {
                    LogAndConsole($"File versioning is now {(versioningEnabled ? "enabled" : "disabled")}");
                    old_versioningEnabled = versioningEnabled;
                }
            }
            else
            {
                // turn off versioning if the file does not exist
                old_versioningEnabled = versioningEnabled;
                versioningEnabled = false;
                LogAndConsole($"Status file is missing, so versioning is disabled. Please create this file containing either 'enabled' or 'disabled' in it: {statusFilePath}");
            }
        }
        catch (Exception ex)
        {
            // turn off versioning if the file can't be read
            old_versioningEnabled = versioningEnabled;
            versioningEnabled = false;
            LogAndConsole($"Error reading status file so versioning is disabled. The exception is: {ex.Message}");
        }
    }
    private bool IsInDotDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);

        // Check if any part of the directory path starts with a dot
        return directory.Split(Path.DirectorySeparatorChar).Any(dir => dir.StartsWith("."));
    }
    private bool ShouldFileVersion(string filePath)
    {
        if (lastVersionTimes.TryGetValue(filePath, out DateTime lastVersionTime))
        {
            return (DateTime.Now - lastVersionTime).TotalMinutes >= 2;
        }
        return true;
    }
    private async Task ProcessFileChangeAsync(string filePath)
    {
        try
        {
            if (!ShouldFileVersion(filePath))
            {
                LogAndConsole($"Skipping versioning for {filePath} as a version was created recently.");
                return;
            }

            string versionedFilePath = CreateVersionedFilePath(filePath);
            if (string.IsNullOrEmpty(versionedFilePath)) return;

            if (await TryCopyFileWithTimeoutAsync(filePath, versionedFilePath, TimeSpan.FromMinutes(20)))
            {
                LogAndConsole($"Versioned file created: {versionedFilePath}");
                lastVersionTimes[filePath] = DateTime.Now;
            }
            else
            {
                LogAndConsole($"Failed to create versioned file for: {filePath}");
            }
        }
        catch (Exception ex)
        {
            LogAndConsole($"Error: {ex.Message}");
        }
    }
    private string CreateVersionedFilePath(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        int versionNumber = 1;
        string versionedFilePath;
        do
        {
            versionedFilePath = Path.Combine(directory, $".{fileName}~~~~{versionNumber:D3}{extension}");
            versionNumber++;
        } while (File.Exists(versionedFilePath));

        return versionedFilePath;
    }
    private async Task<bool> TryCopyFileWithTimeoutAsync(string sourcePath, string destinationPath, TimeSpan timeout)
    {
        using (var cts = new CancellationTokenSource(timeout))
        {
            try
            {
                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using (var destinationStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        await sourceStream.CopyToAsync(destinationStream, cts.Token);
                    }
                }

                // Setting permissions and ownership using Bash commands
                SetPermissionsAndOwnership(sourcePath, destinationPath);

                return true;
            }
            catch (OperationCanceledException)
            {
                LogAndConsole($"Timeout occurred while copying: {sourcePath}");
                return false;
            }
            catch (Exception ex)
            {
                LogAndConsole($"Exception during file copy: {ex.Message}");
                return false;
            }
        }
    }
    private string EscapeForBash(string path)
    {
        // string escapedPath = '\u0022' + Regex.Replace(path, "([ \"])", @"\\$1") + '\u0022'; 
        string escapedPath = '\u0027' + path + '\u0027';
        return (escapedPath);
    }
    private void SetPermissionsAndOwnership(string sourcePath, string destinationPath)
    {
        var escapedSourcePath = EscapeForBash(sourcePath);
        var escapedDestinationPath = EscapeForBash(destinationPath);
        // LogAndConsole($"Escaped Source Path:               {escapedSourcePath}");
        // LogAndConsole($"Escaped Destination Path:          {escapedDestinationPath}");

        // Getting permissions from the source file
        var getPermsCommand = $"stat -c %a {escapedSourcePath}";
        // LogAndConsole($"Perms command for Source:          {getPermsCommand}");
        var permissions = ExecuteBashCommand(getPermsCommand);
        // LogAndConsole($" --- Returned Perms:               {permissions}");

        // Setting permissions for the destination file
        var setPermsCommand = $"chmod {permissions} {escapedDestinationPath}";
        // LogAndConsole($"Set Perms command for Destination: {getPermsCommand}");
        ExecuteBashCommand(setPermsCommand);

        // Getting ownership from the source file
        var getOwnerCommand = $"stat -c %U {escapedSourcePath}";
        // LogAndConsole($"Owner command for Source:          {getOwnerCommand}");
        var owner = ExecuteBashCommand(getOwnerCommand);
        // LogAndConsole($" --- Returned Owner:               {owner}");

        var getGroupCommand = $"stat -c %G {escapedSourcePath}";
        // LogAndConsole($"Group command for Source:          {getGroupCommand}");
        var group = ExecuteBashCommand(getGroupCommand);
        // LogAndConsole($" --- Returned Group:               {group}");

        // Setting ownership for the destination file
        var setOwnerCommand = $"chown {owner}:{group} {escapedDestinationPath}";
        // LogAndConsole($"Set Owner command for Destination  {getGroupCommand}");
        ExecuteBashCommand(setOwnerCommand);
    }
    private string ExecuteBashCommand(string command)
    {
        using (var process = new System.Diagnostics.Process())
        {
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return result.Trim();
        }
    }
    private void LogAndConsole(string message)
    {
        string logMessage = $"{DateTime.Now}: {message}\n";
        Console.WriteLine(logMessage);
        File.AppendAllText(logFile, logMessage);
    }
    static void Main()
    {
        var fileVersionManager = new FileVersionManager();
        Console.WriteLine("Press Ctrl+C to exit.");

        // Option 1: Simple infinite loop
        while (true)
        {
            Thread.Sleep(Timeout.Infinite);
        }
    }
}