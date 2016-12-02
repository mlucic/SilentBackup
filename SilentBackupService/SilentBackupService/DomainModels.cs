using System;
using System.Collections.Generic;
using System.IO;
using Google.Apis.Auth.OAuth2.Responses;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace SilentBackupService
{
    /// <summary>
    /// Stores paths to configuration and credential information
    /// </summary>
    public static class AppInfo
    {
        /// <summary>
        /// Application client identifier for the Google Drive API
        /// </summary>
        public static readonly string GoogleClientId;
        /// <summary>
        /// Application client secret for the Google Drive API
        /// </summary>
        public static readonly string GoogleClientSecret;
        /// <summary>
        /// Application identifier for the DropBox API
        /// </summary>
        public static readonly string DropBoxAppId;
        /// <summary>
        /// Application secret for the DropBox API
        /// </summary>
        public static readonly string DropBoxAppSecret;
        /// <summary>
        /// Application client identifier for the OneDrive API
        /// </summary>
        public static readonly string OneDriveClientId;
        /// <summary>
        /// Path to the configuration file being used by the application
        /// </summary>
        public static readonly string ConfigPath = "config.xml";
        /// <summary>
        /// Logon marker path being used by the application for creating a logon event
        /// </summary>
        public static readonly string LogonMarkerPath = ".logon";
        /// <summary>
        /// Log path for reporting
        /// </summary>
        public static readonly string ServiceLogPath = "service.log";
    }
    /// <summary>
    /// Service providers which can be interfaced with through the application
    /// </summary>
    [Serializable]
    public enum ServiceProviders {
        /// <summary>
        /// Local file system, including USB storage devices and other harddrives viewable from the local file system
        /// </summary>
        [Description("Local")] 
        Local,
        /// <summary>
        /// Google Drive
        /// </summary>
        [Description("Google Drive")]
        Google,
        /// <summary>
        /// OneDrive
        /// </summary>
        [Description("OneDrive")]
        OneDrive,
        /// <summary>
        /// DropBox
        /// </summary>
        [Description("Dropbox")]
        DropBox,
        /// <summary>
        /// Secure Shell
        /// </summary>
        [Description("SSH")]
        SSH
    };

    /// <summary>
    /// Contains user defined backup operations and the triggers which result in their execution
    /// </summary>
    [Serializable]
    public class Configuration
    {
        /// <summary>
        /// Collection of backup operations that the user has defined
        /// </summary>
        public Collection<BackupOperation> BackupOperations { get; set; }
        /// <summary>
        /// Collection of triggers that the user has defined
        /// </summary>
        public Collection<Event> Triggers { get; set; }
        /// <summary>
        /// Constructor for configuration
        /// </summary>
        public Configuration()
        {
            BackupOperations = new Collection<BackupOperation>();
            Triggers = new Collection<Event>();
        }
    }

    /// <summary>
    /// Contains user definitions for a single backup operation
    /// </summary>
    [Serializable]
    public class BackupOperation : IDataErrorInfo
    {
        public BackupOperation Clone()
        {
            return MemberwiseClone() as BackupOperation;
        }
        /// <summary>
        /// Abstract base class defining a label that will be used to manipulate the destination path name
        /// </summary>
        [Serializable]
        [XmlInclude(typeof(OverwriteLabel))]
        [XmlInclude(typeof(IndexedLabel))]
        [XmlInclude(typeof(DateTimeStampedLabel))]
        public abstract class Label
        {
            public Label()
            {

            }
        }
        /// <summary>
        /// Destination label defining that the destination is to be overwritten
        /// </summary>
        [Serializable]
        public class OverwriteLabel : Label { }
        /// <summary>
        /// Destination label for appending an index
        /// </summary>
        [Serializable]
        public class IndexedLabel : Label
        {
            /// <summary>
            /// Last index that has been used during labeling
            /// </summary>
            public int Index { get; set; }
            /// <summary>
            /// The number of trailing digits written when labeling (e.g. When Index == 1 and Padding == 3 => 0001). Default is 2
            /// </summary>
            public int Padding { get; set; }
            /// <summary>
            /// Constructor for IndexedLabel
            /// </summary>
            public IndexedLabel()
            {
                Index = 1;
                Padding = 2;
            }
        }
        /// <summary>
        /// Destination label for appending a date time stamp
        /// </summary>
        [Serializable]
        public class DateTimeStampedLabel : Label
        {
            /// <summary>
            /// Format used by DateTime class to generate date strings in the desired format. Default is yyyy-MM-dd HH-mm-ss
            /// </summary>
            public string Format { get; set; }
            /// <summary>
            /// Constructor for DateTimeStampedLabel
            /// </summary>
            public DateTimeStampedLabel()
            {
                Format = "yyyy-MM-dd HH-mm-ss";
            }
        }
        /// <summary>
        /// Information that defines a destination
        /// </summary>
        [Serializable]
        public class DestinationInfo
        {
            public DestinationInfo()
            {

            }
            /// <summary>
            /// Path to the destination directory
            /// </summary>
            public Path Path { get; set; }
            /// <summary>
            /// Labeling requirements for the destination
            /// </summary>
            public Label Label { get; set; }
        }
        /// <summary>
        /// Internal identifier
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// User defined alias
        /// </summary>
        public string Alias { get; set; }
        /// <summary>
        /// Path for source being backed up
        /// </summary>
        public Path Source { get; set; }
        /// <summary>
        /// List of destination information
        /// </summary>
        public List<DestinationInfo> Destinations { get; set; }
        /// <summary>
        /// For mapping to events
        /// </summary>
        public List<int> Triggers { get; set; }
        /// <summary>
        /// Toggle for user to activate or deactivate the operation
        /// </summary>
        public bool Enabled { get; set; } 
        /// <summary>
        /// Toggle for whether or not to copy sub directories
        /// </summary>
        public bool CopySubDirs { get; set; }
        /// <summary>
        /// Constructor for BackupOperation
        /// </summary>
        public BackupOperation()
        {
            Triggers = new List<int>();
            Source = new Path();
            Destinations = new List<DestinationInfo>();
        }


        /* List properties that require validation */
        static readonly string[] ValidatedProperties =
        {
            "Alias", "Source"
        };

        /* Leave empty */
        string IDataErrorInfo.Error
        {
            get
            {
                return null;
            }
        }

        /* Define validation logic for each property  */
        string IDataErrorInfo.this[string propertyName]
        {
            get
            {
                return GetValidationError(propertyName);
            }
        }

        /* Goes through all the properties that need validation  */
        public bool IsValid  {
            get {
                foreach (string property in ValidatedProperties)
                    if(GetValidationError(property) != null)
                    {
                        return false;
                    }

                return true;
            }    
        }

        #region Validation

        string GetValidationError(string propertyName)
        {
            string error = null;

            switch (propertyName)
            {
                case "Alias":
                    error = ValidateAlias();
                    break;
                case "Source":
                    error = ValidateSource();
                    break;
                case "Destinations":
                    error = ValidateDestinations();
                    break;
                    /* List other properties  */
            }


            return error; /* null means no validation error*/
        }

        private string ValidateDestinations()
        {
            string error = null;

            foreach (DestinationInfo di in Destinations) {
                /* TODO: Define the logic for the rest of the providers*/
                error = ValidateLocalPath(di.Path);
            }

           return error;
        }


        private string ValidateLocalPath(Path path)
        {
            string error = null;
            if (String.IsNullOrEmpty(path.AbsolutePath))  
            {
                error = "Source can't be empty";
            }
            else if (!(System.IO.Directory.Exists(path.AbsolutePath)))
            {
                error = "Invalid Source Path";
            }
            return error;
        }

        private string ValidateSource()
        {
            string error = null;
            /* TODO: Define validation logic for each provider*/
            error = ValidateLocalPath(Source);

            return error;
        }


        private string ValidateAlias()
        {
            string error = null;

            if (String.IsNullOrWhiteSpace(Alias))
            {
                error = "Alias can't be empty";
            }
            else
            {
                string[] UnallowedCharacters = { "!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "_", "=", "+", "{", "}" };
                bool hasInvalidChars = false;
                for (int i = 0; i < UnallowedCharacters.Length; i++)
                {
                    if (Alias.Contains(UnallowedCharacters[i]))
                    {
                        hasInvalidChars = true;
                        error = "Alias contains invalid characters";
                        break;
                    }
                }
            }
            return error;
        }
   
        #endregion


    }

    /// <summary>
    /// Contains detailed information regarding a path that is targeted for backup
    /// </summary>
    [Serializable]
    public class Path
    {
        /// <summary>
        /// User authentication information needed to access the path
        /// </summary>
        public Account UserAccount { get; set; }
        /// <summary>
        /// The service provider which hosts the file system that contains this path
        /// </summary>
        public ServiceProviders Provider { get; set; }
        /// <summary>
        /// Fully qualified path to directory. Leave empty to denote root directory
        /// </summary>
        public string AbsolutePath { get; set; }
        /// <summary>
        /// Identifier of parent directory if needed by the service provider API. Leave empty to denote root directory
        /// </summary>
        public string Parent { get; set; } 
        /// <summary>
        /// Copy-constructor for Path
        /// </summary>
        /// <param name="path">Path object being copied</param>
        public Path(Path path)
        {
            Provider = path.Provider;
            Parent = path.Parent;
            AbsolutePath = path.AbsolutePath;
            UserAccount = path.UserAccount;
        }
        /// <summary>
        /// Constructor for Path
        /// </summary>
        public Path() { }
    }

    /// <summary>
    /// Abstract base class defining user authentication details
    /// </summary>
    [Serializable]
    [XmlInclude(typeof(GoogleDriveAccount))]
    [XmlInclude(typeof(DropBoxAccount))]
    [XmlInclude(typeof(OneDriveAccount))]
    [XmlInclude(typeof(SSHAccount))]
    public abstract class Account
    {
        public Account()
        {

        }
    }

    /// <summary>
    /// User authentication details for the Google Drive service provider
    /// </summary>
    [Serializable]
    public class GoogleDriveAccount : Account
    {
        /// <summary>
        /// Google user identifier
        /// </summary>
        public string UserId { get; set; }
        /// <summary>
        /// Google user token
        /// </summary>
        public TokenResponse UserToken { get; set; } 
        /// <summary>
        /// Constructor for GoogleDriveAccount
        /// </summary>
        /// <param name="userId">Google user identifier</param>
        /// <param name="token">Google user token</param>
        public GoogleDriveAccount(string userId, TokenResponse token)
        {
            UserId = userId;
            UserToken = token;
        }
        public GoogleDriveAccount()
        {

        }
    }

    /// <summary>
    /// User authentication details for the DropBox service provider
    /// </summary>
    [Serializable]
    public class DropBoxAccount : Account
    {
        /// <summary>
        /// DropBox user token
        /// </summary>
        public string UserToken { get; set; }
        public DropBoxAccount()
        {

        }
    }

    /// <summary>
    /// User authentication details for the One Drive service provider
    /// </summary>
    [Serializable]
    public class OneDriveAccount : Account
    {
        public OneDriveAccount()
        {

        }
    }

    /// <summary>
    /// User authentication details for the SSH protocol
    /// </summary>
    [Serializable]
    public class SSHAccount : Account
    {
        public SSHAccount()
        {

        }
    }

    /// <summary>
    /// Describes a file using information that is necessary
    /// for destination agnostic backups to be performed
    /// </summary>
    public class FileDescription
    {
        /// <summary>
        /// Identifier of file if needed by the service provider API
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Name of file
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// File extension
        /// </summary>
        public string FileExtension { get; set; }
        /// <summary>
        /// MIME type of file
        /// </summary>
        public string MimeType { get; set; }
        /// <summary>
        /// Last time that the file was modified
        /// </summary>
        public DateTime LastWriteTime { get; set; }
    }

    /// <summary>
    /// Describes a directory using information that is necessary
    /// for destination agnostic backups to be performed
    /// </summary>
    public class DirectoryDescription
    {
        /// <summary>
        /// Name of directory
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Identifier of parent from source if needed by the service provider API.
        /// </summary>
        public string SourceParentId { get; set; }
        /// <summary>
        /// Identifer of parent from destination if needed by the service provider API.
        /// </summary>
        public string DestinationParentId { get; set; }
    }
}
