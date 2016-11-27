using System.Collections.Generic;
using System.Threading.Tasks;

namespace SilentBackupService.FileTransfer
{
    //    Provider creation flow:

    //      1. Authenticate user
    //      2. Return provider object

    //    Fetch flow:

    //      1. Get the content of the folder
    //      2a.If it is a file, download the file and store its description and bytes into the dictionary of file descriptions and bytes
    //      2b.If it is a folder and the user wishes to copy sub directories, store the directory information into the list of directory descriptions

    //    Dispatch flow: 

    //      1. Check if the directory exists, if not, create it
    //      2. Remove any files that don't exist in the source (this should potentially become a user controlled option)
    //      3. Create/Update files


    /// <summary>
    /// Describes the neccessary methods for a file transfer provider
    /// </summary>
    internal interface IFileTransferProvider
    {
        Task Fetch(Path source,
                   bool copySubDirs,
                   Dictionary<FileDescription, byte[]> fileDescToBytesMapping,
                   List<DirectoryDescription> subDirectories);
        Task Dispatch(Path destination,
                      bool copySubDirs,
                      Dictionary<FileDescription, byte[]> fileDescToBytesMapping,
                      List<DirectoryDescription> subDirectories);
    }
}
