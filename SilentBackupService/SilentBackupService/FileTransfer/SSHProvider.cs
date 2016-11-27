using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilentBackupService.FileTransfer
{
    class SSHProvider : IFileTransferProvider
    {
        public static async Task<SSHProvider> Instance(SSHAccount acc)
        {
            throw new NotImplementedException();
        }

        public Task Dispatch(Path destination, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            throw new NotImplementedException();
        }

        public Task Fetch(Path source, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            throw new NotImplementedException();
        }
    }
}
