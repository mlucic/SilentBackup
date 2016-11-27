using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Threading;

namespace SilentBackupService
{
	public partial class BackupService : ServiceBase
	{
		private BackupOperationManager BOM;
		private NamedPipeServerStream IPCServer;
		private BackgroundWorker bw;

		public BackupService()
		{
			InitializeComponent();
			bw = new BackgroundWorker();
			bw.DoWork += Run;
			bw.WorkerSupportsCancellation = true;
		}

		protected override void OnStart(string[] args)
		{
			bw.RunWorkerAsync();
		}

		private void Run(object sender, DoWorkEventArgs ev)
		{
			BOM = BackupOperationManager.Instance;
            BOM.Initialize();
			PipeSecurity ps = new PipeSecurity();
			ps.AddAccessRule(new PipeAccessRule("CREATOR OWNER", PipeAccessRights.FullControl, AccessControlType.Allow));
			ps.AddAccessRule(new PipeAccessRule("USERS", PipeAccessRights.FullControl, AccessControlType.Allow));
			ps.AddAccessRule(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));
			while (true)
			{
				BOM.RunEventHandlers();

				IPCServer = new NamedPipeServerStream("SilentBackupIPCServer", PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 64, 0, ps);
				do
				{
					try
					{
						var ar = IPCServer.BeginWaitForConnection(null, null);
						if (ar.AsyncWaitHandle.WaitOne(5000))
						{
							IPCServer.EndWaitForConnection(ar);
						}

						if ((sender as BackgroundWorker).CancellationPending == true)
						{
							ev.Cancel = true;
							IPCServer.Dispose();
							BOM.Dispose();
							return;
						}
					}
					catch (Exception e)
					{
                        
					}
					
				} while (IPCServer.IsConnected == false);

				StreamReader reader = new StreamReader(IPCServer);
				try
				{
					var line = reader.ReadLine();
					if (line == "Reload")
					{
						DebugIO.WriteStatement("IPCServer thread", "Reloading config");
						BOM.Reload();
					}
				}
				catch (Exception e)
				{
					ReportIO.WriteStatement(e.Message);
				}
				reader.Close();

				IPCServer.Dispose();
				IPCServer = null;
			}
		}

		protected override void OnStop()
		{
			if (bw != null)
			{
				bw.CancelAsync();
				while (bw.IsBusy)
				{
					Thread.Sleep(1000);
				}
				bw.Dispose();
			}
			
			if(BOM != null)
				BOM.Dispose();

			if (IPCServer != null)
				IPCServer.Dispose();
		}
	}
}
