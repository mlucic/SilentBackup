using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SilentBackupService;
/* using Google.Apis.Auth.OAuth2; */
using System.Threading;
using System.Xml.Serialization;
/* using Newtonsoft.Json; */
using System.Security.Principal;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SilentBackup.Classes;

namespace SilentBackup
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public MainWindow()
        {
            //  Forcing the application to run as admin so that it can write into the application folder in program files
            bool adminMode = false;
            if (adminMode)
            {
                if (IsAdministrator() == false)
                {
                    var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo(exeName);
                    startInfo.Verb = "runas";
                    System.Diagnostics.Process.Start(startInfo);
                    System.Windows.Application.Current.Shutdown();
                    return;
                }
            }

            //this.Loaded += manageUIEditMode((backupList.SelectedItem as BackupOperation).IsValid);
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            this.DataContext = new MainWindowViewModel();
            InitializeComponent();
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            viewModel.LoadConfiguration();
            //viewModel.InitRelayCommands();
            ReflectModelState();
        }

        private void ReflectModelState()
        {
            var viewModel = (MainWindowViewModel)DataContext;
            AddLbl.IsEnabled = !EditModeEnabled;
            DeleteLbl.IsEnabled = EditLbl.IsEnabled = (!EditModeEnabled && backupList.SelectedItem != null);
        }

        /// <summary>
        /// Add new Backup Operation -  Should Use the same logic as edit command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddLbl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            if (viewModel.AddCommand.CanExecute(null)) viewModel.AddCommand.Execute(null);
            // backupList.SelectedValue = viewModel.SelectedBackup;
            AddModeEnabled = true;
            manageUIEditMode(true);
            ReflectModelState();
        }

        private void DeleteLbl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            if (viewModel.DeleteCommand.CanExecute(null))
                viewModel.DeleteCommand.Execute(null);

            ReflectModelState();
        }

        /// <summary>
        /// Edit Backup Operation 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditLbl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            int toBeEdited = backupList.SelectedIndex;

            /* Case nothing was picked up*/
            if (toBeEdited >= 0)
            {
                manageUIEditMode(true);
            }
            ReflectModelState();
        }

        /// <summary>
        ///  Called on "Save" button click event 
        ///  Changes edit mode of UI back to 'false', so elements can't be edited by user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveBtn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            if (viewModel.SaveCommand.CanExecute(null)) viewModel.SaveCommand.Execute(null);
            manageUIEditMode(false);
            ReflectModelState();
        }

        private void DiscardBtn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            if (viewModel.DiscardCommand.CanExecute(AddModeEnabled ? null : rollback))
                viewModel.DiscardCommand.Execute(AddModeEnabled ? null : rollback);

            if (AddModeEnabled) AddModeEnabled = false;
            manageUIEditMode(false);
            ReflectModelState();
        }

        private BackupOperation rollback;

        /// <summary>
        ///   Toggles UI "Edit Mode"
        /// </summary>
        /// <param name="editModeEnabled"> Specifies whether user can edit elements or not</param>
        private void manageUIEditMode(bool editModeEnabled)
        {
            EditModeEnabled = editModeEnabled;
            var switchColour = (editModeEnabled) ? Color.FromArgb(255, (byte)51, (byte)153, (byte)255)
                                                  : Color.FromRgb((byte)255, (byte)255, (byte)255);

            /* Enable/Disable TextBoxes ( fields that you can type in ) */
            BackupAlias.IsEnabled = editModeEnabled;

            /* Create a copy of the selected backup prior to editing for rollback purposes */
            if (editModeEnabled)
                rollback = (backupList.SelectedItem as BackupOperation).Clone();

            /* Disable/Enable selection of the list of backups */
            backupList.IsHitTestVisible = !editModeEnabled;


            /* Disable/Enable destinations */
            foreach (var stack in FindVisualChildren<DockPanel>(this))
            {
                if (stack.Name == "DestinationStack")
                {
                    stack.IsEnabled = editModeEnabled;
                }

            }
            /* Disable/Enable Source */
            SourceStack.IsEnabled = editModeEnabled;

            /* Disable/Enable buttons */
            DeleteLbl.IsEnabled = !editModeEnabled;
            EditLbl.IsEnabled = !editModeEnabled;
            AddLbl.IsEnabled = !editModeEnabled;


            /* Set visiblility of some elements */
            SaveBtn.IsEnabled = DiscardBtn.IsEnabled = editModeEnabled;
            SaveBtn.Visibility = DiscardBtn.Visibility = (editModeEnabled) ? Visibility.Visible : Visibility.Hidden;

            /* Set colour of some elements */
            BackupAlias.Foreground = new SolidColorBrush(switchColour);
            DetailsBorder.BorderBrush = new SolidColorBrush(switchColour);

            if (editModeEnabled)
            {
                Keyboard.Focus(BackupAlias);
                FocusManager.SetFocusedElement(this, BackupAlias); /* Set logical focus */
            }
        }

        /// <summary>
        ///  Finds children of certain type for parent element of certain type
        /// </summary>
        /// <typeparam name="T">Child type</typeparam>
        /// <param name="depObj">Parent</param>
        /// <returns> Collection of elements </returns>
        public IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        /// <summary>
        ///  Adds new destination to the list of destinations in the right details panel
        ///  Changes edit mode of UI to 'true', so elements can be edited by user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addDestination(object sender, MouseButtonEventArgs e)
        {
            DestinationList.SelectedIndex = DestinationList.Items.Count;
            manageUIEditMode(true);
        }

        private string pathPlaceholder = "specify path here...";

        public bool AddModeEnabled { get; private set; }

        public bool EditModeEnabled { get; private set; }

        /// <summary>
        ///  Adds Placeholder to textBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {

            TextBox field = sender as TextBox;
            if (String.IsNullOrWhiteSpace(field.Text) && field.IsEnabled)
            {
                field.Text = pathPlaceholder;
                field.Foreground = new SolidColorBrush(Color.FromRgb((byte)100, (byte)100, (byte)100));
            }
        }


        /// <summary>
        ///  Remove placeholder from TextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox field = sender as TextBox;
            if (field.IsEnabled && field.Text == pathPlaceholder)
            {
                field.Text = "";
                field.Foreground = new SolidColorBrush(Color.FromArgb(255, (byte)51, (byte)153, (byte)255));
            }
        }

        /// <summary>
        ///  Opens Browse Folders window, so user can select path
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Browse_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Determine the type of provider for selected destination
            /* Disable/Enable destinations */
            var selectedItem = DestinationList.SelectedItem as ListBoxItem;

            ComboBox combo = new ComboBox { };

            foreach (var element in FindVisualChildren<ComboBox>(selectedItem))
            {
                if (element.Name == "DestinationProvider")
                {
                    combo = element as ComboBox;
                }
            }

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {


            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            foreach (var item in e.AddedItems)
            {
                viewModel.SelectedBackup = item as BackupOperation;
            }
            ReflectModelState();
        }

        
    }
}