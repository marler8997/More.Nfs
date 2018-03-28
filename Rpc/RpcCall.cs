using System;

using More;

namespace More.Net.Rpc
{
    public class RpcCall : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new ClassFieldReflectors<RpcProgramHeader>(typeof(RpcCall), "programHeader", RpcProgramHeader.memberSerializers),
            new BigEndianUInt32Reflector              (typeof(RpcCall), "procedure"),
            new ClassFieldReflectors<RpcCredentials>  (typeof(RpcCall), "credentials", RpcCredentials.memberSerializers),
            new ClassFieldReflectors<RpcVerifier>     (typeof(RpcCall), "verifier"   , RpcVerifier.memberSerializers),
        });

        public readonly RpcProgramHeader programHeader;
        public readonly UInt32 procedure;
        public readonly RpcCredentials credentials;
        public readonly RpcVerifier verifier;

        public RpcCall()
            : base(memberSerializers)
        {
        }
        public RpcCall(RpcProgramHeader programHeader, UInt32 procedure,
            RpcCredentials credentials, RpcVerifier verifier)
            : base(memberSerializers)
        {
            this.programHeader = programHeader;
            this.procedure = procedure;
            this.credentials = credentials;
            this.verifier = verifier;
        }
        public RpcCall(Byte[] data, UInt32 offset, UInt32 maxOffset, out UInt32 newOffset)
            : base(memberSerializers)
        {
            newOffset = Deserialize(data, offset, maxOffset);
        }
    }
}
