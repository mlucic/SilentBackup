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
using SilentBackupService.Utility;
using System.Collections.Specialized;

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
            //configuration_ = new Configuration();

            /* Set Relay Commands */
            InitRelayCommands();
        }

        private void UpdateBackupListUI(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("BackOps");
        }

        #region RelayCommands

        /// <summary>
        ///  Sets relay commands ( Relay commands provide communication between UI events and ViewModels )
        /// </summary>
        public void InitRelayCommands()
        {
            //BackOps.CollectionChanged += UpdateBackupListUI;
            // On Delete Backup Operation command, do the following :
            DeleteCommand = new RelayCommand(OnDelete, CanDelete);

            // On Add new Backup Operation command, do the following : 
            AddCommand = new RelayCommand(() =>
            {
                var newId = BackOps?.Count > 0 ? BackOps.Max(x => x.Id) + 1 : 1;
                var newBackOp = new BackupOperation() { Alias = "New Backup", Id = newId, Enabled = true };
                newBackOp.Source.AbsolutePath = "Enter absolute path";
                BackOps.Add(newBackOp);
                SelectedBackup = newBackOp;
            }, () =>
            {
                //return BackOps.LastOrDefault() != null ? BackOps.LastOrDefault().IsValid : false; 
                return true;
            });

            // On Add new Backup Destinaion command, do the following :
            AddDestinationCommand = new RelayCommand(() =>
            {
                DestinationInfo di = new DestinationInfo() { Path = new SilentBackupService.Path() { AbsolutePath = "enter path here..." } };
                SelectedBackup.Destinations.Add(di);
                DestInfos.Add(di);
            }, () => { return SelectedBackup != null; });

            // On Toggle Enables/Disabled command, do the following :
            SwitchEnabledCommand = new RelayCommand(() =>
            {
                SelectedBackup.Enabled = !SelectedBackup.Enabled;
            }, () => { return SelectedBackup != null; });

            SaveCommand = new RelayCommand(() =>
            {
                configuration_.BackupOperations = BackOps;
                XmlSerializer.Write<Configuration>(AppInfo.ConfigPath, configuration_);
                RaisePropertyChanged("BackOps");
            }, () => { return true; });

            DiscardCommand = new RelayCommand<BackupOperation>((rollback) =>
            {
                if (rollback != null)
                {
                    var backup = BackOps.SingleOrDefault(x => x.Id == rollback.Id);
                    BackOps.Insert(BackOps.IndexOf(backup), rollback);
                    BackOps.Remove(backup);
                    SelectedBackup = rollback;
                }
                else
                {
                    if (DeleteCommand.CanExecute(null)) DeleteCommand.Execute(null);
                }
            }, (rollback) => { return true; });
        }

        public ICommand DeleteCommand { get; private set; }

        public ICommand AddCommand { get; private set; }

        public ICommand AddDestinationCommand { get; private set; }

        public ICommand SwitchEnabledCommand { get; private set; }

        public ICommand SaveCommand { get; private set; }

        public ICommand DiscardCommand { get; private set; }

        private void OnDelete()
        {
            int toBeDeleted = BackOps.IndexOf(SelectedBackup);
            BackOps.Remove(SelectedBackup);
            if (BackOps.Count > 0)
            {
                SelectedBackup = BackOps.ElementAt((toBeDeleted == 0) ? 0 : (toBeDeleted - 1));
            }
            else SelectedBackup = null;
            RaisePropertyChanged("BackOps");
        }

        private bool CanDelete()
        {
            return selectedBackup_ != null;
        }
        #endregion

        #region LoadAndSaveConfigurations
        /* Consider async implementation - Loads file OnLoad */

        public void LoadConfiguration()
        {
            try
            {
                configuration_ = XmlSerializer.Read<Configuration>(AppInfo.ConfigPath);
            }
            catch (Exception ex)
            {
                // Do nothing
            }

            if (configuration_ == null)
            {
                configuration_ = new Configuration();
                XmlSerializer.Write<Configuration>(AppInfo.ConfigPath, configuration_);
            }

            BackOps = new ObservableCollection<BackupOperation>(configuration_.BackupOperations);
            BackOps.CollectionChanged += BackOps_CollectionChanged;

            selectedBackup_ = configuration_.BackupOperations.Count > 0 ? configuration_.BackupOperations.ElementAt(0) : null;
            if (selectedBackup_ != null)
            {
                DestInfos = new ObservableCollection<DestinationInfo>(selectedBackup_.Destinations);
                /* TODO : Init events */
            }
            RaisePropertyChanged("BackOps");
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

                }
            }
        }

        private void BackOps_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("BackOps");
        }

        /// <summary> 
        /// Selected backup operation (appears in the right panel with operations details)
        /// </summary>
        public BackupOperation SelectedBackup
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
                        RaisePropertyChanged("SelectedBackup");
                        RaisePropertyChanged("BackOps");
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
                    RaisePropertyChanged("DestInfos");
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
                    RaisePropertyChanged("SelectedUSBEvents");
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
                    RaisePropertyChanged("SelectedDateTimeEvents");
                }

            }
        }

        #endregion

        #region PropertyChangedDefinition

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(
            [CallerMemberName] string caller = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(caller));
        }

        #endregion
    }
}

