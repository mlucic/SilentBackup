using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.OneDrive.Sdk;
using Microsoft.Graph;
using Microsoft.OneDrive.Sdk.Authentication;

namespace SilentBackupService.FileTransfer
{
    class OneDriveProvider : IFileTransferProvider
    {
        private IOneDriveClient client;

        private OneDriveProvider()
        {
            client = null;
        }

        public static async Task<OneDriveProvider> Instance(OneDriveAccount acc)
        {
            var provider = new OneDriveProvider();
            await provider.Authenticate(acc);
            return provider;
        }

        private async Task Authenticate(OneDriveAccount acc)
        {
            string[] scopes = { "onedrive.readwrite", "wl.signin" };

            var msaAuthProvider = new MsaAuthenticationProvider(
            AppInfo.OneDriveClientId,
            /*"https://login.live.com/oauth20_desktop.srf"*/ null,
            scopes);

            await msaAuthProvider.AuthenticateUserAsync();
            if (!msaAuthProvider.IsAuthenticated) throw new Exception("Failed to authenticate One Drive client with credentials provided.");
            client = new OneDriveClient("https://api.onedrive.com/v1.0", msaAuthProvider);
        }

        public async Task Fetch(Path source, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            throw new NotImplementedException();
        }

        public async Task Dispatch(Path destination, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            throw new NotImplementedException();
        }
    }
}
