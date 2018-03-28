using System;
using System.Collections.Generic;
using System.Text;

using More;

namespace More.Net.Rpc
{
    public enum RpcAuthenticationFlavor
    {
        None   = 0,
        System = 1,
        Short  = 2,
    }

    public class RpcCredentials : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new XdrEnumReflector           (typeof(RpcCredentials), "authenticationFlavor", typeof(RpcAuthenticationFlavor)),
            new XdrOpaqueVarLengthReflector(typeof(RpcCredentials), "body", 400),
        });

        public static RpcCredentials CreateUnixCredentials(RpcUnixCredentials unixCredentials)
        {
            Byte[] body = new Byte[unixCredentials.SerializationLength()];
            unixCredentials.Serialize(body, 0);
            return new RpcCredentials(RpcAuthenticationFlavor.System, body);
        }

        private static RpcCredentials none = null;
        public static RpcCredentials None
        {
            get
            {
                if (none == null)
                {
                    none = new RpcCredentials(RpcAuthenticationFlavor.None, new byte[0]);
                }
                return none;
            }
        }

        public RpcAuthenticationFlavor authenticationFlavor;
        public byte[] body;

        public RpcCredentials()
            : base(memberSerializers)
        {
        }
        public RpcCredentials(RpcAuthenticationFlavor authenticationFlavor, Byte[] body)
            : base(memberSerializers)
        {
            this.authenticationFlavor = authenticationFlavor;
            this.body = body;
        }
    }
    public class RpcUnixCredentials : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(RpcUnixCredentials), "stamp"),
            new XdrStringReflector(typeof(RpcUnixCredentials), "machineName", 255),
            new BigEndianUInt32Reflector(typeof(RpcUnixCredentials), "uid"),
            new BigEndianUInt32Reflector(typeof(RpcUnixCredentials), "gid"),
            new FixedLengthElementArrayReflector<UInt32>(typeof(RpcUnixCredentials), "auxilaryGids", 4, BigEndianUInt32Serializer.Instance ) // Max length is 16
        });

        public UInt32 stamp;
        public String machineName;
        public UInt32 uid;
        public UInt32 gid;
        public UInt32[] auxilaryGids;

        public RpcUnixCredentials(UInt32 stamp, String machineName, UInt32 uid, UInt32 gid, params UInt32[] auxilaryGids)
            : base(memberSerializers)
        {
            this.stamp = stamp;
            this.machineName = machineName;
            this.uid = uid;
            this.gid = gid;
            this.auxilaryGids = auxilaryGids;
        }

        public RpcUnixCredentials(Byte[] data, UInt32 offset, UInt32 maxOffset)
            : base(memberSerializers)
        {
            Deserialize(data, offset, maxOffset);
        }
    }
    public class RpcVerifier : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new XdrEnumReflector           (typeof(RpcVerifier), "authenticationFlavor", typeof(RpcAuthenticationFlavor)),
            new XdrOpaqueVarLengthReflector(typeof(RpcVerifier), "body", 400),
        });

        private static RpcVerifier none = null;
        public static RpcVerifier None
        {
            get
            {
                if (none == null)
                {
                    none = new RpcVerifier(RpcAuthenticationFlavor.None, new byte[0]);
                }
                return none;
            }
        }

        public readonly RpcAuthenticationFlavor authenticationFlavor;
        public byte[] body;

        public RpcVerifier()
            : base(memberSerializers)
        {
        }
        public RpcVerifier(RpcAuthenticationFlavor authenticationFlavor, Byte[] body)
            : base(memberSerializers)
        {
            this.authenticationFlavor = authenticationFlavor;
            this.body = body;
        }
    }
}
