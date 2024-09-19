using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace RestartAppsAfterReboot;

/// <summary>
/// 
/// The class AppsToRestart provides main functionality for restarting applications after a system reboot
/// 
/// Public methods:
///	- CreateList creates list of applications to restart
///	- WriteToRunOnce writes the applications to the Windows registry under the RunOnce key
/// - RegisterAll registers the applications one by one in the RestartManager
/// - RegisterProcess registers a single process  in the RestartManager
///
/// </summary>
public static class AppsToRestart
{
	// Define the necessary constants and structures
	private const int RmRebootReasonNone = 0;

	[StructLayout (LayoutKind.Sequential)]
	private struct RM_UNIQUE_PROCESS
	{
		public int dwProcessId;
		public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
	}

	[StructLayout (LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct RM_PROCESS_INFO
	{
		public RM_UNIQUE_PROCESS Process;
		[MarshalAs (UnmanagedType.ByValTStr, SizeConst = 256)]
		public string strAppName;
		[MarshalAs (UnmanagedType.ByValTStr, SizeConst = 64)]
		public string strServiceShortName;
		public RM_APP_TYPE ApplicationType;
		public uint AppStatus;
		public uint TSSessionId;
		[MarshalAs (UnmanagedType.Bool)]
		public bool bRestartable;
	}

	private enum RM_APP_TYPE
	{
		RmUnknownApp = 0,
		RmMainWindow = 1,
		RmOtherWindow = 2,
		RmService = 3,
		RmExplorer = 4,
		RmConsole = 5,
		RmCritical = 1000
	}

	// Define the necessary functions
	[DllImport ("rstrtmgr.dll", CharSet = CharSet.Unicode)]
	private static extern int RmStartSession (out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

	[DllImport ("rstrtmgr.dll")]
	private static extern int RmEndSession (uint pSessionHandle);

	[DllImport ("rstrtmgr.dll", CharSet = CharSet.Unicode)]
	private static extern int RmRegisterResources (uint pSessionHandle, uint nFiles, string[]? rgsFilenames, uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications, uint nServices, string[]? rgsServiceNames);

	/// <summary>
	/// Creates a list of applications to restart
	/// </summary>
	/// <param name="running">Currently running apllications</param>
	/// <param name="startup">Applications from startup sections</param>
	/// <returns>List of running applications that are not in startup</returns>
	static public List<App> CreateList (RunningApps running, StartupApps startup)
	{
		List<App> restart = new List<App> ();

		foreach (App app in running)
			if (!app.Path.StartsWith (@"C:\Windows\") && !app.IsContainedInStartups (startup))
				restart.Add (app);

		return restart;
	}

	/// <summary>
	/// Writes the apps to the Windows registry under the RunOnce key
	/// </summary>
	/// <param name="procs">List of applications</param>
	/// <returns>True if the list was written to the registry</returns>
	[SupportedOSPlatform ("windows")]
	public static bool WriteToRunOnce (List<App> procs)
	{
		var subKey = Registry.CurrentUser.OpenSubKey (@"Software\Microsoft\Windows\CurrentVersion\RunOnce", true);
		if (subKey == null)
			return false;

		foreach (var proc in procs)
			subKey.SetValue (proc.Name, proc.Path);

		return true;
	}

	/// <summary>
	/// Registers applications one by one in the RestartManager
	/// </summary>
	/// <param name="procs">List of applications</param>
	/// <returns>True if success</returns>
	[SupportedOSPlatform ("windows")]
	public static bool RegisterAll (List<App> procs)
	{
		// Activate the RestartApps feature
		var subKey = Registry.CurrentUser.OpenSubKey (@"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", true);
		if (subKey == null)
			return false;
		var isRestartOn = subKey.GetValue (@"RestartApps");
		if (isRestartOn == null || isRestartOn.ToString () != "1")
			try 
			{ 
				subKey.SetValue (@"RestartApps", 1);
			}
			finally
			{
				subKey.Close ();
			}

		// Start the RestartManager session
		uint sessionHandle;
		string sessionKey = Guid.NewGuid ().ToString ();
		int result = RmStartSession (out sessionHandle, 0, sessionKey);

		if (result != 0)
			return false;

		try
		{
			// Pass applications one by one to the RestartManager
			foreach (var proc in procs)
				if (! RegisterProcess (sessionHandle, proc.Process))
					return false;
		}
		finally
		{
			// Close the RestartManager session
			RmEndSession (sessionHandle);
		}
		return true;
	}

	/// <summary>
	/// Registers a single process in the RestartManager
	/// </summary>
	/// <param name="sessionHandle">Session handle</param>
	/// <param name="proc">Applications to register</param>
	/// <returns>True if success</returns>
	public static bool RegisterProcess (uint sessionHandle, Process proc)
	{
		RM_UNIQUE_PROCESS[] applications = new RM_UNIQUE_PROCESS[]
		{
			new RM_UNIQUE_PROCESS
			{
				dwProcessId = proc.Id,
				ProcessStartTime = GetProcessStartTime(proc)
			}
		};

		int result = RmRegisterResources (sessionHandle, 0, null, (uint) applications.Length, applications, 0, null);
		return result == 0;
	}

	private static System.Runtime.InteropServices.ComTypes.FILETIME GetProcessStartTime (Process process)
	{
		long fileTime = process.StartTime.ToFileTime ();
		return new System.Runtime.InteropServices.ComTypes.FILETIME
		{
			dwLowDateTime = (int) (fileTime & 0xFFFFFFFF),
			dwHighDateTime = (int) (fileTime >> 32)
		};
	}
}
