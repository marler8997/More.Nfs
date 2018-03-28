using System;
//using System.IO;

using More;

namespace More.Net.Nfs
{
    public class ShareObject
    {
        public readonly FileType fileType;
        public readonly UInt64 fileID;
        public readonly UInt64 cookie;

        public String localPathAndName;
        public String shareLeafName;
        private System.IO.FileInfo fileInfo;

        public readonly Byte[] fileHandleBytes;
        public readonly Nfs3Procedure.OptionalFileHandle optionalFileHandleClass;

        public FileAttributes fileAttributes;
        public Nfs3Procedure.OptionalFileAttributes optionalFileAttributes;

        public ShareObject(FileType fileType, UInt64 fileID, Byte[] fileHandleBytes, String localPathAndName, String shareLeafName)
        {
            this.fileType = fileType;
            this.fileID = fileID;
            this.cookie = (fileID == 0) ? UInt64.MaxValue : fileID; // A cookie value of 0 is not valid

            this.fileHandleBytes = fileHandleBytes;
            this.optionalFileHandleClass = new Nfs3Procedure.OptionalFileHandle(fileHandleBytes);

            this.localPathAndName = localPathAndName;
            SetShareLeafName(shareLeafName);
            this.fileInfo = null;


            this.fileAttributes = new FileAttributes();
            this.fileAttributes.fileType = fileType;
            this.fileAttributes.fileID = fileID;
            this.fileAttributes.fileSystemID = 0;
            if (fileType != FileType.Regular)
            {
                this.fileAttributes.fileSize = 0;
                this.fileAttributes.diskSize = 0;
            }
            this.fileAttributes.lastAccessTime                      = new Time();
            this.fileAttributes.lastModifyTime                      = new Time();
            this.fileAttributes.lastAttributeModifyTime             = new Time();

            if (fileType == FileType.Directory)
            {
                this.fileAttributes.fileSize = 4096;
                this.fileAttributes.diskSize = 4096;
            }

            this.optionalFileAttributes = new Nfs3Procedure.OptionalFileAttributes(fileAttributes);
        }
        public void UpdatePathAndName(String localPathAndName, String shareName)
        {
            this.localPathAndName = localPathAndName;
            SetShareLeafName(shareLeafName);
            this.fileInfo = null;
        }
        void SetShareLeafName(String shareLeafName)
        {
            if (PlatformPath.IsValidUnixFileName(shareLeafName))
            {
                this.shareLeafName = shareLeafName;
            }
            else
            {
                String newShareLeafName = NfsPath.LeafName(shareLeafName);
                if (!PlatformPath.IsValidUnixFileName(newShareLeafName))
                    throw new InvalidOperationException(String.Format("The file you supplied '{0}' is not a valid unix file name", shareLeafName));
                this.shareLeafName = newShareLeafName;
            }
        }

        public Nfs3Procedure.Status CheckStatus()
        {
            switch (fileType)
            {
                case FileType.Regular:
                    if (!System.IO.File.Exists(localPathAndName)) return Nfs3Procedure.Status.ErrorStaleFileHandle;
                    break;
                case FileType.Directory:
                    if (!System.IO.Directory.Exists(localPathAndName)) return Nfs3Procedure.Status.ErrorStaleFileHandle;
                    break;
            }
            return Nfs3Procedure.Status.Ok;
        }
        public System.IO.FileInfo AccessFileInfo()
        {
            if (fileInfo == null) fileInfo = new System.IO.FileInfo(localPathAndName);
            return fileInfo;
        }
        public Boolean RefreshFileAttributes(IPermissions permissions)
        {
            Boolean attributesChanged = false;

            if (fileInfo == null)
            {
                attributesChanged = true;
                fileInfo = new System.IO.FileInfo(localPathAndName);
            }
            else
            {
                fileInfo.Refresh();
            }

            //
            // Update file attributes
            //
            ModeFlags newPermissions = permissions.GetPermissions(this);
            if (newPermissions != fileAttributes.protectionMode)
            {
                attributesChanged = true;
                fileAttributes.protectionMode = newPermissions;
            }

            fileAttributes.hardLinks = (fileType == FileType.Directory) ?
                2U : 1U;

            fileAttributes.ownerUid = 0;
            fileAttributes.gid = 0;

            if (fileType == FileType.Regular)
            {
                UInt64 newFileSize = (UInt64)fileInfo.Length;
                if (fileAttributes.fileSize != newFileSize)
                {
                    attributesChanged = true;
                    fileAttributes.fileSize = newFileSize;
                }
                fileAttributes.diskSize = fileAttributes.fileSize;
            }

            fileAttributes.specialData1 = 0;
            fileAttributes.specialData2 = 0;

            {
                DateTime lastAccessDateTime = fileInfo.LastAccessTime;
                UInt32 newLastAccessTimeSeconds = lastAccessDateTime.ToUniversalTime().ToUnixTime();
                if (fileAttributes.lastAccessTime.seconds != newLastAccessTimeSeconds)
                {
                    attributesChanged = true;
                    fileAttributes.lastAccessTime.seconds = newLastAccessTimeSeconds;
                }
            }
            {
                DateTime lastModifyTime = fileInfo.LastWriteTime;
                UInt32 newLastModifyTimeSeconds = lastModifyTime.ToUniversalTime().ToUnixTime();
                if (fileAttributes.lastModifyTime.seconds != newLastModifyTimeSeconds)
                {
                    attributesChanged = true;
                    fileAttributes.lastModifyTime.seconds = newLastModifyTimeSeconds;
                }
            }

            if (attributesChanged)
            {
                fileAttributes.lastAttributeModifyTime.seconds =
                    (fileAttributes.lastAccessTime.seconds > fileAttributes.lastModifyTime.seconds) ?
                    fileAttributes.lastAccessTime.seconds : fileAttributes.lastModifyTime.seconds;
            }

            return attributesChanged;
        }
        public override String ToString()
        {
            return String.Format("Type '{0}' ID '{1}' LocalPathAndName '{2}' Handle '{3}'",
                fileType, fileID, localPathAndName, BitConverter.ToString(fileHandleBytes));
        }
    }
}