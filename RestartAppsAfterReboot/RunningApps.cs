using System.Diagnostics;

namespace RestartAppsAfterReboot;

/// <summary>
/// Type App
/// Contains the name, path, and process of an application
/// </summary>
public struct App
{
	public string Name;
	public string Path;
	public Process Process; // for future use with RestartManager

	public App (string name, string path, Process process)
	{
		Name = name;
		Path = path;
		Process = process;
	}

	public bool IsContainedInStartups (List<string> started)
	{
		foreach (string s in started)
		{
			if (Path.StartsWith (s))
				return true;
		}
		return false;
	}
}

/// <summary>
/// 
/// Class for currently running applications
/// 
/// Public methods:
/// RunningApps -- constructor
/// 
/// </summary>
public class RunningApps : List<App>
{
	public RunningApps () : base ()
	{
		Process[] processes = Process.GetProcesses ();

		foreach (Process process in processes)
		{
			try
			{
				if (process.MainWindowHandle != IntPtr.Zero && 
					process.MainModule != null && 
					process.MainModule.FileName != null)
					Add (new App (process.ProcessName, process.MainModule.FileName, process));
			}
			catch
			{
				continue;
			}
		}
	}
}
