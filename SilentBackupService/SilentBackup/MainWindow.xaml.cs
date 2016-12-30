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
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.ComponentModel;
using static SilentBackupService.BackupOperation;

namespace SilentBackup
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int destinationChangingId;
        private int operationChangingId;
        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public MainWindow()
        {
            destinationChangingId = -1;
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
            manageUIEditMode(false);
            ReflectModelState();
        }

        private void ReflectModelState()
        {
            var viewModel = (MainWindowViewModel)DataContext;
            AddLbl.IsEnabled = !EditModeEnabled;
            DeleteLbl.IsEnabled = EditLbl.IsEnabled = (!EditModeEnabled && backupList.SelectedItem != null);
            Options.Visibility = viewModel.SelectedBackup != null ? Visibility.Visible : Visibility.Hidden;
        }

        /// <summary>
        /// Add new Backup Operation -  Should Use the same logic as edit command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddLbl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            if (viewModel.AddCommand.CanExecute(null)) viewModel.AddCommand.Execute(null);
            // backupList.SelectedValue = viewModel.SelectedBackup;
            AddModeEnabled = true;
            manageUIEditMode(true);
            ReflectModelState();
        }

        private void DeleteLbl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
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
        private void EditLbl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
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
        private void SaveLbl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;

            if (viewModel.SaveCommand.CanExecute(null))
                viewModel.SaveCommand.Execute(null);

            AddModeEnabled = false;
            manageUIEditMode(false);
            ReflectModelState();
        }

        private void DiscardLbl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            var rollback = AddModeEnabled ? null : SilentBackupService.Utility.XmlSerializer.Read<BackupOperation>("rollback.xml");
            if (viewModel.DiscardCommand.CanExecute(rollback))
                viewModel.DiscardCommand.Execute(rollback);

            AddModeEnabled = false;
            manageUIEditMode(false);
            ReflectModelState();
        }

        private BackupOperation rollback;

        /// <summary>
        /// Toggles UI "Edit Mode"
        /// </summary>
        /// <param name="editModeEnabled"> Specifies whether user can edit elements or not</param>
        private void manageUIEditMode(bool editModeEnabled)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            viewModel.EditMode = editModeEnabled;
            Options.IsEnabled = editModeEnabled;
            EditModeEnabled = editModeEnabled;
            var switchColour = (editModeEnabled) ? Color.FromArgb(255, (byte)51, (byte)153, (byte)255)
                                                  : Color.FromRgb((byte)255, (byte)255, (byte)255);

            /* Enable/Disable TextBoxes ( fields that you can type in ) */
            AliasTextBox.IsEnabled = editModeEnabled;

            /* Create a copy of the selected backup prior to editing for rollback purposes */
            if (editModeEnabled)
            {
                SilentBackupService.Utility.XmlSerializer.Write<BackupOperation>("rollback.xml", backupList.SelectedItem as BackupOperation);
            }

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

            foreach (var label in FindVisualChildren<System.Windows.Controls.Label>(this))
            {
                if (label.Name == "AddDestinationLbl")
                {
                    if (editModeEnabled)
                    {
                        label.Height = 32;
                    }
                    else
                    {
                        label.Height = 0;
                    }
                }
            }

            /* Disable/Enable Source */
            SourceStack.IsEnabled = editModeEnabled;

            /* Disable/Enable buttons */
            DeleteLbl.IsEnabled = !editModeEnabled;
            EditLbl.IsEnabled = !editModeEnabled;
            AddLbl.IsEnabled = !editModeEnabled;

            /* Set visiblility of some elements */
            SaveLbl.IsEnabled =
                DiscardLbl.IsEnabled =
                AddDestinationLbl.IsEnabled = editModeEnabled;
            SaveLbl.Visibility =
                DiscardLbl.Visibility =
                AddDestinationLbl.Visibility = (editModeEnabled) ? Visibility.Visible : Visibility.Hidden;

            /* Set colour of some elements */
            AliasTextBox.Foreground = new SolidColorBrush(switchColour);
            DetailsBorder.BorderBrush = new SolidColorBrush(switchColour);

            if (editModeEnabled)
            {
                Keyboard.Focus(AliasTextBox);
                FocusManager.SetFocusedElement(this, AliasTextBox); /* Set logical focus */
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
        private void AddDestinationLbl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;

            if (viewModel.AddDestinationCommand.CanExecute(null))
                viewModel.AddDestinationCommand.Execute(null);
        }

        public static string PathPlaceholder = "Specify path here";

        public bool AddModeEnabled { get; private set; }

        public bool EditModeEnabled { get; private set; }

        /// <summary>
        ///  Adds Placeholder to textBox for path
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox field = sender as TextBox;
            if (String.IsNullOrWhiteSpace(field.Text) && field.IsEnabled)
            {
                field.Text = PathPlaceholder.Replace("path", field.Name.Replace("TextBox", "").ToLower());
                field.Foreground = new SolidColorBrush(Color.FromRgb((byte)100, (byte)100, (byte)100));
            }
        }

        /// <summary>
        ///  Remove placeholder from TextBox for path
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PathTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox field = sender as TextBox;
            if (field.IsEnabled && field.Text == PathPlaceholder.Replace("path", field.Name.Replace("TextBox", "").ToLower()))
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
            var dlg = new System.Windows.Forms.FolderBrowserDialog();

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
            System.Windows.Forms.DialogResult result = dlg.ShowDialog();
            if (result.HasFlag(System.Windows.Forms.DialogResult.OK))
            {
                var a = VisualTreeHelper.GetParent(sender as System.Windows.Controls.Label);
                var b = VisualTreeHelper.GetParent(a);
                var c = (b as DockPanel).Children[3] as TextBox;
                c.Text = dlg.SelectedPath;
            }
        }

        private void OnBackupOperationSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            foreach (var item in e.AddedItems)
            {
                viewModel.SelectedBackup = item as BackupOperation;
            }
            ReflectModelState();
        }

        private void ExpandProviderComboBox(object sender, MouseButtonEventArgs e)
        {
            var stackPanel = sender as StackPanel;
            var parent = (stackPanel.Parent as Border).Parent as DockPanel;
            var comboBox = parent.Children.OfType<ComboBox>().FirstOrDefault();
            comboBox.IsDropDownOpen = true;
        }
        private void CollapseProviderComboBox(object sender, MouseEventArgs e)
        {
            (sender as ComboBox).IsDropDownOpen = false;
        }
    
        private double ResizeProviderIconFactor = 1.04;
        private bool ResizeProviderIconState = false;
        private void ResizeProviderIcon(object sender, MouseEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            string providerIconPath = "";
            Image image = sender as Image;
            switch ((ServiceProviders)image.Tag)
            {
                case ServiceProviders.Local:
                    providerIconPath = viewModel.LocalIcon;
                    break;
                case ServiceProviders.Google:
                    providerIconPath = viewModel.GoogleIcon;
                    break;
                case ServiceProviders.OneDrive:
                    providerIconPath = viewModel.OneDriveIcon;
                    break;
                case ServiceProviders.DropBox:
                    providerIconPath = viewModel.DropBoxIcon;
                    break;
                case ServiceProviders.SSH:
                    providerIconPath = viewModel.SSHIcon;
                    break;
                default:
                    throw new Exception("Invalid provider specified in selected backups source");
            }
            BitmapImage source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            if (!ResizeProviderIconState)
            {
                source.UriSource = new Uri(providerIconPath.Substring(0, providerIconPath.LastIndexOf('.')) + "Inverse.png");
                if (image != null)
                {
                    image.Width = image.Width * ResizeProviderIconFactor;
                    image.Height = image.Height * ResizeProviderIconFactor;
                }
            }
            else
            {
                source.UriSource = new Uri(providerIconPath);
                if (image != null)
                {
                    image.Width = image.Width / ResizeProviderIconFactor;
                    image.Height = image.Height / ResizeProviderIconFactor;
                }
            }
            source.EndInit();
            if (image != null) image.Source = source;
            ResizeProviderIconState = !ResizeProviderIconState;
        }

        private void ChangeProviderIconUri(Image img, ServiceProviders provider)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            BitmapImage source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            string providerIconPath = "";
            switch (provider)
            {
                case ServiceProviders.Local:
                    providerIconPath = viewModel.LocalIcon;
                    break;
                case ServiceProviders.Google:
                    providerIconPath = viewModel.GoogleIcon;
                    break;
                case ServiceProviders.OneDrive:
                    providerIconPath = viewModel.OneDriveIcon;
                    break;
                case ServiceProviders.DropBox:
                    providerIconPath = viewModel.DropBoxIcon;
                    break;
                case ServiceProviders.SSH:
                    providerIconPath = viewModel.SSHIcon;
                    break;
                default:
                    throw new Exception("Invalid provider specified in selected backups source");
            }

            source.UriSource = new Uri(providerIconPath);
            source.EndInit();
            img.Source = source;
        }

        private void ProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = (MainWindowViewModel)DataContext;
            var a = VisualTreeHelper.GetParent(sender as ComboBox);
            var b = (a as DockPanel).Children[1];
            var c = (b as Border).Child;
            var d = (c as StackPanel).Children[0];
            if ((d as Image).Tag != null)
            {
                ServiceProviders provider = (ServiceProviders)(d as Image).Tag;
                ChangeProviderIconUri(d as Image, (ServiceProviders)(d as Image).Tag);
            }
        }

        private double ResizeImageFactor = 1.04;
        private bool ResizeImageState = false;
        private void ToggleImageHoverState(object sender, MouseEventArgs e)
        {
            if((sender as Image).Source != null)
            {
                Image image = sender as Image;
                ImageSourceConverter imgConv = new ImageSourceConverter();
                string imagePath = imgConv.ConvertToString(image.Source);

                BitmapImage source = new BitmapImage();
                source.BeginInit();
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                if (!ResizeImageState)
                {
                    source.UriSource = new Uri(imagePath.Substring(0, imagePath.LastIndexOf('.')) + "Inverse.png");
                    if (image != null)
                    {
                        image.Width = image.Width * ResizeImageFactor;
                        image.Height = image.Height * ResizeImageFactor;
                    }
                }
                else
                {
                    string[] splitStrs = { "Inverse" };
                    source.UriSource = new Uri(imagePath.Split(splitStrs, StringSplitOptions.None)[0] + ".png");
                    if (image != null)
                    {
                        image.Width = image.Width / ResizeImageFactor;
                        image.Height = image.Height / ResizeImageFactor;
                    }
                }
                source.EndInit();
                if (image != null) image.Source = source;
                ResizeImageState = !ResizeImageState;
            }
        }
    }
}