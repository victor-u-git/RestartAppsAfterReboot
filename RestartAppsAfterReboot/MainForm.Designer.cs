namespace RestartAppsAfterReboot;

partial class MainForm
{
	private System.ComponentModel.IContainer components = null;

	protected override void Dispose (bool disposing)
	{
		if (disposing && (components != null))
			components.Dispose ();
		base.Dispose (disposing);
	}

	private void InitializeComponent ()
	{
		SuspendLayout ();
		// 
		// MainForm
		// 
		ClientSize = new Size (756, 390);
		Name = "MainForm";
		StartPosition = FormStartPosition.CenterScreen;
		Text = "List of Apps to Restart";
		ResumeLayout (false);
	}
}