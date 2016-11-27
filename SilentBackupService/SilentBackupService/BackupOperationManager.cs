using Newtonsoft.Json;
using SilentBackupService.FileTransfer;
using SilentBackupService.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace SilentBackupService
{
    /// <summary>
    /// Manages the creation and loading of event handlers for
    /// performing backup operations
    /// </summary>
    internal class BackupOperationManager : IDisposable
    {
        /// <summary>
        /// Singleton instance object
        /// </summary>
		private static BackupOperationManager instance;
        /// <summary>
        /// Constructor for BackupOperationManager made private to disable it's use
        /// </summary>
		private BackupOperationManager()
        {
        }
        /// <summary>
        /// Property providing singleton access
        /// </summary>
		public static BackupOperationManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new BackupOperationManager();
                }
                return instance;
            }
        }
        private Configuration Config;
        private USBInsertionEventHandler USBHandler;
        private DateTimeEventHandler DTHandler;
        private LogonEventHandler LogonHandler;
        private BackgroundWorker USBWorker;
        private BackgroundWorker DTWorker;

        public void Initialize()
        {
            if (Config != null)
                throw new Exception("BackupOperationManager is already initialized.");

            Config = new Configuration();
            USBHandler = new USBInsertionEventHandler();
            DTHandler = new DateTimeEventHandler();
            LogonHandler = new LogonEventHandler();
            USBWorker = new BackgroundWorker();
            USBWorker.WorkerSupportsCancellation = true;
            USBWorker.DoWork += RunUSBHandler;
            DTWorker = new BackgroundWorker();
            DTWorker.WorkerSupportsCancellation = true;
            DTWorker.DoWork += RunDateTimeHandler;
            try
            {
                LoadConfig();
                foreach (var trigger in Config.Triggers.Where(ev => ev.Enabled))
                {
                    List<Callback> callBacks = new List<Callback>();
                    foreach (var backop in Config.BackupOperations.Where(x => x.Triggers.Contains(trigger.Id)).Where(x => x.Enabled).ToList())
                    {
                        foreach (var dest in backop.Destinations)
                        {
                            callBacks.Add(new Callback(() =>
                            {
                                // Save original path
                                string ogname = dest.Path.AbsolutePath;
                                // Label the destination. 
                                // It is important that the original be labeled and not the copy
                                // because we modify the destination information and save the configuration. 
                                // If we modified a copy, we would lose those modifications and end up with 
                                // adverse effects.
                                Labeler.Label(dest.Path, dest.Label);
                                // Make a copy of the destination
                                var labeledCopy = new Path(dest.Path);
                                // Reset the original path because we only need the label for the copy
                                dest.Path.AbsolutePath = ogname;

                                FileTransferManager.Copy(new Path(backop.Source), labeledCopy, backop.CopySubDirs, true);
                            }));
                        }
                    }

                    var triggerType = trigger.GetType();

                    // Send event to appropriate handler based on the event type
                    if (triggerType == typeof(USBInsertionEvent))
                    {
                        USBHandler.AddEvent(trigger, callBacks.ToList());
                    }
                    else if (triggerType == typeof(DateTimeEvent))
                    {
                        DTHandler.AddEvent(trigger, callBacks.ToList());
                    }
                    else if (triggerType == typeof(LogonEvent))
                    {
                        LogonHandler.AddEvent(trigger, callBacks.ToList());
                    }
                    else
                    {
                        ReportIO.WriteStatement("Invalid trigger type defined in configuration.");
                    }
                }
            }
            catch (Exception e)
            {
                ReportIO.WriteStatement(e.Message);
                Reload();
            }
        }

        public void RunEventHandlers()
        {
            DTWorker?.RunWorkerAsync();

            USBWorker?.RunWorkerAsync();

            LogonHandler.Run();
        }

        private void RunUSBHandler(object sender, DoWorkEventArgs ev)
        {
            try
            {
                USBHandler.Run(sender, ev);
            }
            catch (Exception ex)
            {
                ReportIO.WriteStatement(ex.Message);
            }
        }

        private void RunDateTimeHandler(object sender, DoWorkEventArgs ev)
        {
            try
            {
                DTHandler.Run(sender, ev);
            }
            catch (Exception ex)
            {
                ReportIO.WriteStatement(ex.Message);
            }
        }

        public void Reload()
        {
            Dispose();
            Initialize();
        }

        public void Dispose()
        {
            if (USBWorker != null)
            {
                USBWorker.CancelAsync();
                while (USBWorker.IsBusy == true)
                {
                    new ManualResetEvent(false).WaitOne(1000);
                }
                USBWorker.Dispose();
                USBWorker = null;
            }

            if (DTWorker != null)
            {
                DTWorker.CancelAsync();
                while (DTWorker.IsBusy == true)
                {
                    new ManualResetEvent(false).WaitOne(1000);
                }
                DTWorker.Dispose();
                DTWorker = null;
            }

            Config = null;
            USBHandler.Dispose();
            DTHandler.Dispose();
            LogonHandler.Dispose();
        }

        public void SaveConfig()
        {
            string configJson = JsonConvert.SerializeObject(Config, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple
            });

            using (var configWriter = new StreamWriter(AppInfo.ConfigPath))
            {
                configWriter.WriteLineAsync(configJson).Wait();
            }
        }

        public void LoadConfig()
        {
            string configJson;
            using (StreamReader sr = new StreamReader(AppInfo.ConfigPath))
            {
                configJson = sr.ReadToEnd();
            }
            Config = JsonConvert.DeserializeObject<Configuration>(configJson, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        }
    }
}
