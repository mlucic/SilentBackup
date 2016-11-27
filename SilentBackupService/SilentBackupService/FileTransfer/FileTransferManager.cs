using SilentBackupService.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SilentBackupService.BackupOperation;

namespace SilentBackupService.FileTransfer
{
    /// <summary>
    /// Manager for file transfer related tasks
    /// </summary>
    internal static class FileTransferManager
    {
        /// <summary>
        /// Method which copies files and folders from the source path and sends them to the destination path
        /// </summary>
        /// <param name="source">Source path</param>
        /// <param name="destination">Destination path</param>
        /// <param name="copySubDirs">Whether or not to copy sub directories</param>
        /// <param name="isRoot">Whether or not the current copy is at the root level</param>
        public static async Task Copy(Path source, Path destination, bool copySubDirs, bool isRoot)
        {
            Dictionary<FileDescription, byte[]> fileDescToBytesMapping = new Dictionary<FileDescription, byte[]>();
            List<DirectoryDescription> subDirectories = copySubDirs ? new List<DirectoryDescription>() : null;

            try
            {
                IFileTransferProvider provider = null;

                // Fetching process
                provider = await getProvider(source.Provider, source.UserAccount);
                await provider.Fetch(source, copySubDirs, fileDescToBytesMapping, subDirectories);

                // Dispatching process
                provider = await getProvider(destination.Provider, destination.UserAccount);
                await provider.Dispatch(destination, copySubDirs, fileDescToBytesMapping, subDirectories);

                fileDescToBytesMapping = null; // Dispose of files and bytes that have been used

                if (copySubDirs)
                {
                    CopySubDirs(source, destination, subDirectories, copySubDirs);
                }

                if (isRoot) // Once all the copies have been completed report the success
                {
                    ReportIO.WriteStatement("Backup between " + source.AbsolutePath + " and " + destination.AbsolutePath + " completed successfully.");
                }
            }
            catch (Exception e)
            {
                if (isRoot) // Report the error only at the root level, otherwise throw so root can catch
                {
                    ReportIO.WriteStatement("Backup between " + source.AbsolutePath + " and " + destination.AbsolutePath + " failed. Reason: " + e.Message);
                }
                else throw e;
            }
        }

        /// <summary>
        /// Copies all sub directories inside the source path
        /// </summary>
        /// <param name="source">Source path</param>
        /// <param name="destination">Destination path</param>
        /// <param name="subDirectories">List of directory descriptions for the subdirectories in the source</param>
        /// <param name="copySubDirs">Whether or not to copy sub directories</param>
        private static void CopySubDirs(Path source, Path destination, List<DirectoryDescription> subDirectories, bool copySubDirs)
        {
            foreach (DirectoryDescription subdir in subDirectories)
            {
                string pathStr = string.Concat(source.AbsolutePath, "\\", subdir.Name);

                Path srcPath = new Path();
                srcPath.AbsolutePath = string.Concat(source.AbsolutePath, "\\", subdir.Name);
                srcPath.Provider = source.Provider;
                srcPath.Parent = subdir.SourceParentId;
                srcPath.UserAccount = source.UserAccount;

                Path destPath = new Path();
                destPath.AbsolutePath = string.Concat(destination.AbsolutePath, "\\", subdir.Name);
                destPath.Provider = destination.Provider;
                destPath.Parent = subdir.DestinationParentId;
                destPath.UserAccount = destPath.UserAccount;

                Copy(srcPath, destPath, copySubDirs, false);
            }
        }

        /// <summary>
        /// Returns the appropriate authenticated provider based on the parameters
        /// </summary>
        /// <param name="providerType">Service provider</param>
        /// <param name="acc">User account</param>
        /// <returns></returns>
        private static async Task<IFileTransferProvider> getProvider(ServiceProviders providerType, Account acc)
        {
            IFileTransferProvider retval = null;
            switch (providerType)
            {
                case ServiceProviders.Local:
                    retval = LocalStorageProvider.Instance();
                    break;
                case ServiceProviders.Google:
                    retval = await GoogleDriveProvider.Instance(acc as GoogleDriveAccount);
                    break;
                case ServiceProviders.DropBox:
                    retval = await DropBoxProvider.Instance(acc as DropBoxAccount);
                    break;
                case ServiceProviders.OneDrive:
                    retval = await OneDriveProvider.Instance(acc as OneDriveAccount);
                    break;
                case ServiceProviders.SSH:
                    retval = await SSHProvider.Instance(acc as SSHAccount);
                    break;
            }
            return retval;
        }
    }
}