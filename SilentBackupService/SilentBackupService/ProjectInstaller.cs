using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.ServiceProcess;

namespace SilentBackupService
{
    [RunInstaller(true)]
	public partial class ProjectInstaller : System.Configuration.Install.Installer
	{
		public ProjectInstaller()
		{
			InitializeComponent();
			this.serviceProcessInstaller1.BeforeUninstall += new System.Configuration.Install.InstallEventHandler(serviceProcessInstaller1_BeforeUninstall);
		}


		void serviceProcessInstaller1_BeforeUninstall(object sender, InstallEventArgs e)
		{
			try
			{
				using (ServiceController sc = new ServiceController(serviceInstaller1.ServiceName))
				{
					sc.Stop();
					sc.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 0, 15));
				}
				System.Diagnostics.Process process = new System.Diagnostics.Process();
				System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
				startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
				startInfo.FileName = "cmd.exe";
				startInfo.Arguments = "sc delete BackupService";
				process.StartInfo = startInfo;
				process.Start();
				//This does not work as the folder and files are locked until the MSI has finished the uninstall
				if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%") + "\\SilentBackup"))
				{
					Directory.Delete(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%") + "\\SilentBackup", true);
				}
			}
			catch (Exception ex)
			{
				//ReportIO.WriteStatement("", ex.Message);
			}
		}

		//protected override void OnAfterUninstall(IDictionary savedState)
		//{
		//	base.OnAfterUninstall(savedState);

		//	string targetDir = Context.Parameters["TargetDir"]; // Must be passed in as a parameter

		//	if (targetDir.EndsWith("|"))
		//		targetDir = targetDir.Substring(0, targetDir.Length - 1);

		//	if (!targetDir.EndsWith("\\"))
		//		targetDir += "\\";

		//	if (!Directory.Exists(targetDir))
		//	{
		//		//Debug.WriteLine("Target dir does not exist: " + targetDir);
		//		return;
		//	}

		//	string[] files = new[] { "File1.txt", "File2.tmp", "File3.doc" };
		//	string[] dirs = new[] { "Logs", "Temp" };

		//	foreach (string f in files)
		//	{
		//		string path = System.IO.Path.Combine(targetDir, f);

		//		if (File.Exists(path))
		//			File.Delete(path);
		//	}

		//	foreach (string d in dirs)
		//	{
		//		string path = System.IO.Path.Combine(targetDir, d);

		//		if (Directory.Exists(d))
		//			Directory.Delete(d, true);
		//	}

		//	// At this point, all generated files and directories must be deleted.
		//	// The installation folder will be removed automatically.
		//}

		private void serviceInstaller1_AfterInstall(object sender, InstallEventArgs e)
		{
			using (ServiceController sc = new ServiceController(serviceInstaller1.ServiceName))
			{
				sc.Start();
				sc.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 15));
			}
		}

		private void serviceProcessInstaller1_AfterInstall(object sender, InstallEventArgs e)
		{

		}

		public string GetContextParameter(string key)
		{
			string sValue = "";
			try
			{
				sValue = this.Context.Parameters[key].ToString();
			}
			catch
			{
				sValue = "";
			}
			return sValue;
		}
	}
}
