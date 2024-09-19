using Microsoft.Win32;
using Shell32;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using TaskScheduler;

namespace RestartAppsAfterReboot;

/// <summary>
/// 
/// Class for collecting applications that start automatically after signing in
/// 
/// Public methods:
/// StartupApps -- constructor
/// </summary>
public class StartupApps : List<string>
{
	[SupportedOSPlatform ("windows")]
	public StartupApps () : base ()
	{
		// Get startup programs from registry
		GetStartupAppsFromRegistry (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
		GetStartupAppsFromRegistry (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
		GetStartupAppsFromRegistry (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run");

		// Get startup programs from startup folders
		GetAppsFromStartupFolder (Environment.GetFolderPath (Environment.SpecialFolder.Startup));
		GetAppsFromStartupFolder (Environment.GetFolderPath (Environment.SpecialFolder.CommonStartup));

		// Get scheduled tasks
		GetStartupAppsFromScheduledTasks ();
	}

	/// <summary>
	/// Add a path to the list
	/// </summary>
	/// <param name="path"></param>
	void AddPath (string path)
	{
		if (!string.IsNullOrEmpty(path))
			Add (path);
	}

	/// <summary>
	/// Reads apps listed in the registry
	/// </summary>
	/// <param name="rootKey">CurrentUser or LocalMachine</param>
	/// <param name="subKey">Registry path</param>
	[SupportedOSPlatform ("windows")]
	void GetStartupAppsFromRegistry (RegistryKey rootKey, string subKey)
	{
		using RegistryKey? key = rootKey.OpenSubKey (subKey);
		if (key != null)
			foreach (string valueName in key.GetValueNames ())
			{
				var val = key.GetValue (valueName);
				if (val != null)
				{
					string programPath = ExtractApplicationPath (val.ToString ()!);
					AddPath (programPath);
				}
			}
	}

	/// <summary>
	/// Extracts a file path excluding parameters, quotes, etc.
	/// </summary>
	/// <param name="rawValue">String to extract from</param>
	/// <returns>File path</returns>
	static string ExtractApplicationPath (string rawValue)
	{
		if (rawValue == null)
			return string.Empty;

		// Regular expression pattern for Windows file paths
		string pattern = @"[a-zA-Z]:\\(?:[^\/:*?""<>|]+\\)*[^\/:*?""<>| ]+(\s[^\/:*?""<>|]+)*(?=\s[\/\-])?";

		// Extract matches
		MatchCollection matches = Regex.Matches (rawValue, pattern);

		if (matches.Count == 0)
			return string.Empty;
		string path = matches[0].Value;

		return path;
	}

	/// <summary>
	/// Reads apps from a folder
	/// </summary>
	/// <param name="folderPath">Startup folder</param>
	void GetAppsFromStartupFolder (string folderPath)
	{
		if (Directory.Exists (folderPath))
			foreach (var filePath in Directory.GetFiles (folderPath))
			{
				if (Path.GetExtension (filePath).ToLower () == ".ini")
					continue;

				// Extracting the file path from the command line
				string path = ExtractApplicationPath (filePath);

				// Find target file in a shortcut
				if (Path.GetExtension (filePath).ToLower () == ".lnk")
					path = GetShortcutTargetFile (path);

				AddPath (path);
			}
	}

	/// <summary>
	/// Starts scanning scheduled tasks
	/// </summary>
	[SupportedOSPlatform ("windows")]
	void GetStartupAppsFromScheduledTasks ()
	{
		try
		{
			TaskScheduler.TaskScheduler scheduler = new TaskScheduler.TaskScheduler ();
			scheduler.Connect (null, null, null, null);

			ITaskFolder rootFolder = scheduler.GetFolder ("\\");
			GetTasksFromFolder (rootFolder);
		}
		finally
		{
		}
	}

	/// <summary>
	/// Recursively scans folders for tasks
	/// </summary>
	/// <param name="folder"></param>
	[SupportedOSPlatform ("windows")]
	void GetTasksFromFolder (ITaskFolder folder)
	{
		IRegisteredTaskCollection tasks = folder.GetTasks (0);

		foreach (IRegisteredTask task in tasks)
			foreach (ITrigger trigger in task.Definition.Triggers)
				// Check if the task is triggered at logon
				if (trigger.Type == _TASK_TRIGGER_TYPE2.TASK_TRIGGER_LOGON)
					foreach (IAction action in task.Definition.Actions)
						if (action is IExecAction execAction)
							AddPath (ExtractApplicationPath (execAction.Path));

		// Recursively check subfolders
		ITaskFolderCollection subFolders = folder.GetFolders (0);
		foreach (ITaskFolder subFolder in subFolders)
			GetTasksFromFolder (subFolder);
	}

	/// <summary>
	/// Retrieves the target file from a shortcut
	/// </summary>
	/// <param name="shortcutFilename">Shortcut</param>
	/// <returns>Target filename</returns>
	static string GetShortcutTargetFile (string shortcutFilename)
	{
		try
		{
			var shell = new Shell ();
			string? directory = Path.GetDirectoryName (shortcutFilename);
			string fileName = Path.GetFileName (shortcutFilename);

			Folder folder = shell.NameSpace (directory);
			FolderItem folderItem = folder.ParseName (fileName);

			if (folderItem != null)
			{
				ShellLinkObject link = (ShellLinkObject) folderItem.GetLink;
				return link.Path;
			}
		}
		catch
		{
			return Path.GetFileName (shortcutFilename);
		}
		return string.Empty;
	}
}
