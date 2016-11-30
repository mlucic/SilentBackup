using SilentBackupService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Drawing;
using static SilentBackupService.BackupOperation;

namespace SilentBackup.Classes
{

    #region EventViewModels
    class EventV
    {
        public string EventName { get; set; }
    }

    class USBEventV : EventV
    {
        public USBEventV() { }
        public string UsbName { get; set; }
        public string UsbserialNumber { get; set; }
    }


    class DateTimeEventV : EventV
    {
        public DateTime Start { get; set; } // The first occurence of the event
        public DateTime Next { get; set; } // The next occurence of the event
        public int IntervalMinutes { get; set; } // The time in minutes between events. Negative values refer to special values: -1 Means use end of month
        public bool RunIfMissed { get; set; }
    }
    #endregion

    /// <summary>
    /// MainWindowViewModel serves as a connection between Back-End and UI (Model and View).
    /// </summary>
    class MainWindowViewModel : INotifyPropertyChanged
    {

        /** Our view Model properties are declared here */
        /// <summary> 
        /// Selected backup operation (appears in the right panel with operations details)
        /// </summary>
        private BackupOperation selectedBackup_;

        /// <summary> 
        ///  List of backup operations (appears in the left panels)
        /// </summary>
        private ObservableCollection<BackupOperation> backupOperations_;

        /// <summary> 
        ///  Contains information from JSON configuration file
        /// </summary>
        private Configuration configuration_;

        /// <summary> 
        ///  instance of View Model class which represents usb events 
        /// </summary>
        private ObservableCollection<USBEventV> usbEvents_;

        /// <summary> 
        ///  instance of View Model class which represents date time events 
        /// </summary>
        private ObservableCollection<DateTimeEventV> dateTimeEvents_;

        /// <summary> 
        ///  List of Destinations for selected backup operations
        /// </summary>
        private ObservableCollection<DestinationInfo> destInfos_;


        /// <summary>
        /// Loads json Configuration file with backup operations and inits RelayCommands 
        /// </summary>
        public MainWindowViewModel()
        {
            /* Grabs configuraton from JSON file */
            LoadConfiguration();

            /* Set Relay Commands */
            InitRelayCommands();
        }

        #region RelayCommands

        /// <summary>
        ///  Sets relay commands ( Relay commands provide communication between UI events and ViewModels )
        /// </summary>
        private void InitRelayCommands()
        {

            // On Delete Backup Operation command, do the following :
            DeleteCommand = new RelayCommand(OnDelete, CanDelete);

            // On Add new Backup Operation command, do the following : 
            AddCommand = new RelayCommand(() =>
            {
                var newBackOp = new BackupOperation() { Alias = "New Backup Operation" };
                DestinationInfo di = new DestinationInfo() { Path = new SilentBackupService.Path() { AbsolutePath = "enter path here..." } };
                newBackOp.Destinations.Add(di);
                BackOps.Add(newBackOp);
                SelectedBackOp = newBackOp;
                // DestInfos.Add(di);
            }, () => {
                //return BackOps.LastOrDefault() != null ? BackOps.LastOrDefault().IsValid : false; 
                return true;
            });

            // On Add new Backup Destinaion command, do the following :
            AddDestinationCommand = new RelayCommand(() =>
            {
                DestinationInfo di = new DestinationInfo() { Path = new SilentBackupService.Path() { AbsolutePath = "enter path here..." } };
                SelectedBackOp.Destinations.Add(di);
                DestInfos.Add(di);
            }, () => { return SelectedBackOp != null; });

            // On Toggle Enables/Disabled command, do the following :
            SwitchEnabledCommand = new RelayCommand(() =>
            {
                SelectedBackOp.Enabled = !SelectedBackOp.Enabled;
            }, () => { return SelectedBackOp != null; });
        }

        public ICommand DeleteCommand { get; private set; }

        public ICommand AddCommand { get; private set; }

        public ICommand AddDestinationCommand { get; private set; }

        public ICommand SwitchEnabledCommand { get; private set; }

        /** cont */
        private void OnDelete()
        {
            int toBeDeleted = BackOps.IndexOf(selectedBackup_);
            BackOps.Remove(selectedBackup_);
            if (BackOps.Count > 0)
            {
                selectedBackup_ = BackOps.ElementAt((toBeDeleted == 0) ? 0 : (toBeDeleted - 1));
            }
            else selectedBackup_ = null;
        }

        private bool CanDelete()
        {
            return selectedBackup_ != null;
        }
        #endregion

        #region LoadAndSaveConfigurations
        /* Consider async implementation - Loads file OnLoad */
        private void LoadConfiguration()
        {
            try
            {
                string configJson;
                using (StreamReader sr = new StreamReader(AppInfo.ConfigPath))
                {
                    configJson = sr.ReadToEnd();
                }

                configuration_ = JsonConvert.DeserializeObject<Configuration>(configJson, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            }
            catch (Exception ex)
            {
                // No config to load, make new config
                configuration_ = new Configuration();
                string configJson = JsonConvert.SerializeObject(configuration_, new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.All,
                    TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple
                });

                using (var configWriter = new StreamWriter(AppInfo.ConfigPath))
                {
                    configWriter.WriteLineAsync(configJson).Wait();
                }
            }






            /* TODO: Handle file not found exception */
            //string configJson;
            //using (StreamReader sr = new StreamReader(AppInfo.ConfigPath))
            //{
            //    configJson = sr.ReadToEnd();
            //}

            //configuration_ = JsonConvert.DeserializeObject<Configuration>(configJson, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

            backupOperations_ = new ObservableCollection<BackupOperation>(configuration_.BackupOperations);
            selectedBackup_ = configuration_.BackupOperations.Count > 0 ? configuration_.BackupOperations.ElementAt(0) : null;
            if (selectedBackup_ != null)
            {
                DestInfos = new ObservableCollection<DestinationInfo>(selectedBackup_.Destinations);
                /* TODO : Init events */
            }
        }


        /// <summary> *TODO
        ///   Saves backup operations on window close (destruction)
        /// </summary>
        private void saveConfiguration() { }
        #endregion

        #region PublicProperties  

        /// <summary> 
        ///  List of All the available providers (Appears in a drop-down list under proivder Icon )
        /// </summary>
        public IEnumerable<ValueDescription> ProviderList => EnumHelper.GetAllValuesAndDescriptions<ServiceProviders>();

        /// <summary> 
        ///  List of backup operations (appears in the left panels)
        /// </summary>
        public ObservableCollection<BackupOperation> BackOps
        {
            get { return backupOperations_; }
            set
            {
                if (value != backupOperations_)
                {
                    backupOperations_ = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary> 
        /// Selected backup operation (appears in the right panel with operations details)
        /// </summary>
        public BackupOperation SelectedBackOp
        {
            get { return selectedBackup_; }
            set
            {
                if (value != selectedBackup_)
                {
                    selectedBackup_ = value;
                    if (selectedBackup_ != null)
                    {
                        var sUsbEv = new ObservableCollection<USBEventV>();
                        var sDateTimeEv = new ObservableCollection<DateTimeEventV>();

                        //foreach (var triggerId in selectedBackup_.Triggers)
                        //{

                        //    var triggerType = configuration_.Triggers.ElementAt(triggerId).GetType();

                        //    /* If can cast to USBEvent -> push to USBEventList */
                        //    if (triggerType  == typeof(USBInsertionEvent))
                        //    {
                        //        var trigger = (configuration_.Triggers.ElementAt(triggerId) as USBInsertionEvent);
                        //        sUsbEv.Add(new USBEventV() { UsbName = trigger.Name,
                        //                                     UsbserialNumber = trigger.VolumeSerialNumber,

                        //                                    });

                        //    }/* Else if can cast to DateTimeEvent -> push to DateTimeList */
                        //    else if (triggerType == typeof(DateTimeEvent))
                        //    {
                        //        var trigger = (configuration_.Triggers.ElementAt(triggerId) as DateTimeEvent);

                        //        sDateTimeEv.Add(new DateTimeEventV { Start = trigger.Start, Next = trigger.Next});
                        //    } 
                        //}

                        //SelectedUSBEvents = sUsbEv;
                        //SelectedDateTimeEvents = sDateTimeEv;
                        DestInfos = new ObservableCollection<DestinationInfo>(selectedBackup_.Destinations);

                        RaisePropertyChanged("SelectedBackOp");
                        //DeleteCommand.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        /// <summary> 
        ///  List of Destinations for selected backup operations
        /// </summary>
        public ObservableCollection<DestinationInfo> DestInfos
        {
            get { return destInfos_; }
            set
            {
                if (destInfos_ != value)
                {
                    destInfos_ = value;
                    RaisePropertyChanged();
                }

            }
        }

        public ObservableCollection<USBEventV> SelectedUSBEvents
        {
            get { return usbEvents_; }
            set
            {
                if (usbEvents_ != value)
                {
                    usbEvents_ = value;
                    RaisePropertyChanged();
                }

            }
        }

        public ObservableCollection<DateTimeEventV> SelectedDateTimeEvents
        {
            get { return dateTimeEvents_; }
            set
            {
                if (dateTimeEvents_ != value)
                {
                    dateTimeEvents_ = value;
                    RaisePropertyChanged();
                }

            }
        }
        #endregion

        #region PropertyChangedDefinition

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(
            [CallerMemberName] string caller = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this,
                   new PropertyChangedEventArgs(caller));
            }
        }

        #endregion
    }
}

