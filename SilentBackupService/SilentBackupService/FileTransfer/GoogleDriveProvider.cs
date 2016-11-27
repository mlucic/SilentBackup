using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v2;
using Google.Apis.Drive.v2.Data;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SilentBackupService.FileTransfer
{
    internal class GoogleDriveProvider : IFileTransferProvider
    {
        public async static Task<GoogleDriveProvider> Instance(GoogleDriveAccount acc)
        {
            GoogleDriveProvider provider = new GoogleDriveProvider();
            await provider.Authenticate(acc);
            return provider;
        }

        private DriveService driveService;
        private GoogleDriveProvider()
        {
            driveService = null;
        }

        public async Task Authenticate(GoogleDriveAccount acc)
        {
            object threadlock = new Object();
            GoogleAuthorizationCodeFlow.Initializer init = new GoogleAuthorizationCodeFlow.Initializer();
            init.ClientSecrets = new ClientSecrets();
            init.ClientSecrets.ClientId = AppInfo.GoogleClientId;
            init.ClientSecrets.ClientSecret = AppInfo.GoogleClientSecret;
            GoogleAuthorizationCodeFlow f = new GoogleAuthorizationCodeFlow(init);
            Google.Apis.Auth.OAuth2.Responses.TokenResponse t = null;

            UserCredential credential = new UserCredential(f, acc.UserId, acc.UserToken);
            await credential.RefreshTokenAsync(CancellationToken.None);

            driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "SilentBackup",
            });
        }

        public async Task Fetch(Path source, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            if (driveService == null)
                throw new Exception("Google drive provider is not authenticated");

            DriveOptions drop = new DriveOptions();

            try
            {
                FilesResource.ListRequest list = driveService.Files.List();
                FileList filesFeed = null;
                list.MaxResults = 1000;
                var splitArr = source.AbsolutePath.Split('\\');
                drop.Title = splitArr == null ? source.AbsolutePath : splitArr.Last();
                drop.Parent = source.Parent;
                drop.MimeType = "application/vnd.google-apps.folder";
                list.Q = GetDriveQuery(drop);
                filesFeed = await list.ExecuteAsync();
                string parentId = filesFeed.Items[0].Id;
                list.Q = GetDriveQuery(new DriveOptions { Parent = parentId });

                filesFeed = await list.ExecuteAsync();

                while (filesFeed.Items != null)
                {
                    foreach (var file in filesFeed.Items)
                    {
                        if (file.MimeType != "application/vnd.google-apps.folder")
                        {
                            byte[] bytes = null;
                            FileDescription fd = new FileDescription();
                            try
                            {
                                fd.Name = file.Title;
                                fd.LastWriteTime = file.ModifiedDate ?? DateTime.MinValue;
                                fd.MimeType = file.MimeType;
                                fd.Id = file.Id;
                                bytes = driveService.HttpClient.GetByteArrayAsync(file.DownloadUrl).Result;
                                fileDescToBytesMapping.Add(fd, bytes);
                            }
                            catch (Exception ex)
                            {
                                throw ex;
                            }
                        }
                        else
                        {
                            if (copySubDirs)
                                subDirectories.Add(new DirectoryDescription() { SourceParentId = parentId, Name = file.Title });
                        }
                    }

                    if (filesFeed.NextPageToken == null)
                        break;

                    list.PageToken = filesFeed.NextPageToken;
                    filesFeed = await list.ExecuteAsync();
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public async Task Dispatch(Path destination, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            if (driveService == null)
                throw new Exception("Google drive provider is not authenticated");

            DriveOptions drop = new DriveOptions();
            try
            {
                FilesResource.ListRequest list = driveService.Files.List();
                FileList filesFeed = null;
                Google.Apis.Drive.v2.Data.File gDir = null;

                if (destination.AbsolutePath != "") // Destination is not root
                {
                    // Get directory
                    list.MaxResults = 1000;
                    drop.Parent = string.IsNullOrEmpty(destination.Parent) ? "root" : destination.Parent;
                    list.Q = GetDriveQuery(drop);

                    filesFeed = await list.ExecuteAsync();

                    // Try and locate the folder in google drive if it already exists
                    while (filesFeed.Items != null)
                    {
                        gDir = filesFeed.Items.SingleOrDefault(x => x.MimeType == "application/vnd.google-apps.folder" && x.Title == destination.AbsolutePath.Split('\\').Last());

                        if (filesFeed.NextPageToken == null) break; // Break if there are no more pages and the directory has not been found

                        list.PageToken = filesFeed.NextPageToken;
                        filesFeed = await list.ExecuteAsync();
                    }

                    // If the destination directory doesn't exist, create it.
                    if (gDir == null)
                    {
                        // Create directory
                        Google.Apis.Drive.v2.Data.File file = new Google.Apis.Drive.v2.Data.File();
                        file.Title = destination.AbsolutePath.Split('\\').Last();
                        file.MimeType = "application/vnd.google-apps.folder";
                        file.Parents = new List<ParentReference>();
                        ParentReference pr = new ParentReference() { Id = drop.Parent, IsRoot = drop.Parent == "root" };
                        file.Parents.Add(pr);

                        gDir = await driveService.Files.Insert(file).ExecuteAsync();
                    }

                    drop.Parent = gDir.Id;
                }
                else // Destination is root
                {
                    drop.Parent = "root";
                }

                // Remove all files/folders(and their contents) that don't exist in the source
                RemoveExtras(drop, fileDescToBytesMapping.Keys.ToList(), subDirectories);

                // Update/upload files
                foreach (var item in fileDescToBytesMapping)
                {
                    MemoryStream stream = new MemoryStream(item.Value);

                    list.Q = GetDriveQuery(new DriveOptions { Title = item.Key.Name, MimeType = item.Key.MimeType, Parent = gDir.Id });
                    var gFile = list.Execute().Items.Count > 0 ? list.Execute().Items[0] : null;
                    if (gFile == null)
                    {
                        if (item.Key.MimeType != "application/vnd.google-apps.folder")
                        {
                            // Create files
                            Google.Apis.Drive.v2.Data.File f = new Google.Apis.Drive.v2.Data.File();

                            f.MimeType = item.Key.MimeType;
                            f.Title = item.Key.Name;
                            f.Parents = new List<ParentReference>();
                            ParentReference pr = new ParentReference() { Id = gDir.Id };

                            if (drop.Parent == "root")
                                pr.IsRoot = true;
                            else
                                pr.IsRoot = false;

                            f.Parents.Add(pr);
                            var status = await driveService.Files.Insert(f, stream, f.MimeType).UploadAsync();
                        }
                    }
                    else
                    {
                        // Update files
                        if (item.Key.LastWriteTime > gFile.ModifiedDate)
                        {
                            var status = await driveService.Files.Update(gFile, gFile.Id, stream, gFile.MimeType).UploadAsync();
                        }
                    }
                }

                if (copySubDirs)
                {
                    foreach (var item in subDirectories)
                    {
                        item.DestinationParentId = gDir.Id;
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// This method recursively deletes a Google Drive directory and it's contents if they cannot be found in either the file or directory listing
        /// </summary>
        /// <param name="drop">Drive options of previous query</param>
        /// <param name="fileDescList"></param>
        /// <param name="subDirectories"></param>
        private async void RemoveExtras(DriveOptions drop, List<FileDescription> fileDescList, List<DirectoryDescription> subDirectories)
        {
            FilesResource.ListRequest list = driveService.Files.List();
            FileList filesFeed = null;

            list.MaxResults = 1000;
            list.Q = GetDriveQuery(drop);

            filesFeed = await list.ExecuteAsync();

            while (filesFeed.Items != null)
            {
                // Deleting files that shouldn't exist anymore
                List<string> names = new List<string>();

                if (fileDescList != null)
                {
                    foreach (var item in fileDescList) // Add names of files
                    {
                        names.Add(item.Name);
                    }
                }

                if (subDirectories != null)
                {
                    foreach (var item in subDirectories) // Add names of subdirectories
                    {
                        names.Add(item.Name);
                    }
                }

                var files = filesFeed.Items;

                foreach (var item in files)
                {
                    if (!names.Contains(item.Title))
                    {
                        // Recursively delete subdirectories
                        if (item.MimeType == "application/vnd.google-apps.folder")
                        {
                            RemoveExtras(new DriveOptions { Parent = item.Id }, null, null);
                        }
                        await driveService.Files.Delete(item.Id).ExecuteAsync();
                    }
                }

                if (filesFeed.NextPageToken == null)
                    break;

                list.PageToken = filesFeed.NextPageToken;
                filesFeed = await list.ExecuteAsync();
            }
        }

        private static string GetDriveQuery(DriveOptions drop)
        {
            string rc = ("trashed = false");

            if (!string.IsNullOrEmpty(drop.Title))
                rc += string.Concat(" and title = '", drop.Title, "'");

            if (!string.IsNullOrEmpty(drop.MimeType))
                rc += string.Concat(" and mimeType = '", drop.MimeType, "'");

            if (!string.IsNullOrEmpty(drop.Parent))
                rc += string.Concat(" and '", drop.Parent, "' in parents");

            return rc;
        }

        private class DriveOptions
        {
            public string Parent { get; set; }
            public string MimeType { get; set; }
            public string Title { get; set; }
        }
    }
}
