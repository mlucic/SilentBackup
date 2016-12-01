using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management;
using System.Threading;
using System.Xml.Serialization;

namespace SilentBackupService
{
    /// <summary>
    /// Abstract base class describing an event that is associated with one or more operations as a trigger
    /// </summary>
    [Serializable]
    [XmlInclude(typeof(USBInsertionEvent))]
    [XmlInclude(typeof(DateTimeEvent))]
    [XmlInclude(typeof(LogonEvent))]
    public abstract class Event
    {
        /// <summary>
        /// Toggle describing whether the event is to be handled or not
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>
        /// Internal identifier for the event
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Constructor for Event
        /// </summary>
        /// <param name="id">Internal identifier of the event</param>
        public Event(int id)
        {
            Id = id;
        }
        public Event()
        {

        }
    }

    /// <summary>
    /// Event triggered by the insertion of a user specified USB storage device
    /// </summary>
    public class USBInsertionEvent : Event
    {
        /// <summary>
        /// The VSN of the USB storage device
        /// </summary>
        public string VolumeSerialNumber { get; set; }
        /// <summary>
        /// Name of USB storage device
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Constructor for USBInsertionEvent
        /// </summary> 
        /// <param name="id">Internal identifier of the event</param>
        /// <param name="vsn">Volume serial number of the USB storage device</param>
        /// <param name="name">Name of USB</param>
        public USBInsertionEvent(int id, string vsn, string name = null) : base(id)
        {
            VolumeSerialNumber = vsn;
            Name = name;
        }
        public USBInsertionEvent()
        {

        }
    }

    /// <summary>
    /// Event triggered by the logon of the user
    /// </summary>
    public class LogonEvent : Event
    {
        /// <summary>
        /// Constructor for LogonEvent
        /// </summary>
        /// <param name="id">Internal identifier of the event</param>
        public LogonEvent(int id) : base(id)
        {
        }
        public LogonEvent()
        {

        }
    }

    /// <summary>
    /// Event triggered at a user specified time
    /// </summary>
    public class DateTimeEvent : Event
    {
        /// <summary>
        /// The first occurence of the event
        /// </summary>
        public DateTime Start { get; set; }
        /// <summary>
        /// The next occurence of the event that has yet to occur
        /// </summary>
        public DateTime Next { get; set; }
        /// <summary>
        /// The time in minutes between events
        /// Negative values denote special values: 
        ///     -1 : End of month
        /// </summary>
        public int IntervalMinutes { get; set; }
        /// <summary>
        /// Option denoting that the event is to trigger execution immediately
        /// once the service returns to a running state in the case that the service
        /// was not running during the previous one or many occurrences of the event.
        /// 
        /// If false, the next backup will be scheduled to occur at the next possible
        /// passing of the interval from the previously expected occurrence
        /// </summary>
        public bool RunIfMissed { get; set; }
        /// <summary>
        /// Constructor for DateTimeEvent
        /// </summary>
        /// <param name="id">Internal identifier of the event</param>
        /// <param name="start">When the first occurence of the event will be</param>
        /// <param name="intervalMins">Time in minutes between occurrences of the event</param>
        /// <param name="rim">Have the event occur immediately after missing occurence(s) due to the service not running during the expected time</param>
        public DateTimeEvent(int id, DateTime start, int intervalMins, bool rim) : base(id)
        {
            DateTime st = start;
            if (st < DateTime.Now)
                st = DateTime.Now;

            Start = st;
            Next = Start.AddMinutes(intervalMins);
            IntervalMinutes = intervalMins;
            RunIfMissed = rim;
        }
        public DateTimeEvent()
        {

        }
    }

    internal interface IEventHandler : IDisposable
    {
        void Run(object sender, DoWorkEventArgs ev);
    }

    /// <summary>
    /// Used to contain callback code that will be used by event handlers
    /// </summary>
    internal delegate void Callback();

    internal abstract class EventHandler : IEventHandler
    {
        protected IDictionary<Event, List<Callback>> EventToCallbackMapping;
        public virtual void AddEvent(Event e, List<Callback> callBacks)
        {
            try
            {
                EventToCallbackMapping.Add(e, callBacks);
            }
            catch (ArgumentException ex)
            {
                EventToCallbackMapping.SingleOrDefault(x => x.Key == e).Value.AddRange(callBacks);
            }
            catch (Exception ex)
            {
                ReportIO.WriteStatement("An issue occurred adding events to the event handler: " + ex.Message);
            }
        }
        public virtual void Run(object sender, DoWorkEventArgs ev) { }
        public abstract void Dispose();
    }

    internal class USBInsertionEventHandler : EventHandler
    {
        private ManagementEventWatcher watcher;

        public USBInsertionEventHandler()
        {
            WqlEventQuery query =
            new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_LogicalDisk'");

            watcher = new ManagementEventWatcher(query);
            EventToCallbackMapping = new Dictionary<Event, List<Callback>>();
        }

        public override void Run(object sender, DoWorkEventArgs ev)
        {
            try
            {
                while (true)
                {
                    bool satisfaction = false;
                    string vsn = "";

                    watcher.EventArrived +=
                        new EventArrivedEventHandler(
                        (send, eve) =>
                        {
                            if (((ManagementBaseObject)eve.NewEvent["TargetInstance"])["Description"].ToString() == "Removable Disk")
                            {
                                satisfaction = true;
                                vsn = ((ManagementBaseObject)eve.NewEvent["TargetInstance"])["VolumeSerialNumber"].ToString();
                            }
                        });

                    watcher.Start();
                    WaitHandle wh = new ManualResetEvent(false);

                    while (!satisfaction)
                    {
                        if ((sender as BackgroundWorker).CancellationPending == true)
                        {
                            ev.Cancel = true;
                            Dispose();
                            return;
                        }
                        wh.WaitOne(5000);
                    };

                    watcher.Stop();

                    var eventsWithCallbacks = EventToCallbackMapping.Where(x => (x.Key as USBInsertionEvent).VolumeSerialNumber.Equals(vsn));

                    // Execute callbacks
                    foreach (var eventWithCallback in eventsWithCallbacks)
                    {
                        foreach (var callback in eventWithCallback.Value)
                        {
                            callback.Invoke();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                watcher.Dispose();
                throw ex;
            }
        }
        public override void Dispose()
        {
            watcher.Stop();
            watcher.Dispose();
        }
    }

    internal class DateTimeEventHandler : EventHandler
    {
        public DateTimeEventHandler()
        {
            // Making the event to callback map sorted by the time of the next occurence
            EventToCallbackMapping = new SortedDictionary<Event, List<Callback>>(Comparer<Event>.Create((x, y) =>
            {
                return DateTime.Compare((x as DateTimeEvent).Next, (y as DateTimeEvent).Next);
            }));
        }

        public override void Run(object sender, DoWorkEventArgs ev)
        {
            // Sort the items based off the next interval from now
            var now = DateTime.Now;

            foreach (var evn in EventToCallbackMapping.Keys)
            {
                var dte = evn as DateTimeEvent;
                var item = EventToCallbackMapping.SingleOrDefault(x => x.Key.Id == dte.Id);
                EventToCallbackMapping.Remove(item.Key);
                
                // Run events that are past-due
                if (dte.RunIfMissed)
                {
                    foreach (var cb in item.Value)
                    {
                        cb.Invoke();
                    }
                }
                (item.Key as DateTimeEvent).Next = now.AddMinutes(dte.IntervalMinutes);
                EventToCallbackMapping.Add(item.Key, item.Value);
            }
            /////////////////////////////////////////////////////////

            while (true)
            {
                if (EventToCallbackMapping.Count > 0)
                {
                    var eventWithCallbacks = EventToCallbackMapping.First();
                    EventToCallbackMapping.Remove(eventWithCallbacks.Key); // Effectively pop front
                    var dte = (eventWithCallbacks.Key as DateTimeEvent); // Grab the events as date time events because that's what they are!

                    var waitTime = 0.0;
                    var next = dte.Next;

                    // Determine how long to wait to invoke callback
                    if (next > DateTime.Now)
                    {
                        var timeSince = (DateTime.Now - next).TotalMinutes;
                        var x = Math.Floor(timeSince / dte.IntervalMinutes);
                        next = next.AddMinutes(dte.IntervalMinutes * (x + 1));
                        waitTime = Math.Max(0, (next - DateTime.Now).TotalMilliseconds);
                    }

                    var sleepInterval = 5000;

                    WaitHandle wh = new ManualResetEvent(false);

                    for (int i = sleepInterval; i <= waitTime; i += sleepInterval)
                    {
                        if ((sender as BackgroundWorker).CancellationPending == true)
                        {
                            ev.Cancel = true;
                            if (dte.RunIfMissed)
                                EventToCallbackMapping.Add(eventWithCallbacks.Key, eventWithCallbacks.Value);
                            return;
                        }
                        wh.WaitOne(sleepInterval);
                    }

                    foreach (var callback in eventWithCallbacks.Value)
                    {
                        callback.Invoke();
                    }

                    if (dte.IntervalMinutes == -1) // End of month logic
                        dte.Next = dte.Next.AddMonths(1);
                    else
                        dte.Next = dte.Next.AddMinutes(dte.IntervalMinutes);

                    // Re-insert the event into it's proper position (its a SortedDictionary)
                    EventToCallbackMapping.Add(eventWithCallbacks.Key, eventWithCallbacks.Value);
                }
                Thread.Sleep(5000);
            }
        }
        public override void Dispose()
        {
        }
    }

    internal class LogonEventHandler : EventHandler
    {
        public override void Run(object sender, DoWorkEventArgs ev)
        {
            if (!System.IO.Directory.Exists(AppInfo.LogonMarkerPath))
            {
                // Execute callbacks
                if (this.EventToCallbackMapping != null)
                {
                    foreach (var eventWithCallback in this.EventToCallbackMapping)
                    {
                        foreach (var callback in eventWithCallback.Value)
                        {
                            callback.Invoke();
                        }
                    }
                }

                // Create logon marker
                System.IO.Directory.CreateDirectory(AppInfo.LogonMarkerPath);

                TaskDefinition td = TaskService.Instance.NewTask();
                td.Triggers.Add(new SessionStateChangeTrigger { StateChange = TaskSessionStateChangeType.ConsoleDisconnect });
                td.Triggers.Add(new SessionStateChangeTrigger { StateChange = TaskSessionStateChangeType.RemoteDisconnect });
                td.Actions.Add("del /Q /F /S", AppInfo.LogonMarkerPath);
                td.Actions.Add("SchTasks /Delete /TN /F", "SilentBackupLogoffHandler"); // Get rid of the handler once its used
                TaskService.Instance.RootFolder.RegisterTaskDefinition("SilentBackupLogoffHandler", td);

                // // IMPORTANT: This needs to occur as a part of the application uninstallation process
                // //TaskService.Instance.RootFolder.DeleteTask("SilentBackupLogoffHandler", false);
            }
        }

        // Quicker access to use the Run method seeing as sender and ev are never used
        public void Run()
        {
            Run(null, null);
        }

        public override void Dispose()
        {
        }
    }
}
