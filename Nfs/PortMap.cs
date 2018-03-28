using System;

using More;
using More.Net.Rpc;

namespace More.Net.Nfs
{
    public static class PortMap
    {
        public const String Name = "PortMap";
        public const UInt16 DefaultPort = 111;
        public const UInt32 ProgramNumber = 100000;

        public const UInt32 IPProtocolTcp = 6;
        public const UInt32 IPProtocolUdp = 17;
    }
    public static class PortMap2
    {
        public const UInt32 ProgramVersion = 2;

        public const UInt32 NULL    = 0;
        public const UInt32 SET     = 1;
        public const UInt32 UNSET   = 2;
        public const UInt32 GETPORT = 3;
        public const UInt32 DUMP    = 4;
        public const UInt32 CALLIT  = 5;
        public const UInt32 ProcedureNumberLimit = 4;

        private static RpcProgramHeader programHeader = null;
        public static RpcProgramHeader ProgramHeader
        {
            get
            {
                if (programHeader == null)
                {
                    programHeader = new RpcProgramHeader(RpcVersion.Two, PortMap.ProgramNumber, ProgramVersion);
                }
                return programHeader;
            }
        }
    }

    public class NamedMapping
    {
        public readonly String programName;
        public readonly Mapping mapping;
        public NamedMapping(String programName, Mapping mapping)
        {
            this.programName = programName;
            this.mapping = mapping;
        }
    }
    /*
    public class Mapping
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(Mapping), "program"),
            new BigEndianUInt32Reflector(typeof(Mapping), "version"),
            new BigEndianUInt32Reflector(typeof(Mapping), "protocol"),
            new BigEndianUInt32Reflector(typeof(Mapping), "port"),
        });
        public ISerializer CreateSerializer() { return new SerializerFromObjectAndReflectors(this, memberSerializers); }


        public UInt32 program, version, protocol, port;

        public Mapping(Byte[] data, UInt32 offset, UInt32 offsetLimit)
        {
            memberSerializers.Deserialize(this, data, offset, offsetLimit);
        }
        public Mapping(UInt32 program, UInt32 version, UInt32 protocol, UInt32 port)
        {
            this.program = program;
            this.version = version;
            this.protocol = protocol;
            this.port = port;
        }
    }
    */
}
namespace More.Net.Nfs.PortMap2Procedure
{
    /*
    //
    // GetPort Procedure
    //
    public class GetPortCall : ISerializerCreator
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(GetPortCall), "program"),
            new BigEndianUInt32Reflector(typeof(GetPortCall), "programVersion"),
            new BigEndianUInt32Reflector(typeof(GetPortCall), "transportProtocol"),
            new BigEndianUInt32Reflector(typeof(GetPortCall), "port"),
        });
        public ISerializer CreateSerializer() { return new SerializerFromObjectAndReflectors(this, memberSerializers); }

        public UInt32 program;
        public UInt32 programVersion;
        public UInt32 transportProtocol;
        public UInt32 port;

        public GetPortCall(Byte[] data, UInt32 offset, UInt32 offsetLimit)
        {
            memberSerializers.Deserialize(this, data, offset, offsetLimit);
        }
        public GetPortCall(UInt32 program, UInt32 programVersion, UInt32 transportProtocol, UInt32 port)
        {
            this.program = program;
            this.programVersion = programVersion;
            this.transportProtocol = transportProtocol;
            this.port = port;
        }
    }
    public class GetPortReply : ISerializerCreator
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(GetPortReply), "port"),
        });
        public ISerializer CreateSerializer() { return new SerializerFromObjectAndReflectors(this, memberSerializers); }

        public UInt32 port;
        public GetPortReply(Byte[] data, UInt32 offset, UInt32 offsetLimit)
        {
            memberSerializers.Deserialize(this, data, offset, offsetLimit);
        }
        public GetPortReply(UInt32 program, UInt32 programVersion, UInt32 transportProtocol, UInt32 port)
        {
            this.port = port;
        }
    }
    */
    //
    // Dump Procedure
    //
    public class MappingEntry
    {
        public static readonly Reflectors mappingSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(Mapping), "program"),
            new BigEndianUInt32Reflector(typeof(Mapping), "version"),
            new BigEndianUInt32Reflector(typeof(Mapping), "protocol"),
            new BigEndianUInt32Reflector(typeof(Mapping), "port"),
        });

        //
        // These serializers have to be created weirdly because is a some circular references in it
        //
        static IReflector NextMappingEntryReflectorCreator(Reflectors reflectorsReference)
        {
            return new XdrBooleanDescriminateReflector(

                new XdrBooleanReflector(typeof(MappingEntry), "haveNextMapping"),

                new Reflectors(new ClassFieldReflectors<MappingEntry>(typeof(MappingEntry), "nextMapping", reflectorsReference)),

                VoidReflector.Reflectors);
        }
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new ClassFieldReflectors<Mapping>(typeof(MappingEntry), "mapping", mappingSerializers),
            null // Placeholder for next mapping
        }, NextMappingEntryReflectorCreator);


        public Mapping mapping;
        public Boolean haveNextMapping;
        MappingEntry nextMapping;

        public MappingEntry(Mapping mapping)
        {
            this.mapping = mapping;
        }
        public MappingEntry NextMapping { get { return nextMapping; } }
        public void SetNextMapping(MappingEntry nextMapping)
        {
            if (nextMapping == null)
            {
                this.nextMapping = null;
                this.haveNextMapping = false;
            }
            else
            {
                this.nextMapping = nextMapping;
                this.haveNextMapping = true;
            }
        }
    }
    public class DumpReply : ISerializerCreator
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new XdrBooleanDescriminateReflector(
                new XdrBooleanReflector(typeof(DumpReply), "haveMappingList"),
                new Reflectors(new ClassFieldReflectors<MappingEntry>(typeof(DumpReply), "mappingList", MappingEntry.memberSerializers)),
                VoidReflector.Reflectors),

        });
        public ISerializer CreateSerializer() { return new SerializerFromObjectAndReflectors(this, memberSerializers); }

        public Boolean haveMappingList;
        public MappingEntry mappingList;

        public DumpReply()
        {
        }
        public DumpReply(Byte[] data, UInt32 offset, UInt32 offsetLimit)
        {
            memberSerializers.Deserialize(this, data, offset, offsetLimit);
        }
        public DumpReply(MappingEntry mappingList)
        {
            if (mappingList != null)
            {
                haveMappingList = true;
                this.mappingList = mappingList;
            }
        }
    }
}
