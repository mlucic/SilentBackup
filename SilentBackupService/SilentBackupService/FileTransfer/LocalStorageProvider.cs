using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SilentBackupService.FileTransfer
{
    internal class LocalStorageProvider : IFileTransferProvider
    {
        private LocalStorageProvider() { }
        public static LocalStorageProvider Instance()
        {
            var provider = new LocalStorageProvider();
            return provider;
        }

        public async Task Dispatch(Path destination, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destination.AbsolutePath))
            {
                Directory.CreateDirectory(destination.AbsolutePath);
            }

            // Get directory info
            DirectoryInfo dir = new DirectoryInfo(destination.AbsolutePath);

            // Delete files that shouldn't exist anymore
            List<string> names = new List<string>();

            foreach (var item in fileDescToBytesMapping)
            {
                names.Add(item.Key.Name);
            }

            foreach (var item in dir.GetFiles())
            {
                if (!names.Contains(item.Name))
                {
                    // Remove file
                    System.IO.File.Delete(System.IO.Path.Combine(destination.AbsolutePath, item.Name));
                }
            }

            // Create/Update files
            foreach (var item in fileDescToBytesMapping)
            {
                string temppath = string.Join("", destination.AbsolutePath, "\\", item.Key.Name, item.Key.FileExtension);
                File.WriteAllBytes(temppath, item.Value);
            }
        }

        public async Task Fetch(Path source, bool copySubDirs, Dictionary<FileDescription, byte[]> fileDescToBytesMapping, List<DirectoryDescription> subDirectories)
        {
            DirectoryInfo dir = new DirectoryInfo(source.AbsolutePath);

            if (copySubDirs)
                foreach (var item in dir.GetDirectories())
                    subDirectories.Add(new DirectoryDescription() { Name = item.Name });

            FileInfo[] files = dir.GetFiles();

            foreach (FileInfo file in files)
            {
                byte[] bytes;
                FileDescription fd = new FileDescription();
                int index = file.Name.LastIndexOf('.'); // Get rid of file extension from name
                fd.Name = index == -1 ? file.Name : file.Name.Substring(0, index); // Get rid of file extension from name
                fd.FileExtension = file.Extension;
                fd.LastWriteTime = file.LastWriteTime;

                // Read bytes
                bytes = File.ReadAllBytes(System.IO.Path.Combine(source.AbsolutePath, file.Name));

                fd.MimeType = "application/unknown";
                string ext = System.IO.Path.GetExtension(System.IO.Path.Combine(source.AbsolutePath, file.Name).ToLower());
                Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
                if (regKey != null && regKey.GetValue("Content Type") != null)
                    fd.MimeType = regKey.GetValue("Content Type").ToString();

                fileDescToBytesMapping.Add(fd, bytes);
            }
        }
    }
}
