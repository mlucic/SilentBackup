using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SilentBackupService.FileTransfer
{
    class DropBoxProvider : IFileTransferProvider
    {
        private DropboxRestAPI.Client client;

        private DropBoxProvider()
        {
            client = null;
        }

        public async static Task<DropBoxProvider> Instance(DropBoxAccount acc)
        {
            DropBoxProvider provider = new DropBoxProvider();
            await provider.Authenticate(acc);
            return provider;
        }

        private async Task Authenticate(DropBoxAccount acc)
        {
            var options = new DropboxRestAPI.Options
            {
                ClientId = AppInfo.DropBoxAppId,
                ClientSecret = AppInfo.DropBoxAppSecret,
                AccessToken = acc.UserToken
            };

            client = new DropboxRestAPI.Client(options);
        }

        public async Task Dispatch(Path destination, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            throw new NotImplementedException();
        }

        public async Task Fetch(Path source, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            var folder = await client.Core.Metadata.MetadataAsync("\\" + source.AbsolutePath, list: true);

            foreach (var item in folder.contents)
            {
                if (item.is_dir)
                {
                    var dd = new DirectoryDescription() { Name = item.Name };
                    subDirectories.Add(dd);
                }
                else
                {
                    var fd = new FileDescription();
                    fd.Name = item.Name;
                    fd.LastWriteTime = DateTime.Parse(item.modified);
                    fd.MimeType = item.mime_type;
                    fd.FileExtension = item.Extension;

                    var tempFile = System.IO.Path.GetTempFileName();
                    using (var fileStream = System.IO.File.OpenWrite(tempFile))
                    {
                        await client.Core.Metadata.FilesAsync(item.path, fileStream);
                    }
                    var bytes = System.IO.File.ReadAllBytes(tempFile);
                    System.IO.File.Delete(tempFile);

                    fileDescToBytesMapping.Add(fd, bytes);
                }
            }
        }
    }
}
