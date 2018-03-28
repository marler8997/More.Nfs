using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

using More;
using More.Net.Rpc;
using More.Net.Nfs.Nfs3Procedure;

namespace More.Net.Nfs
{
#if !WindowsCE
    //[NpcInterface]
#endif
    public interface INfs3Server
    {
        GetFileAttributesReply GETATTR(GetFileAttributesCall getFileAttributesCall);
        SetFileAttributesReply SETATTR(SetFileAttributesCall setFileAttributesCall);
        LookupReply LOOKUP(LookupCall lookupCall);
        AccessReply ACCESS(AccessCall accessCall);
        ReadReply READ(ReadCall readCall);
        WriteReply WRITE(WriteCall writeCall);
        CreateReply CREATE(CreateCall createCall);
        MkdirReply MKDIR(MkdirCall mkdirCall);
        SymLinkReply SYMLINK(SymLinkCall symLinkCall);
        RemoveReply REMOVE(RemoveCall removeCall);
        RenameReply RENAME(RenameCall renameCall);
        ReadDirPlusReply READDIRPLUS(ReadDirPlusCall readDirPlusCall);
        FileSystemStatusReply FSSTAT(FileSystemStatusCall fileSystemStatusCall);
        FSInfoReply FSINFO(FSInfoCall fsInfoCall);
    }
    
    public class Nfs3Server : RpcServerHandler, INfs3Server
    {
        /*
        public class NfsServerManager : INfs3ServerNiceInterface
        {
            readonly Nfs3Server server;
            public NfsServerManager(Nfs3Server server)
            {
                this.server = server;
            }
            public String[] RootShareNames()
            {
                RootShareDirectory[] shareDirectories = server.sharedFileSystem.rootShareDirectories;
                String[] shareNames = new String[shareDirectories.Length];
                for (int i = 0; i < shareDirectories.Length; i++)
                {
                    shareNames[i] = shareDirectories[i].shareName;
                }
                return shareNames;
            }
            public ShareObject[] ShareObjects()
            {
                return server.sharedFileSystem.CreateArrayOfShareObjects();
            }
            public FileSystemStatusReply FSStatusByName(String directory)
            {
                RootShareDirectory rootShareDirectory;
                ShareObject shareDirectoryObject;

                Status status = server.sharedFileSystem.TryGetDirectory(directory, out rootShareDirectory, out shareDirectoryObject);
                if (status != Status.Ok) return new FileSystemStatusReply(status, OptionalFileAttributes.None);

                return server.FSSTAT(new FileSystemStatusCall(shareDirectoryObject.fileHandleBytes));
            }
            public FSInfoReply FSInfoByName(String directory)
            {
                RootShareDirectory rootShareDirectory;
                ShareObject shareDirectoryObject;

                Status status = server.sharedFileSystem.TryGetDirectory(directory, out rootShareDirectory, out shareDirectoryObject);
                if (status != Status.Ok) return new FSInfoReply(status, OptionalFileAttributes.None);

                return server.FSINFO(new FSInfoCall(shareDirectoryObject.fileHandleBytes));
            }
            public ReadDirPlusReply ReadDirPlus(String directory, ulong cookie, uint maxDirectoryInfoBytes)
            {
                RootShareDirectory rootShareDirectory;
                ShareObject shareDirectoryObject;

                Status status = server.sharedFileSystem.TryGetDirectory(directory, out rootShareDirectory, out shareDirectoryObject);
                if (status != Status.Ok) return new ReadDirPlusReply(status, OptionalFileAttributes.None);

                //return new NonRecursiveReadDirPlusReply(server.READDIRPLUS(new ReadDirPlusCall(shareDirectoryObject.fileHandleBytes, cookie, null, maxDirectoryInfoBytes, UInt32.MaxValue)));
                return server.READDIRPLUS(new ReadDirPlusCall(shareDirectoryObject.fileHandleBytes, cookie, null, maxDirectoryInfoBytes, UInt32.MaxValue));
            }
        }
        */
        public static FileSystemStatusReply MakeFileSystemStatusReply(DriveInfo driveInfo, OptionalFileAttributes fileAttributes)
        {
            return new Nfs3Procedure.FileSystemStatusReply(fileAttributes,
                (UInt64)driveInfo.TotalSize,
                (UInt64)driveInfo.TotalFreeSpace,
                (UInt64)driveInfo.AvailableFreeSpace,
                99999999,
                99999999,
                99999999,
                0);
        }
        public static UInt32 ReadFile(FileInfo fileInfo, UInt64 fileOffset, Byte[] buffer, UInt32 maxBytesToRead, FileShare shareOptions, out Boolean reachedEndOfFile)
        {
            fileInfo.Refresh();

            UInt64 fileSize = (UInt64)fileInfo.Length;

            if (fileOffset >= fileSize)
            {
                reachedEndOfFile = true;
                return 0;
            }

            UInt64 fileSizeFromOffset = fileSize - fileOffset;

            UInt64 readLength;
            if (fileSizeFromOffset > (UInt64)maxBytesToRead)
            {
                reachedEndOfFile = false;
                readLength = maxBytesToRead;
            }
            else
            {
                reachedEndOfFile = true;
                readLength = fileSizeFromOffset;
            }
            
            if (readLength <= 0) return 0;

            // Check that readLength can be converted to Int32
            if ((0x7FFFFFFFUL & readLength) != readLength)
                throw new InvalidOperationException(String.Format("The desired read length is {0}, but casting it to an Int32 yeilds another value {1}",
                    readLength, 0x7FFFFFFF & readLength));
            Int32 readLengthAsInt32 = (Int32)readLength;


            using (FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, shareOptions))
            {
                fileStream.Position = (Int64)fileOffset;
                // TODO: what should I do if I can't read the entire length?
                fileStream.ReadFullSize(buffer, 0, readLengthAsInt32);
            }

            return (UInt32)readLengthAsInt32;
        }

        private readonly RpcServicesManager servicesManager;
        private readonly SharedFileSystem sharedFileSystem;
        private Slice<Byte> fileContents;
        private readonly UInt32 suggestedReadSizeMultiple;

        public Nfs3Server(RpcServicesManager servicesManager, SharedFileSystem sharedFileSystem, ByteArrayReference sendBuffer,
            UInt32 readSizeMax, UInt32 suggestedReadSizeMultiple)
            : base("Nfs3", sendBuffer)
        {
            this.servicesManager = servicesManager;
            this.sharedFileSystem = sharedFileSystem;
            this.fileContents.array = new Byte[readSizeMax];
            this.suggestedReadSizeMultiple = suggestedReadSizeMultiple;
        }

        public override Boolean ProgramHeaderSupported(RpcProgramHeader programHeader)
        {
            return programHeader.program == Nfs.ProgramNumber && programHeader.programVersion == 3;
        }
        public override RpcReply Call(String clientString, RpcCall call, byte[] callParameters, UInt32 callOffset, UInt32 callMaxOffset, out ISerializer replyParameters)
        {
            String nfsMethodName;
            ISerializer callData;
            Int32 extraPerfoamanceData = -1;

            Int64 beforeCall = Stopwatch.GetTimestamp();

            Boolean printCall = true, printReply = true;

            switch (call.procedure)
            {
                case (UInt32)Nfs3Command.NULL:
                    nfsMethodName = "NULL";

                    callData = VoidSerializer.Instance;
                    replyParameters = VoidSerializer.Instance;
                    break;
                case (UInt32)Nfs3Command.GETATTR:
                    nfsMethodName = "GETATTR";

                    GetFileAttributesCall getFileAttributesCall = new GetFileAttributesCall(callParameters, callOffset, callMaxOffset);
                    callData = getFileAttributesCall.CreateSerializer();

                    replyParameters = GETATTR(getFileAttributesCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.SETATTR:
                    nfsMethodName = "SETATTR";

                    SetFileAttributesCall setFileAttributesCall = new SetFileAttributesCall(callParameters, callOffset, callMaxOffset);
                    callData = setFileAttributesCall.CreateSerializer();

                    replyParameters = SETATTR(setFileAttributesCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.LOOKUP:
                    nfsMethodName = "LOOKUP";
                    printReply = false;

                    LookupCall lookupCall = new LookupCall(callParameters, callOffset, callMaxOffset);
                    callData = lookupCall.CreateSerializer();

                    replyParameters = LOOKUP(lookupCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.ACCESS:
                    nfsMethodName = "ACCESS";

                    AccessCall accessCall = new AccessCall(callParameters, callOffset, callMaxOffset);
                    callData = accessCall.CreateSerializer();

                    replyParameters = ACCESS(accessCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.READ:
                    nfsMethodName = "READ";
                    printReply = false;

                    ReadCall readCall = new ReadCall(callParameters, callOffset, callMaxOffset);
                    callData = readCall.CreateSerializer();

                    replyParameters = READ(readCall).CreateSerializer();

                    extraPerfoamanceData = (Int32)readCall.count;
                    break;
                case (UInt32)Nfs3Command.WRITE:
                    nfsMethodName = "WRITE";
                    printCall = false;

                    WriteCall writeCall = new WriteCall(callParameters, callOffset, callMaxOffset);
                    callData = writeCall.CreateSerializer();

                    replyParameters = WRITE(writeCall).CreateSerializer();

                    extraPerfoamanceData = (Int32)writeCall.count;
                    break;
                case (UInt32)Nfs3Command.CREATE:
                    nfsMethodName = "CREATE";

                    CreateCall createCall = new CreateCall(callParameters, callOffset, callMaxOffset);
                    callData = createCall.CreateSerializer();

                    replyParameters = CREATE(createCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.MKDIR:
                    nfsMethodName = "MKDIR";

                    MkdirCall mkdirCall = new MkdirCall(callParameters, callOffset, callMaxOffset);
                    callData = mkdirCall.CreateSerializer();

                    replyParameters = MKDIR(mkdirCall).CreateSerializer();

                    break;

                case (UInt32)Nfs3Command.SYMLINK:
                    nfsMethodName = "SYMLINK";

                    SymLinkCall symLinkCall = new SymLinkCall(callParameters, callOffset, callMaxOffset);
                    callData = symLinkCall.CreateSerializer();

                    replyParameters = SYMLINK(symLinkCall).CreateSerializer();
                    
                    break;

                case (UInt32)Nfs3Command.REMOVE:
                case (UInt32)Nfs3Command.RMDIR:
                    nfsMethodName = "REMOVE/RMDIR";

                    RemoveCall removeCall = new RemoveCall(callParameters, callOffset, callMaxOffset);
                    callData = removeCall.CreateSerializer();

                    replyParameters = REMOVE(removeCall).CreateSerializer();
                    break;

                case (UInt32)Nfs3Command.RENAME:
                    nfsMethodName = "RENAME";

                    RenameCall renameCall = new RenameCall(callParameters, callOffset, callMaxOffset);
                    callData = renameCall.CreateSerializer();

                    replyParameters = RENAME(renameCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.READDIRPLUS:
                    nfsMethodName = "READDIRPLUS";
                    printReply = false;

                    ReadDirPlusCall readDirPlusCall = new ReadDirPlusCall(callParameters, callOffset, callMaxOffset);
                    callData = readDirPlusCall.CreateSerializer();

                    replyParameters = READDIRPLUS(readDirPlusCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.FSSTAT:
                    nfsMethodName = "FSSTAT";

                    FileSystemStatusCall fileSystemInfoCall = new FileSystemStatusCall(callParameters, callOffset, callMaxOffset);
                    callData = fileSystemInfoCall.CreateSerializer();

                    replyParameters = FSSTAT(fileSystemInfoCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.FSINFO:
                    nfsMethodName = "FSINFO";

                    //
                    // Deserialize
                    //
                    FSInfoCall fsInfoCall = new FSInfoCall(callParameters, callOffset, callMaxOffset);
                    callData = fsInfoCall.CreateSerializer();

                    replyParameters = FSINFO(fsInfoCall).CreateSerializer();

                    break;
                case (UInt32)Nfs3Command.COMMIT:
                    nfsMethodName = "COMMIT";

                    // Since this server does not perform unstable writes at the moment, this function is unnecessary
                    CommitCall commitCall = new CommitCall(callParameters, callOffset, callMaxOffset);
                    callData = commitCall.CreateSerializer();

                    //replyParameters = Handle(commitCall).CreateSerializer();
                    CommitReply commitReply = new CommitReply(BeforeAndAfterAttributes.None, null);
                    replyParameters = commitReply.CreateSerializer();

                    break;
                default:
                    if (NfsServerLog.warningLogger != null)
                        NfsServerLog.warningLogger.WriteLine("[{0}] [Warning] client '{1}' sent unknown procedure number {2}", serviceName, clientString, call.procedure);
                    replyParameters = VoidSerializer.Instance;
                    return new RpcReply(RpcVerifier.None, RpcAcceptStatus.ProcedureUnavailable);
            }

            Int64 afterCall = Stopwatch.GetTimestamp();
            Int64 callStopwatchTicks = afterCall - beforeCall;
            if (NfsServerLog.rpcCallLogger != null)
            {
                NfsServerLog.rpcCallLogger.WriteLine(
#if WindowsCE
                    JediTimer.JediTimerPrefix() + 
#endif                    
                    "[{0}] {1} {2} => {3} {4:0.00} milliseconds", serviceName, nfsMethodName,
                    printCall ? DataStringBuilder.DataSmallString(callData, NfsServerLog.sharedDataStringBuilder) : "[Call Ommited From Log]",
                    printReply ? DataStringBuilder.DataSmallString(replyParameters, NfsServerLog.sharedDataStringBuilder) : "[Reply Ommited From Log]",
                    callStopwatchTicks.StopwatchTicksAsDoubleMilliseconds());
            }
            else if (NfsServerLog.warningLogger != null)
            {
                Double callMilliseconds = callStopwatchTicks.StopwatchTicksAsDoubleMilliseconds();
                if (callMilliseconds >= 40)
                {
                    NfsServerLog.warningLogger.WriteLine(
#if WindowsCE
JediTimer.JediTimerPrefix() +
#endif                    
                        "[{0}] [Warning] {1} {2} => {3} {4:0.00} milliseconds", serviceName, nfsMethodName,
                        printCall ? DataStringBuilder.DataSmallString(callData, NfsServerLog.sharedDataStringBuilder) : "[Call Ommited From Log]",
                        printReply ? DataStringBuilder.DataSmallString(replyParameters, NfsServerLog.sharedDataStringBuilder) : "[Reply Ommited From Log]",
                        callMilliseconds);
                }
            }

            if (NfsServerLog.performanceLog != null)
            {
                NfsServerLog.performanceLog.Log((Nfs3Command)call.procedure,
                    (UInt32)callStopwatchTicks.StopwatchTicksAsMicroseconds(), extraPerfoamanceData);
            }

            //servicesManager.PrintPerformance();
            return new RpcReply(RpcVerifier.None);
        }
        public GetFileAttributesReply GETATTR(GetFileAttributesCall getFileAttributesCall)
        {
            ShareObject shareObject;
            Status status = sharedFileSystem.TryGetSharedObject(getFileAttributesCall.handle, out shareObject);
            if (status != Status.Ok) return new GetFileAttributesReply(status);

            shareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            return new GetFileAttributesReply(shareObject.fileAttributes);
        }
        public SetFileAttributesReply SETATTR(SetFileAttributesCall setFileAttributesCall)
        {
            ShareObject shareObject;
            Status status = sharedFileSystem.TryGetSharedObject(setFileAttributesCall.fileHandle, out shareObject);
            if (status != Status.Ok) return new SetFileAttributesReply(status, BeforeAndAfterAttributes.None);

            shareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            SizeAndTimes before = AutoExtensions.CreateSizeAndTimes(shareObject.fileAttributes);

            // TODO: change the permissions

            return new SetFileAttributesReply(Status.Ok, new BeforeAndAfterAttributes(before, shareObject.fileAttributes));
        }
        public LookupReply LOOKUP(LookupCall lookupCall)
        {
            //
            // Get Directory Object
            //
            ShareObject directoryShareObject;
            Status status = sharedFileSystem.TryGetSharedObject(lookupCall.directoryHandle, out directoryShareObject);            
            if (status != Status.Ok) return new LookupReply(status, OptionalFileAttributes.None);

            if (directoryShareObject.fileType != FileType.Directory) return new LookupReply(Status.ErrorNotDirectory, OptionalFileAttributes.None);

            //
            // Get File
            //
            String localPathAndName = PlatformPath.LocalCombine(directoryShareObject.localPathAndName, lookupCall.fileName);
            ShareObject fileShareObject;
            sharedFileSystem.TryGetSharedObject(localPathAndName, lookupCall.fileName, out fileShareObject);

            if (status != Status.Ok) return new LookupReply(status, OptionalFileAttributes.None);
            if (fileShareObject == null) return new LookupReply(Status.ErrorNoSuchFileOrDirectory, OptionalFileAttributes.None);

            directoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            fileShareObject.RefreshFileAttributes(sharedFileSystem.permissions);

            return new LookupReply(fileShareObject.fileHandleBytes, directoryShareObject.optionalFileAttributes,
                fileShareObject.optionalFileAttributes);
        }
        public AccessReply ACCESS(AccessCall accessCall)
        {
            ShareObject shareObject;
            Status status = sharedFileSystem.TryGetSharedObject(accessCall.fileHandle, out shareObject);
            if (status != Status.Ok) return new AccessReply(status, OptionalFileAttributes.None);

            //
            // For now just give every all permissions
            //
            return new AccessReply(OptionalFileAttributes.None,
                AccessFlags.Delete | AccessFlags.Execute | AccessFlags.Extend |
                AccessFlags.Lookup | AccessFlags.Modify | AccessFlags.Read);
        }
        public ReadReply READ(ReadCall readCall)
        {
            ShareObject shareObject;
            Status status = sharedFileSystem.TryGetSharedObject(readCall.fileHandle, out shareObject);
            if (status != Status.Ok) return new ReadReply(status, OptionalFileAttributes.None);

            if(shareObject.fileType != FileType.Regular) return new ReadReply(Status.ErrorInvalidArgument, OptionalFileAttributes.None);

            if (readCall.count > (UInt32)fileContents.array.Length) return new ReadReply(Status.ErrorInvalidArgument, OptionalFileAttributes.None);

            Boolean reachedEndOfFile;
            UInt32 bytesRead;
            try
            {
                bytesRead = ReadFile(shareObject.AccessFileInfo(), readCall.offset, fileContents.array, readCall.count, FileShare.ReadWrite, out reachedEndOfFile);
            }
            catch (IOException)
            {
                //
                // Note: Linux does not support preventing a file open based on another process having the file open,
                //       so in turn, NFS does not have an error code to that effect.  The best error code fit seems to
                //       be and ACCESS error.
                //
                return new ReadReply(Status.ErrorAccess, OptionalFileAttributes.None);
            }

            fileContents.length = bytesRead;
            return new ReadReply(OptionalFileAttributes.None, (UInt32)bytesRead, reachedEndOfFile, fileContents);
        }
        public WriteReply WRITE(WriteCall writeCall)
        {
            ShareObject shareObject;
            Status status = sharedFileSystem.TryGetSharedObject(writeCall.fileHandle, out shareObject);
            if (status != Status.Ok) return new WriteReply(status, BeforeAndAfterAttributes.None);

            if (shareObject.fileType != FileType.Regular) return new WriteReply(Status.ErrorInvalidArgument, BeforeAndAfterAttributes.None);

            shareObject.RefreshFileAttributes(sharedFileSystem.permissions);

            SizeAndTimes sizeAndTimesBeforeWrite = AutoExtensions.CreateSizeAndTimes(shareObject.fileAttributes);

            FileInfo fileInfo = shareObject.AccessFileInfo();

            //
            // TODO: in the future in order to enhance performance, I could implement UNSTABLE writes,
            //       which means I could return the reply before the write is finished.
            //
            using (FileStream fileStream = fileInfo.Open(FileMode.Open))
            {
                fileStream.Position = (Int64)writeCall.offset;
                fileStream.Write(writeCall.data.array, (Int32)writeCall.data.offset, (Int32)writeCall.data.length);
            }

            shareObject.RefreshFileAttributes(sharedFileSystem.permissions);

            return new WriteReply(new BeforeAndAfterAttributes(sizeAndTimesBeforeWrite, shareObject.fileAttributes),
                writeCall.data.length, writeCall.stableHow, null);
        }
        public CreateReply CREATE(CreateCall createCall)
        {
            if(createCall.mode == CreateModeEnum.Exclusive)
                return new CreateReply(Status.ErrorNotSupported, BeforeAndAfterAttributes.None);

            ShareObject directoryShareObject;
            Status status = sharedFileSystem.TryGetSharedObject(createCall.directoryHandle, out directoryShareObject);
            if (status != Status.Ok) return new CreateReply(status, BeforeAndAfterAttributes.None);

            FileStream fileStream = null;
            try
            {
                String localPathAndName = PlatformPath.LocalCombine(directoryShareObject.localPathAndName, createCall.newFileName);

                ShareObject fileShareObject;
                status = sharedFileSystem.TryGetSharedObject(localPathAndName, createCall.newFileName, out fileShareObject);

                if (status == Nfs3Procedure.Status.Ok)
                {
                    fileShareObject.RefreshFileAttributes(sharedFileSystem.permissions);

                    // The file already exists
                    if (createCall.mode == CreateModeEnum.Guarded)
                        return new CreateReply(Status.ErrorAlreadyExists, BeforeAndAfterAttributes.None);
                }
                else 
                {
                    if(status != Nfs3Procedure.Status.ErrorNoSuchFileOrDirectory)
                        return new CreateReply(status, BeforeAndAfterAttributes.None);
                }

                directoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
                SizeAndTimes directorySizeAndTimesBeforeCreate = AutoExtensions.CreateSizeAndTimes(directoryShareObject.fileAttributes);

                // Todo: handle exceptions
                fileStream = new FileStream(localPathAndName, FileMode.Create);
                fileStream.Dispose();

                status = sharedFileSystem.TryGetSharedObject(localPathAndName, createCall.newFileName, out fileShareObject);
                if (status != Nfs3Procedure.Status.Ok) return new CreateReply(status, BeforeAndAfterAttributes.None);

                fileShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
                directoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);

                return new CreateReply(fileShareObject.optionalFileHandleClass,
                    fileShareObject.optionalFileAttributes,
                    new BeforeAndAfterAttributes(directorySizeAndTimesBeforeCreate, directoryShareObject.fileAttributes));
            }
            finally
            {
                if (fileStream != null) fileStream.Dispose();
            }
        }
        public MkdirReply MKDIR(MkdirCall mkdirCall)
        {
            ShareObject parentDirectoryShareObject;
            Status status = sharedFileSystem.TryGetSharedObject(mkdirCall.directoryHandle, out parentDirectoryShareObject);
            if (status != Status.Ok) return new MkdirReply(status, BeforeAndAfterAttributes.None);

            String localPathAndName = PlatformPath.LocalCombine(parentDirectoryShareObject.localPathAndName, mkdirCall.newDirectoryName);

            ShareObject mkdirDirectoryShareObject;
            status = sharedFileSystem.TryGetSharedObject(localPathAndName, mkdirCall.newDirectoryName, out mkdirDirectoryShareObject);
            if (status == Nfs3Procedure.Status.Ok) return new MkdirReply(Status.ErrorAlreadyExists, BeforeAndAfterAttributes.None);
            if (status != Nfs3Procedure.Status.ErrorNoSuchFileOrDirectory) return new MkdirReply(status, BeforeAndAfterAttributes.None);

            parentDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            SizeAndTimes directorySizeAndTimesBeforeCreate = AutoExtensions.CreateSizeAndTimes(parentDirectoryShareObject.fileAttributes);

            // Todo: handle exceptions
            Directory.CreateDirectory(localPathAndName);

            status = sharedFileSystem.TryGetSharedObject(localPathAndName, mkdirCall.newDirectoryName, out mkdirDirectoryShareObject);
            if (status != Nfs3Procedure.Status.Ok) return new MkdirReply(status, BeforeAndAfterAttributes.None);

            mkdirDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            parentDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);

            return new MkdirReply(mkdirDirectoryShareObject.optionalFileHandleClass,
                parentDirectoryShareObject.optionalFileAttributes,
                new BeforeAndAfterAttributes(directorySizeAndTimesBeforeCreate, parentDirectoryShareObject.fileAttributes));
        }
        public SymLinkReply SYMLINK(SymLinkCall symLinkCall)
        {
            ShareObject shareObject;
            Status status = sharedFileSystem.TryGetSharedObject(symLinkCall.linkToHandle, out shareObject);
            if (status != Status.Ok) return new SymLinkReply(status, BeforeAndAfterAttributes.None);

            //
            // Todo: implement this, for now just return an error
            //
            return new SymLinkReply(Status.ErrorNotSupported, BeforeAndAfterAttributes.None);
        }
        public RemoveReply REMOVE(RemoveCall removeCall)
        {
            ShareObject parentDirectoryShareObject;
            Status status = sharedFileSystem.TryGetSharedObject(removeCall.directoryHandle, out parentDirectoryShareObject);
            if (status != Status.Ok) return new RemoveReply(status, BeforeAndAfterAttributes.None);

            parentDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            SizeAndTimes directorySizeAndTimesBeforeCreate = AutoExtensions.CreateSizeAndTimes(parentDirectoryShareObject.fileAttributes);

            status = sharedFileSystem.RemoveFileOrDirectory(parentDirectoryShareObject.localPathAndName, removeCall.fileName);
            if (status != Status.Ok) return new RemoveReply(status, BeforeAndAfterAttributes.None);

            parentDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);

            return new RemoveReply(Status.Ok, new BeforeAndAfterAttributes(directorySizeAndTimesBeforeCreate,
                parentDirectoryShareObject.fileAttributes));
        }
        public RenameReply RENAME(RenameCall renameCall)
        {
            ShareObject oldParentDirectoryShareObject;
            ShareObject newParentDirectoryShareObject;

            Status status = sharedFileSystem.TryGetSharedObject(renameCall.oldDirectoryHandle, out oldParentDirectoryShareObject);
            if (status != Status.Ok) return new RenameReply(status, BeforeAndAfterAttributes.None, BeforeAndAfterAttributes.None);

            status = sharedFileSystem.TryGetSharedObject(renameCall.newDirectoryHandle, out newParentDirectoryShareObject);
            if (status != Status.Ok) return new RenameReply(status, BeforeAndAfterAttributes.None, BeforeAndAfterAttributes.None);


            oldParentDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            SizeAndTimes oldDirectorySizeAndTimesBeforeCreate = AutoExtensions.CreateSizeAndTimes(oldParentDirectoryShareObject.fileAttributes);

            newParentDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            SizeAndTimes newDirectorySizeAndTimesBeforeCreate = AutoExtensions.CreateSizeAndTimes(newParentDirectoryShareObject.fileAttributes);            
            
            status = sharedFileSystem.Move(oldParentDirectoryShareObject, renameCall.oldName,
                newParentDirectoryShareObject, renameCall.newName);
            if (status != Status.Ok) return new RenameReply(status, BeforeAndAfterAttributes.None, BeforeAndAfterAttributes.None);


            oldParentDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);
            newParentDirectoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);

            return new RenameReply(Status.Ok,
                new BeforeAndAfterAttributes(oldDirectorySizeAndTimesBeforeCreate, oldParentDirectoryShareObject.fileAttributes),
                new BeforeAndAfterAttributes(newDirectorySizeAndTimesBeforeCreate, newParentDirectoryShareObject.fileAttributes));
        }

        enum ReadDirPlusLoopState
        {
            Directories,
            Files,
        }
        public ReadDirPlusReply READDIRPLUS(ReadDirPlusCall readDirPlusCall)
        {
            ShareObject directoryShareObject;
            Status status = sharedFileSystem.TryGetSharedObject(readDirPlusCall.directoryHandle, out directoryShareObject);
            if (status != Status.Ok) return new ReadDirPlusReply(status, OptionalFileAttributes.None);

            UInt64 cookie = readDirPlusCall.cookie;

            EntryPlus lastEntry = null;
            GenericArrayBuilder<EntryPlus> entriesBuilder = new GenericArrayBuilder<EntryPlus>();

            UInt32 directoryInfoByteCount = 0;

            Boolean foundCookieObject;
            if (cookie > 0)
            {
                if (cookie == directoryShareObject.cookie)
                {
                    foundCookieObject = true;
                }
                else
                {
                    foundCookieObject = false;
                }
            }
            else
            {
                foundCookieObject = true; // This is the first call so there is no cookie to look for

                // Handle the '.' directory (will always be included if cookie is 0)
                if (lastEntry != null) lastEntry.IsNotLastEntry();
                lastEntry = new EntryPlus(
                    directoryShareObject.fileID,
                    ".",
                    directoryShareObject.cookie,
                    directoryShareObject.optionalFileAttributes,
                    directoryShareObject.optionalFileHandleClass);
                entriesBuilder.Add(lastEntry);

                directoryInfoByteCount += 16 + (UInt32)directoryShareObject.shareLeafName.Length;
            }

            FileType currentFileType = FileType.Directory;
            String[] objectNames = Directory.GetDirectories(directoryShareObject.localPathAndName);

            while (true)
            {
                for (int i = 0; i < objectNames.Length; i++)
                {
                    String objectName = objectNames[i];
                    ShareObject shareObject = sharedFileSystem.TryGetSharedObject(currentFileType, directoryShareObject.localPathAndName, objectName);
                    if (shareObject == null)
                    {
                        if (NfsServerLog.warningLogger != null)
                            NfsServerLog.warningLogger.WriteLine("[{0}] [Warning] Could not create or access share object for local directory '{1}'", serviceName, objectName);
                        continue;
                    }

                    if (!foundCookieObject)
                    {
                        if (shareObject.cookie == cookie)
                        {
                            foundCookieObject = true;
                        }
                    }
                    else
                    {
                        UInt32 entryInfoByteCount = 16 + (UInt32)shareObject.shareLeafName.Length;
                        if (directoryInfoByteCount + entryInfoByteCount > readDirPlusCall.maxDirectoryBytes)
                        {
                            return new ReadDirPlusReply(directoryShareObject.optionalFileAttributes, null, entriesBuilder.Build(), false);
                        }

                        directoryInfoByteCount += entryInfoByteCount;

                        shareObject.RefreshFileAttributes(sharedFileSystem.permissions);

                        if (lastEntry != null) lastEntry.IsNotLastEntry();
                        lastEntry = new EntryPlus(
                            shareObject.fileID,
                            shareObject.shareLeafName,
                            shareObject.cookie,
                            shareObject.optionalFileAttributes,//OptionalFileAttributes.None,
                            shareObject.optionalFileHandleClass);
                        entriesBuilder.Add(lastEntry);
                    }
                }

                if (currentFileType == FileType.Directory)
                {
                    objectNames = Directory.GetFiles(directoryShareObject.localPathAndName);
                    currentFileType = FileType.Regular;
                }
                else
                {
                    break;
                }
            }

            directoryShareObject.RefreshFileAttributes(sharedFileSystem.permissions);

            if (!foundCookieObject) return new ReadDirPlusReply(Status.ErrorBadCookie, directoryShareObject.optionalFileAttributes);

            return new ReadDirPlusReply(directoryShareObject.optionalFileAttributes, null, entriesBuilder.Build(), true);
        }


        public FileSystemStatusReply FSSTAT(FileSystemStatusCall fsStatCall)
        {
            RootShareDirectory rootShareDirectory;
            Nfs3Procedure.Status status = sharedFileSystem.TryGetRootSharedDirectory(fsStatCall.fileSystemRoot, out rootShareDirectory);
            if (status != Nfs3Procedure.Status.Ok) return new FileSystemStatusReply(status, OptionalFileAttributes.None);

            return MakeFileSystemStatusReply(rootShareDirectory.driveInfo, OptionalFileAttributes.None);
        }
        public FSInfoReply FSINFO(FSInfoCall fsInfoCall)
        {
            RootShareDirectory rootShareDirectory;
            Nfs3Procedure.Status status = sharedFileSystem.TryGetRootSharedDirectory(fsInfoCall.handle, out rootShareDirectory);
            if (status != Nfs3Procedure.Status.Ok) return new FSInfoReply(status, OptionalFileAttributes.None);

            return new FSInfoReply(
                OptionalFileAttributes.None,
                (UInt32)fileContents.array.Length, (UInt32)fileContents.array.Length, suggestedReadSizeMultiple,
                0x10000, 0x10000, 0x1000,
                0x1000,
#if WindowsCE
                0x100000000, // 4 Gigabytes
#else
                UInt64.MaxValue,
#endif
 1,
                0,
                FileProperties.Fsf3Link | FileProperties.Fsf3SymLink | FileProperties.Fsf3Homogeneous | FileProperties.Fsf3CanSetTime
                );
        }
    }
}