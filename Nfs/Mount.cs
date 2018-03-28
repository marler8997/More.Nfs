using System;
using System.Collections.Generic;

using More;
using More.Net.Rpc;

namespace More.Net.Nfs
{
    public static class Mount
    {
        public const String Name = "Mount";
        public const UInt16 DefaultPort = 59733;
        public const UInt32 ProgramNumber = 100005;

        
        public const UInt32 NULL   = 0;
        public const UInt32 MNT    = 1;
        public const UInt32 DUMP   = 2;
        public const UInt32 UMNT   = 3;
        public const UInt32 UMTALL = 4;
        public const UInt32 EXPORT = 5;
        public const UInt32 ProcedureNumberLimit = 6;

        
        public const Int32 MaxPathLength = 1024;
        public const UInt32 MaxNameLength  =  255;
        public const UInt32 FileHandleSize =   32;
    }
    public static class Mount1
    {
        public const UInt32 ProgramVersion = 1;

        private static RpcProgramHeader programHeader = null;
        public static RpcProgramHeader ProgramHeader
        {
            get
            {
                if (programHeader == null)
                {
                    programHeader = new RpcProgramHeader(RpcVersion.Two, Mount.ProgramNumber, ProgramVersion);
                }
                return programHeader;
            }
        }
    }
    public static class Mount3
    {
        public const UInt32 ProgramVersion = 3;

        private static RpcProgramHeader programHeader = null;
        public static RpcProgramHeader ProgramHeader
        {
            get
            {
                if (programHeader == null)
                {
                    programHeader = new RpcProgramHeader(RpcVersion.Two, Mount.ProgramNumber, ProgramVersion);
                }
                return programHeader;
            }
        }
    }

    //
    // Mount Procedure
    //
    public class MountCall : ISerializerCreator
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new XdrStringReflector(typeof(MountCall), "directory", Mount.MaxPathLength),
        });
        public ISerializer CreateSerializer() { return new SerializerFromObjectAndReflectors(this, memberSerializers); }

        public String directory;
        
        public MountCall(Byte[] data, UInt32 offset, UInt32 offsetLimit)
        {
            memberSerializers.Deserialize(this, data, offset, offsetLimit);
        }
        public MountCall(String directory)
        {
            this.directory = directory;
        }
    }
    public class Mount3Reply : ISerializerCreator
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new XdrDescriminatedUnionReflector<Nfs3Procedure.Status>(
                new XdrEnumReflector(typeof(Mount3Reply), "status", typeof(Nfs3Procedure.Status)),
                VoidReflector.ReflectorsArray,
                new XdrDescriminatedUnionReflector<Nfs3Procedure.Status>.KeyAndSerializer(Nfs3Procedure.Status.Ok, new IReflector[] {
                    new XdrOpaqueVarLengthReflector(typeof(Mount3Reply), "fileHandle", Mount.FileHandleSize),
                    new FixedLengthElementArrayReflector<RpcAuthenticationFlavor>(typeof(Mount3Reply), "authenticationFlavors", 4, BigEndianUnsignedEnumSerializer<RpcAuthenticationFlavor>.FourByteInstance),
                })
            ),
        });
        public ISerializer CreateSerializer() { return new SerializerFromObjectAndReflectors(this, memberSerializers); }

        public Nfs3Procedure.Status status;
        public Byte[] fileHandle;
        public RpcAuthenticationFlavor[] authenticationFlavors;

        public Mount3Reply(Byte[] data, UInt32 offset, UInt32 offsetLimit)
        {
            memberSerializers.Deserialize(this, data, offset, offsetLimit);
        }
        public Mount3Reply(Byte[] fileHandle, RpcAuthenticationFlavor[] authenticationFlavors)
        {
            this.status = Nfs3Procedure.Status.Ok;
            this.fileHandle = fileHandle;
            this.authenticationFlavors = authenticationFlavors;
        }
        public Mount3Reply(Nfs3Procedure.Status status)
        {
            if (status == Nfs3Procedure.Status.Ok)
                throw new InvalidOperationException("Wrong Constructor: The MountStatus for this constructor can not be Ok");
            this.status = status;
        }
    }

    //
    // Unmount Procedure
    //
    public class UnmountCall : ISerializerCreator
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new XdrStringReflector(typeof(UnmountCall), "directory", Mount.MaxPathLength),
        });
        public ISerializer CreateSerializer() { return new SerializerFromObjectAndReflectors(this, memberSerializers); }

        public String directory;

        public UnmountCall(Byte[] data, UInt32 offset, UInt32 offsetLimit)
        {
            memberSerializers.Deserialize(this, data, offset, offsetLimit);
        }
        public UnmountCall(String directory)
        {
            this.directory = directory;
        }
    }
}