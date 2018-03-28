using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Diagnostics;

using More;
using More.Net;
using More.Net.Rpc;

namespace More.Net.Nfs
{
    public class RpcServicesManager
    {
        Int64 serverStartTimeStopwatchTicks;
        SelectServerSharedBuffer selectServer;

        public RpcServicesManager()
        {
        }
        static void AddRpcServer(SelectServerSharedBuffer selectServer, EndPoint endPoint, RpcServerHandler handler, int backlog)
        {
            Socket tcpAcceptSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            tcpAcceptSocket.Bind(endPoint);
            tcpAcceptSocket.Listen(backlog);
            selectServer.AddListenSocket(tcpAcceptSocket, handler.AcceptCallback);

            Socket udpSocket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            udpSocket.Bind(endPoint);
            selectServer.AddReceiveSocket(udpSocket, handler.DatagramRecvHandler);
        }
        public void Run(TextWriter selectServerEventLog, IPEndPoint debugServerEndPoint, IPEndPoint npcServerEndPoint,
            IPAddress listenIPAddress, Int32 backlog, SharedFileSystem sharedFileSystem,
            Int32 portmapPort, Int32 mountPort, Int32 nfsPort, UInt32 readSizeMax, UInt32 suggestedReadSizeMultiple)
        {
            ByteArrayReference sendBuffer = new ByteArrayReference(4096);

            //
            // Create Mappings List
            //
            NamedMapping[] namedMappings = new NamedMapping[] {
                new NamedMapping(PortMap.Name, new Mapping(PortMap.ProgramNumber, PortMap2.ProgramVersion, PortMap.IPProtocolTcp, (UInt32)portmapPort)),
                new NamedMapping(PortMap.Name, new Mapping(PortMap.ProgramNumber, PortMap2.ProgramVersion, PortMap.IPProtocolUdp, (UInt32)portmapPort)),

                new NamedMapping(Mount.Name  , new Mapping(Mount.ProgramNumber  , Mount1.ProgramVersion  , PortMap.IPProtocolTcp, (UInt32)mountPort)),
                new NamedMapping(Mount.Name  , new Mapping(Mount.ProgramNumber  , Mount1.ProgramVersion  , PortMap.IPProtocolUdp, (UInt32)mountPort)),

                new NamedMapping(Mount.Name  , new Mapping(Mount.ProgramNumber  , Mount3.ProgramVersion  , PortMap.IPProtocolTcp, (UInt32)mountPort)),
                new NamedMapping(Mount.Name  , new Mapping(Mount.ProgramNumber  , Mount3.ProgramVersion  , PortMap.IPProtocolUdp, (UInt32)mountPort)),

                new NamedMapping(Nfs.Name    , new Mapping(Nfs.ProgramNumber    , Nfs3.ProgramVersion    , PortMap.IPProtocolTcp, (UInt32)nfsPort)),
                new NamedMapping(Nfs.Name    , new Mapping(Nfs.ProgramNumber    , Nfs3.ProgramVersion    , PortMap.IPProtocolUdp, (UInt32)nfsPort)),
            };

            PortMap2Server portMapServer = new PortMap2Server(this, namedMappings, sendBuffer);
            Mount1And3Server mountServer = new Mount1And3Server(this, sharedFileSystem, sendBuffer);
            Nfs3Server nfsServer = new Nfs3Server(this, sharedFileSystem, sendBuffer, readSizeMax, suggestedReadSizeMultiple);

            //
            // Create Endpoints
            //
            if (listenIPAddress == null)
            {
                listenIPAddress = IPAddress.Any;
            }
            IPEndPoint portMapEndPoint = new IPEndPoint(listenIPAddress, portmapPort);
            IPEndPoint mountEndPoint = new IPEndPoint(listenIPAddress, mountPort);
            IPEndPoint nfsEndPoint = new IPEndPoint(listenIPAddress, nfsPort);

            selectServer = new SelectServerSharedBuffer(false, new byte[4096]);

            AddRpcServer(selectServer, portMapEndPoint, portMapServer, backlog);
            AddRpcServer(selectServer, mountEndPoint, mountServer, backlog);
            AddRpcServer(selectServer, nfsEndPoint, nfsServer, backlog);
 
            if (debugServerEndPoint != null)
            {
                Socket tcpAcceptSocket = new Socket(debugServerEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                tcpAcceptSocket.Bind(debugServerEndPoint);
                tcpAcceptSocket.Listen(4);
                selectServer.AddListenSocket(tcpAcceptSocket, new ControlServer().AcceptCallback);
            }

            if (npcServerEndPoint != null)
            {
#if !WindowsCE
                /*
                NpcSelectServerHandler npcServerHandler;
                {
                    Nfs3Server.NfsServerManager nfsServerManager = new Nfs3Server.NfsServerManager(nfsServer);
                    NpcReflector reflector = new NpcReflector(
                        new NpcExecutionObject(nfsServerManager, "Nfs3ServerManager", null, null),
                        new NpcExecutionObject(nfsServer, "Nfs3Server", null, null),
                        new NpcExecutionObject(portMapServer, "Portmap2Server", null, null),
                        new NpcExecutionObject(mountServer, "Mount1And3Server", null, null)
                        );
                    npcServerHandler = new NpcSelectServerHandler(NpcCallback.Instance, reflector, new DefaultNpcHtmlGenerator("NfsServer", reflector));
                }

                Socket tcpAcceptSocket = new Socket(npcServerEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                tcpAcceptSocket.Bind(npcServerEndPoint);
                tcpAcceptSocket.Listen(4);

                selectServer.control.AddListenSocket(tcpAcceptSocket, npcServerHandler.AcceptCallback);
                 * */
#endif
            }

            this.serverStartTimeStopwatchTicks = Stopwatch.GetTimestamp();
            /*
            selectServer.Run(selectServerEventLog, new byte[1024], tcpListeners.ToArray(),
                new UdpSelectListener[]{
                    new UdpSelectListener(portMapEndPoint, portMapServer),
                    new UdpSelectListener(mountEndPoint  , mountServer),
                    new UdpSelectListener(nfsEndPoint    , nfsServer),
                }
            );
            */
            selectServer.Run();
        }

        /*
        public void PrintPerformance()
        {
            Int64 totalStopwatchTicks = Stopwatch.GetTimestamp() - serverStartTimeStopwatchTicks;

            if (RpcPerformanceLog.rpcCallTimeLogger != null)
            {
                RpcPerformanceLog.PrintPerformance(totalTimeMilliseconds);
            }
            if (selectServer.totalSelectBlockTimeMicroseconds > 0)
            {
                UInt64 totalSelectBlockMilliseconds = selectServer.totalSelectBlockTimeMicroseconds / 1000;
                Console.WriteLine("[Performance] SelectBlockPercentage {0:0.00}% TotalSelectBlockTime {1} milliseconds TotalTime {2} milliseconds",
                (Double)totalSelectBlockMilliseconds / (Double)totalTimeMilliseconds, totalSelectBlockMilliseconds, totalTimeMilliseconds);
                
            }
        }
        */
    }
#if comment
    class NpcCallback : INpcServerCallback
    {
        private static NpcCallback instance = null;
        public static NpcCallback Instance
        {
            get
            {
                if (instance == null) instance = new NpcCallback();
                return instance;
            }
        }
        private NpcCallback() { }

        public void ServerListening(Socket listenSocket)
        {
            if (NfsServerLog.npcEventsLogger != null)
            {
                NfsServerLog.npcEventsLogger.WriteLine("[Npc] Server is listening");
            }
        }
        public void FunctionCall(string clientString, string methodName)
        {
            if (NfsServerLog.npcEventsLogger != null)
            {
                NfsServerLog.npcEventsLogger.WriteLine("[Npc] Client '{0}': Function Call '{1}'",
                    clientString, methodName);
            }
        }
        public void FunctionCallThrewException(string clientString, string methodName, Exception e)
        {
            if (NfsServerLog.npcEventsLogger != null)
            {
                NfsServerLog.npcEventsLogger.WriteLine("[Npc] Client '{0}': Function Call '{1}' threw exception: {2}",
                    clientString, methodName, e);
            }
        }
        public void GotInvalidData(string clientString, string message)
        {
            if (NfsServerLog.npcEventsLogger != null)
            {
                NfsServerLog.npcEventsLogger.WriteLine("[Npc] Client '{0}': Got invalid data: {1}", clientString, message);
            }
        }
        public void ExceptionDuringExecution(string clientString, string methodName, Exception e)
        {
            if (NfsServerLog.npcEventsLogger != null)
            {
                NfsServerLog.npcEventsLogger.WriteLine("[Npc] Client '{0}': Exception: {1}", clientString, e);
            }
        }
        public void ExceptionWhileGeneratingHtml(string clientString, Exception e)
        {
            if (NfsServerLog.npcEventsLogger != null)
            {
                NfsServerLog.npcEventsLogger.WriteLine("[Npc] Client '{0}': Exception: {1}", clientString, e);
            }
        }
        public void UnhandledException(string clientString, Exception e)
        {
            if (NfsServerLog.npcEventsLogger != null)
            {
                NfsServerLog.npcEventsLogger.WriteLine("[Npc] Client '{0}': Exception: {1}", clientString, e);
            }
        }
    }
#endif
}