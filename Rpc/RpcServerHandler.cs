using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;

using More;

namespace More.Net.Rpc
{
    public class RpcServerConnectionHandler
    {
        public readonly RpcServerHandler server;
        public readonly Socket socket;
        public readonly RecordBuilder recordBuilder;
        public RpcServerConnectionHandler(RpcServerHandler server, Socket socket)
        {
            this.server = server;
            this.socket = socket;
            this.recordBuilder = new RecordBuilder(socket.SafeRemoteEndPointString(), server.HandleTcpRecord);
        }
        public void HandleData(SelectServerSharedBuffer server, Socket sock)
        {
            int bytesReceived;
            try
            {
                bytesReceived = sock.Receive(server.sharedBuffer);
            }
            catch (SocketException)
            {
                bytesReceived = -1;
            }
            if (bytesReceived <= 0)
            {
                sock.ShutdownSafe();
                server.DisposeAndRemoveReceiveSocket(sock);
                return;
            }

            recordBuilder.HandleData(socket, server.sharedBuffer, 0, (uint)bytesReceived);
        }
    }

    public abstract class RpcServerHandler
    {
        public readonly String serviceName;
        public readonly ByteArrayReference sendBuffer;

        public RpcServerHandler(String serviceName, ByteArrayReference sendBuffer)
        {
            this.serviceName = serviceName;
            this.sendBuffer = sendBuffer;
        }

        public abstract Boolean ProgramHeaderSupported(RpcProgramHeader programHeader);

        public abstract RpcReply Call(String clientString, RpcCall call,
            Byte[] callParameters, UInt32 callOffset, UInt32 callMaxOffset,
            out ISerializer replyParameters);

        public void AcceptCallback(SelectServerSharedBuffer server, Socket listenSock)
        {
            Socket newSocket = listenSock.Accept();
            RpcServerConnectionHandler connection = new RpcServerConnectionHandler(this, newSocket);
            server.AddReceiveSocket(newSocket, connection.HandleData);
        }

        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        public void DatagramRecvHandler(SelectServerSharedBuffer server, Socket sock)
        {
            int bytesReceived = sock.ReceiveFrom(server.sharedBuffer, ref from);
            if (bytesReceived <= 0)
            {
                if (bytesReceived < 0)
                {
                    throw new InvalidOperationException(String.Format("ReceiveFrom on UDP socket returned {0}", bytesReceived));
                }
                return; // TODO: how to handle neg
            }

            String clientString = "?";
            try
            {
                clientString = from.ToString();
            }
            catch (Exception) { }

            UInt32 parametersOffset;
            RpcMessage callMessage = new RpcMessage(server.sharedBuffer, 0, (uint)bytesReceived, out parametersOffset);

            if (callMessage.messageType != RpcMessageType.Call)
            {
                throw new InvalidOperationException(String.Format("Received an Rpc reply from '{0}' but only expecting Rpc calls", clientString));
            }
            if (!ProgramHeaderSupported(callMessage.call.programHeader))
            {
                new RpcMessage(callMessage.transmissionID, new RpcReply(RpcVerifier.None, RpcAcceptStatus.ProgramUnavailable)).SendUdp(from, sock, ref server.sharedBuffer, null);
                return;
            }

            ISerializer replyParameters;
            RpcReply reply = Call(clientString, callMessage.call, server.sharedBuffer, parametersOffset, (uint)bytesReceived, out replyParameters);

            if (reply != null)
            {
                new RpcMessage(callMessage.transmissionID, reply).SendUdp(from, sock, ref server.sharedBuffer, replyParameters);
            }
        }
        public void HandleTcpRecord(String clientString, Socket socket, Byte[] record, UInt32 recordOffset, UInt32 recordOffsetLimit)
        {
            UInt32 parametersOffset;
            RpcMessage callMessage = new RpcMessage(record, recordOffset, recordOffsetLimit, out parametersOffset);

            if (callMessage.messageType != RpcMessageType.Call)
            {
                throw new InvalidOperationException(String.Format("Received an Rpc reply from '{0}' but only expecting Rpc calls", clientString));
            }

            if (!ProgramHeaderSupported(callMessage.call.programHeader))
            {
                new RpcMessage(callMessage.transmissionID, new RpcReply(RpcVerifier.None, RpcAcceptStatus.ProgramUnavailable)).SendTcp(socket, sendBuffer, null);
            }
            else
            {
                ISerializer replyParameters;
                RpcReply reply = Call(clientString, callMessage.call, record, parametersOffset, recordOffsetLimit, out replyParameters);

                if (reply != null)
                {
                    new RpcMessage(callMessage.transmissionID, reply).SendTcp(socket, sendBuffer, replyParameters);
                }
            }
        }
    }
}
