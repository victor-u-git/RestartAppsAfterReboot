using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace RestartAppsAfterReboot;

/// <summary>
/// Main form of the application.
/// </summary>
public partial class MainForm : Form
{
	private NotifyIcon trayIcon;
	private ContextMenuStrip trayMenu;

	const int WM_QUERYENDSESSION = 0x0011;
	const int WM_ENDSESSION = 0x0016;
	const int WM_CLOSE = 0x0010;

	[DllImport ("kernel32.dll", SetLastError = true)]
	static extern bool SetProcessShutdownParameters (uint dwLevel, uint dwFlags);

	[DllImport ("user32.dll", SetLastError = true)]
	static extern bool ShutdownBlockReasonCreate (IntPtr hWnd, [MarshalAs (UnmanagedType.LPWStr)] string reason);

	[DllImport ("user32.dll", SetLastError = true)]
	static extern bool ShutdownBlockReasonDestroy (IntPtr hWnd);

	[DllImport ("user32.dll", SetLastError = true)]
	static extern bool SendMessage (IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

	[SupportedOSPlatform ("windows")]
	public MainForm ()
	{
		InitializeComponent ();

		// Create a context menu for the tray icon
		trayMenu = new ContextMenuStrip ();
		//trayMenu.Items.Add ("Show", null, ShowApp);
		trayMenu.Items.Add ("Exit", null, ExitApp);

		// Create the tray icon
		trayIcon = new NotifyIcon ();
		trayIcon.Text = "RestartAppsAfterReboot";
		trayIcon.Icon = new Icon (SystemIcons.Application, 40, 40);

		// Add context menu to tray icon
		trayIcon.ContextMenuStrip = trayMenu;

		// Handle double-click event to restore the window
		//trayIcon.DoubleClick += new EventHandler (ShowApp);

		// Show the tray icon
		trayIcon.Visible = true;

		// Hide window at startup (set to minimized)
		WindowState = FormWindowState.Minimized;
		ShowInTaskbar = false; // Hide from taskbar initially
		Hide (); // Start hidden in system tray

		// Disable the system RestartManager otherwise some applications (e.g. Chrome) will start twice
		var subKey = Registry.CurrentUser.OpenSubKey (@"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", true);
		if (subKey != null)
			try
			{ 
				subKey.SetValue (@"RestartApps", 0);
			}
			catch(UnauthorizedAccessException)
			{
				MessageBox.Show ("You need to run this application as administrator", "RestartAppsAfterReboot", MessageBoxButtons.OK, MessageBoxIcon.Error);
				Load += (s, e) => Close ();
				return;
			}

		// It is necessary to receive the WM_QUERYENDSESSION message before other programs,
		// otherwise some of them will have time to close and
		// it will be impossible to create a full list of running applications.
		// To do this, set the highest priority of shutdown range.
		SetProcessShutdownParameters (0x4FF /* System first shutdown range */, 0);
	}

	// Show the form (for future use)
	private void ShowApp (object? sender, EventArgs e)
	{
		Show ();
		WindowState = FormWindowState.Normal;
		ShowInTaskbar = true;
	}

	private void ExitApp (object? sender, EventArgs e)
	{
		trayIcon.Visible = false; // Hide the tray icon
		Application.Exit (); 
	}

	protected override void OnResize (EventArgs e)
	{
		base.OnResize (e);
		if (WindowState == FormWindowState.Minimized)
		{
			Hide ();
			ShowInTaskbar = false;
		}
	}

	protected override void OnFormClosing (FormClosingEventArgs e)
	{
		trayIcon.Dispose ();
		base.OnFormClosing (e);
	}

	[SupportedOSPlatform ("windows")]
	void OnShutDownWindowsSession ()
	{
		RunningApps running = new RunningApps ();
		StartupApps startup = new StartupApps ();
		List<App> runOnce = AppsToRestart.CreateList (running, startup);
		AppsToRestart.WriteToRunOnce (runOnce);
		//AppsToRestart.RegisterAll (runOnce); // Reserved for future use
	}

	[SupportedOSPlatform ("windows")]
	protected override void WndProc (ref Message msg)
	{
		if (msg.Msg == WM_QUERYENDSESSION || msg.Msg == WM_ENDSESSION)
		{
			// Prevent Windows shutdown until the list of running apps is created
			ShutdownBlockReasonCreate (Handle, "Creating a list of running apps");

			// Creating a list of running apps
			OnShutDownWindowsSession ();

			// Allow Windows to shutdown
			ShutdownBlockReasonCreate (Handle, "Continue shutting down");
			ShutdownBlockReasonDestroy (Handle);

			// Close this window
			SendMessage (Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

			/* Saved for future to use with RestartManager
			// 
			ThreadPool.QueueUserWorkItem (obj =>
			{
				// Creating a list of running apps
				OnShutDown ();

				BeginInvoke (() =>
				{
					// Allow Windows to shutdown
					ShutdownBlockReasonCreate (Handle, "Continue shutting down");
					ShutdownBlockReasonDestroy (Handle);

					// Close this window
					SendMessage (Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
				});
			});
			*/

			return;
		}

		base.WndProc (ref msg);
	}
}