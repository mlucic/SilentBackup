using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SilentBackupService.BackupOperation;

namespace SilentBackupService.Utility
{
    /// <summary>
    /// Path labeling utility intended for use on labeling root destination directories
    /// </summary>
    static class Labeler
    {
        public static void Label(Path path, Label label)
        {
            if (label is IndexedLabel)
            {
                foreach (int i in Enumerable.Range(0, (label as IndexedLabel).Padding - (label as IndexedLabel).Index.ToString().Length + 1))
                {
                    path.AbsolutePath += '0';
                }
                path.AbsolutePath += (label as IndexedLabel).Index++;
                BackupOperationManager.Instance.SaveConfig(); // Save change to the label information
            }
            else if (label is DateTimeStampedLabel)
            {
                path.AbsolutePath += '-' + DateTime.Now.ToString((label as DateTimeStampedLabel).Format);
            }
        }
    }
}
