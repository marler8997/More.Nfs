using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Diagnostics;
using System.Net;

using More;

namespace More.Net.Rpc
{
    public enum RpcVersion
    {
        One = 1,
        Two = 2,
    }
    public class RpcProgramHeader : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(RpcProgramHeader), "rpcVersion"),
            new BigEndianUInt32Reflector(typeof(RpcProgramHeader), "program"),
            new BigEndianUInt32Reflector(typeof(RpcProgramHeader), "programVersion"),
        });

        public readonly UInt32 rpcVersion;
        public readonly UInt32 program;
        public readonly UInt32 programVersion;

        public RpcProgramHeader()
            : base(memberSerializers)
        {
        }
        public RpcProgramHeader(RpcVersion rpcVersion, UInt32 program, UInt32 programVersion)
            : base(memberSerializers)
        {
            this.rpcVersion = (UInt32)rpcVersion;
            this.program = program;
            this.programVersion = programVersion;
        }
    }
    public enum RpcMessageType
    {
        Call  = 0,
        Reply = 1,
    };
    public class RpcMessage : SubclassSerializer
    {
        public static readonly Reflectors memberSerializers = new Reflectors(new IReflector[] {
            new BigEndianUInt32Reflector(typeof(RpcMessage), "transmissionID"),

            new XdrDescriminatedUnionReflector<RpcMessageType>(
                new XdrEnumReflector(typeof(RpcMessage), "messageType", typeof(RpcMessageType)),
                null,                
                new XdrDescriminatedUnionReflector<RpcMessageType>.KeyAndSerializer(RpcMessageType.Call, new IReflector[] {
                    new ClassFieldReflectors<RpcCall>(typeof(RpcMessage), "call", RpcCall.memberSerializers)}),
                new XdrDescriminatedUnionReflector<RpcMessageType>.KeyAndSerializer(RpcMessageType.Reply, new IReflector[] {
                    new ClassFieldReflectors<RpcReply>(typeof(RpcMessage), "reply", RpcReply.memberSerializers)})
            ),
        });

        public UInt32 transmissionID;
        public RpcMessageType messageType;
        public RpcCall call;
        public RpcReply reply;

        public RpcMessage()
            : base(memberSerializers)
        {
        }

        public RpcMessage(UInt32 transmissionID, RpcCall call)
            : base(memberSerializers)
        {
            this.transmissionID = transmissionID;
            this.messageType = RpcMessageType.Call;
            this.call = call;
        }

        public RpcMessage(UInt32 transmissionID, RpcReply reply)
            : base(memberSerializers)
        {
            this.transmissionID = transmissionID;
            this.messageType = RpcMessageType.Reply;
            this.reply = reply;
        }
        public RpcMessage(Socket socket, ByteArrayReference buffer, out UInt32 contentOffset, out UInt32 contentOffsetLimit)
            : base(memberSerializers)
        {
            //
            // TODO: catch socket exception to prevent server from failing
            //

            buffer.EnsureCapacityCopyAllData(12);
            socket.ReadFullSize(buffer.array, 0, 4); // read the size
            Int32 rpcMessageSize = (
                (0x7F000000 & (buffer.array[0] << 24)) |
                (0x00FF0000 & (buffer.array[1] << 16)) |
                (0x0000FF00 & (buffer.array[2] <<  8)) |
                (0x000000FF & (buffer.array[3]      )) );

            if ((buffer.array[0] & 0x80) != 0x80)
                throw new NotImplementedException(String.Format("Records with multiple fragments it not currently implemented"));

            buffer.EnsureCapacityCopyAllData((UInt32)rpcMessageSize);
            socket.ReadFullSize(buffer.array, 0, rpcMessageSize);

            //
            // Deserialize
            //
            contentOffsetLimit = (UInt32)rpcMessageSize;

            if (RpcPerformanceLog.rpcMessageSerializationLogger != null) RpcPerformanceLog.StartSerialize();
            contentOffset = Deserialize(buffer.array, 0, (UInt32)rpcMessageSize);
            if (RpcPerformanceLog.rpcMessageSerializationLogger != null) RpcPerformanceLog.StopSerializationAndLog("RpcDeserializationTime");
        }
        public RpcMessage(Byte[] data, UInt32 offset, UInt32 offsetLimit, out UInt32 contentOffset)
            : base(memberSerializers)
        {
            if (RpcPerformanceLog.rpcMessageSerializationLogger != null) RpcPerformanceLog.StartSerialize();
            contentOffset = Deserialize(data, offset, offsetLimit);
            if (RpcPerformanceLog.rpcMessageSerializationLogger != null) RpcPerformanceLog.StopSerializationAndLog("RpcDeserializationTime");
        }
        public void SendTcp(Socket socket, ByteArrayReference buffer, ISerializer messageContents)
        {
            UInt32 messageContentLength = (messageContents == null) ? 0 : messageContents.SerializationLength();
            UInt32 totalMessageLength = SerializationLength() + messageContentLength;

            buffer.EnsureCapacityNoCopy(4 + totalMessageLength); // Extra 4 bytes for the record header

            if (RpcPerformanceLog.rpcMessageSerializationLogger != null) RpcPerformanceLog.StartSerialize();
            UInt32 offset = Serialize(buffer.array, 4);
            if (messageContents != null)
            {
                offset = messageContents.Serialize(buffer.array, offset);
            }
            if (RpcPerformanceLog.rpcMessageSerializationLogger != null) RpcPerformanceLog.StopSerializationAndLog("RpcSerializationTime");

            if (offset != totalMessageLength + 4)
                throw new InvalidOperationException(String.Format("[CodeBug] The caclulated serialization length of RpcMessage '{0}' was {1} but actual size was {2}",
                    DataStringBuilder.DataString(this, new StringBuilder()), totalMessageLength, offset));

            //
            // Insert the record header
            //
            buffer.array[0] = (Byte)(0x80 | (totalMessageLength >> 24));
            buffer.array[1] = (Byte)(        totalMessageLength >> 16 );
            buffer.array[2] = (Byte)(        totalMessageLength >>  8 );
            buffer.array[3] = (Byte)(        totalMessageLength       );

            socket.Send(buffer.array, 0, (Int32)(totalMessageLength + 4), SocketFlags.None);
        }
        public void SendUdp(EndPoint endPoint, Socket socket, ref Byte[] buffer, ISerializer messageContents)
        {
            UInt32 messageContentLength = (messageContents == null) ? 0 : messageContents.SerializationLength();
            UInt32 totalMessageLength = SerializationLength() + messageContentLength;

            ArrayExt.EnsureCapacityNoCopy(ref buffer, totalMessageLength);

            if (RpcPerformanceLog.rpcMessageSerializationLogger != null) RpcPerformanceLog.StartSerialize();
            UInt32 offset = Serialize(buffer, 0);
            if (messageContents != null)
            {
                offset = messageContents.Serialize(buffer, offset);
            }
            if (RpcPerformanceLog.rpcMessageSerializationLogger != null) RpcPerformanceLog.StopSerializationAndLog("RpcSerializationTime");

            if (offset != totalMessageLength)
                throw new InvalidOperationException(String.Format("[CodeBug] The caclulated serialization length of RpcMessage '{0}' was {1} but actual size was {2}",
                    DataStringBuilder.DataString(this, new StringBuilder()), totalMessageLength, offset));

            socket.SendTo(buffer, 0, (Int32)totalMessageLength, SocketFlags.None, endPoint);
        }
    }
}
