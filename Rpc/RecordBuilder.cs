using System;
using System.Collections.Generic;
using System.Net.Sockets;

#if WindowsCE
using ArrayCopier = System.MissingInCEArrayCopier;
#else
using ArrayCopier = System.Array;
#endif

namespace More.Net.Rpc
{
    public class RecordBuilder
    {
        public delegate void RecordHandler(String clientString, Socket socket, Byte[] bytes, UInt32 offset, UInt32 length);
        readonly RecordHandler recordHandler;

        enum State
        {
            Initial,
            PartialLengthReceived,
            LengthReceived
        };

        readonly String clientString;
        State state;
        Byte[] copiedFragmentData;
        UInt32 copiedFramentDataLength;

        public RecordBuilder(String clientString, RecordHandler recordHandler)
        {
            this.clientString = clientString;
            this.state = State.Initial;
            this.recordHandler = recordHandler;
        }
        public void Reset()
        {
            this.copiedFragmentData = null;
            this.copiedFramentDataLength = 0;
        }

        public void TcpSocketRecvCallback(SelectServerSharedBuffer server, Socket socket)
        {
            int bytesRead;
            try
            {
                bytesRead = socket.Receive(server.sharedBuffer);
            }
            catch (SocketException)
            {
                bytesRead = -1;
            }
            if (bytesRead <= 0)
            {
                server.RemoveReceiveSocket(socket);
                return;
            }
            HandleData(socket, server.sharedBuffer, 0, (uint)bytesRead);
        }

        // This function is highly tested
        public void HandleData(Socket socket, Byte[] bytes, UInt32 offset, UInt32 offsetLimit)
        {
            switch (state)
            {
                case State.Initial:
                    {
                        while (offset < offsetLimit)
                        {
                            //
                            // Only a few bytes of the length were received
                            //
                            if (offsetLimit - offset < 4)
                            {
                                copiedFramentDataLength = offsetLimit - offset;
                                copiedFragmentData = new Byte[4];
                                for (int i = 0; i < copiedFramentDataLength; i++)
                                {
                                    this.copiedFragmentData[i] = bytes[offset + i];
                                }
                                state = State.PartialLengthReceived;
                                return;
                            }

                            Boolean isLastFragment = (bytes[offset] & 0x80) == 0x80;
                            if (!isLastFragment) throw new NotSupportedException("Multifragment records are not supported");

                            Int32 fragmentLength =
                                (0x7F000000 & (bytes[offset] << 24)) |
                                (0x00FF0000 & (bytes[offset + 1] << 16)) |
                                (0x0000FF00 & (bytes[offset + 2] << 8)) |
                                (0x000000FF & (bytes[offset + 3]));

                            offset += 4;

                            UInt32 fragmentBytesAvailable = offsetLimit - offset;

                            if (fragmentBytesAvailable < fragmentLength)
                            {
                                this.copiedFragmentData = new Byte[fragmentLength];
                                ArrayCopier.Copy(bytes, offset, copiedFragmentData, 0, fragmentBytesAvailable);
                                this.copiedFramentDataLength = fragmentBytesAvailable;
                                state = State.LengthReceived;
                                return;
                            }

                            recordHandler(clientString, socket, bytes, offset, (UInt32)fragmentLength);
                            offset += (UInt32)fragmentLength;
                        }
                    }
                    return;
                case State.PartialLengthReceived:
                    {
                        while (true)
                        {
                            copiedFragmentData[copiedFramentDataLength] = bytes[offset];
                            offset++;
                            if (copiedFramentDataLength == 3) break;
                            copiedFramentDataLength++;
                            if (offset >= offsetLimit) return;
                        }

                        Boolean isLastFragment = (copiedFragmentData[0] & 0x80) == 0x80;
                        if (!isLastFragment) throw new NotSupportedException("Multifragment records are not supported");

                        Int32 fragmentLength =
                            (0x7F000000 & (copiedFragmentData[0] << 24)) |
                            (0x00FF0000 & (copiedFragmentData[1] << 16)) |
                            (0x0000FF00 & (copiedFragmentData[2] << 8)) |
                            (0x000000FF & (copiedFragmentData[3]));

                        UInt32 fragmentBytesAvailable = offsetLimit - offset;

                        if (fragmentBytesAvailable < fragmentLength)
                        {
                            this.copiedFragmentData = new Byte[fragmentLength];
                            ArrayCopier.Copy(bytes, offset, copiedFragmentData, 0, fragmentBytesAvailable);
                            this.copiedFramentDataLength = fragmentBytesAvailable;
                            state = State.LengthReceived;
                            return;
                        }

                        recordHandler(clientString, socket, bytes, offset, (UInt32)fragmentLength);
                        offset += (UInt32)fragmentLength;

                        state = State.Initial;
                        goto case State.Initial;
                    }
                case State.LengthReceived:
                    {
                        UInt32 fragmentBytesAvailable = offsetLimit - offset;
                        UInt32 fragmentBytesNeeded = (UInt32)copiedFragmentData.Length - copiedFramentDataLength;

                        if (fragmentBytesAvailable < fragmentBytesNeeded)
                        {
                            ArrayCopier.Copy(bytes, offset, copiedFragmentData, copiedFramentDataLength, fragmentBytesAvailable);
                            copiedFramentDataLength += fragmentBytesAvailable;
                            return;
                        }
                        else
                        {
                            ArrayCopier.Copy(bytes, offset, copiedFragmentData, copiedFramentDataLength, fragmentBytesNeeded);

                            recordHandler(clientString, socket, copiedFragmentData, 0, (UInt32)copiedFragmentData.Length);
                            offset += fragmentBytesNeeded;

                            this.copiedFragmentData = null;

                            this.state = State.Initial;
                            goto case State.Initial;
                        }
                    }
            }
        }
    }
}
