using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;

namespace More.Net.Nfs
{
    public class ControlServer
    {
        public void AcceptCallback(SelectServerSharedBuffer server, Socket listenSock)
        {
            Socket newSock = listenSock.Accept();

            DebugClientData clientData = new DebugClientData(newSock);
            server.AddReceiveSocket(newSock, clientData.DataCallback);
            clientData.SendPrompt();
        }
        class DebugClientData
        {
            public readonly LineParser lineParser;
            public readonly StreamWriter writer;
            public DebugClientData(Socket socket)
            {
                this.lineParser = new LineParser(Encoding.ASCII, 64);
                this.writer = new StreamWriter(new NetworkStream(socket));
            }
            public void SendPrompt()
            {
                writer.Write("NfsServerDebug> ");
                writer.Flush();
            }
            public void DataCallback(SelectServerSharedBuffer server, Socket socket)
            {
                int bytesReceived;
                try
                {
                    bytesReceived = socket.Receive(server.sharedBuffer);
                }
                catch (SocketException)
                {
                    bytesReceived = -1;
                }
                if (bytesReceived <= 0)
                {
                    socket.ShutdownSafe();
                    server.DisposeAndRemoveReceiveSocket(socket);
                }

                lineParser.Add(server.sharedBuffer, 0, (uint)bytesReceived);

                while (true)
                {
                    String line = lineParser.GetLine();
                    if (line == null) break;


                    if (line[0] == 'd' || line[0] == 'D')
                    {
                        if (NfsServerLog.performanceLog == null)
                        {
                            writer.Write("Cannot dump performance log because it was not enabled");
                        }
                        else
                        {
                            NfsServerLog.performanceLog.DumpLog(writer);
                        }
                    }
                    else if (line[0] == 'h' || line[0] == 'H')
                    {
                        writer.WriteLine("Commands: dump, help");
                    }
                    else if (line[0] == 'e' || line[0] == 'E')
                    {
                        socket.ShutdownSafe();
                        server.DisposeAndRemoveReceiveSocket(socket);
                    }
                    else
                    {
                        writer.WriteLine("UnknownCommand '{0}'", line);
                        writer.WriteLine("Commands: dump, help, exit");
                    }
                    SendPrompt();
                }
            }
        }

    }
}
