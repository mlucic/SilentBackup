using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Xml.Serialization;
using static SilentBackupService.BackupOperation;

namespace SilentBackupService
{
    static class BackupServiceEntry
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			//Directory.SetCurrentDirectory(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%") + "\\SilentBackup\\SilentBackup"); // USE THIS FOR RELEASE	
				 
			try
			{
				ServiceBase[] ServicesToRun;
				ServicesToRun = new ServiceBase[] 
            { 
                new BackupService()
            };
				RunInteractiveServices(ServicesToRun); // USE THIS FOR TESTING
				//ServiceBase.Run(ServicesToRun);  // USE THIS FOR RELEASE
			}
			catch (Exception ex)
			{
				ReportIO.WriteStatement("Fatal service exception: " + ex.Message);
				return;
			}
		}

		/// <summary>
		/// Run services in interactive mode
		/// </summary>
		static void RunInteractiveServices(ServiceBase[] servicesToRun)
		{
            // Get the method to invoke on each service to start it
            MethodInfo onStartMethod = typeof(ServiceBase).GetMethod("OnStart", BindingFlags.Instance | BindingFlags.NonPublic);

			// Start services loop
			foreach (ServiceBase service in servicesToRun)
			{
				onStartMethod.Invoke(service, new object[] { new string[] { } });
			}

			Console.ReadLine();

			// Get the method to invoke on each service to stop it
			MethodInfo onStopMethod = typeof(ServiceBase).GetMethod("OnStop", BindingFlags.Instance | BindingFlags.NonPublic);

			// Stop loop
			foreach (ServiceBase service in servicesToRun)
			{
				onStopMethod.Invoke(service, null);
			}

			Console.ReadLine();
		}
	}
}
