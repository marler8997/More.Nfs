using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

using More;
using More.Net.Rpc;
using More.Net.Nfs;

enum LogLevel
{
    None,
    Warning,
    Info,
    All,
}

class NfsServerProgramOptions : CommandLineParser
{
    public CommandLineArgument<IPAddress> listenIPAddress;

    public CommandLineArgument<UInt16> debugListenPort;

    public CommandLineArgument<UInt16> npcListenPort;

    public CommandLineArgumentString logFile;

    public CommandLineArgumentEnum<LogLevel> logLevel;
    public CommandLineArgumentString performanceLog;

#if WindowsCE
    public CommandLineSwitch jediTimer;
#endif

    public NfsServerProgramOptions()
    {
        listenIPAddress = new CommandLineArgument<IPAddress>(IPAddress.Parse, 'l', "Listen IP Address");
        listenIPAddress.Default = IPAddress.Any;
        Add(listenIPAddress);

        //
        // Debug Server
        //
        debugListenPort = new CommandLineArgument<UInt16>(UInt16.Parse, 'd', "DebugListenPort", "The TCP port that the debug server will be listening to (If no port is specified, the debug server will not be running)");
        Add(debugListenPort);

        //
        // Npc Server
        //
        npcListenPort = new CommandLineArgument<UInt16>(UInt16.Parse, 'n', "NpcListenPort", "The TCP port that the NPC server will be listening to (If no port is specified, the NPC server will not be running)");
        Add(npcListenPort);

        logFile = new CommandLineArgumentString('f', "LogFile", "Log file (logs to stdout if not specified)");
        Add(logFile);


        logLevel = new CommandLineArgumentEnum<LogLevel>('v', "LogLevel", "Level of statements to log");
        logLevel.SetDefault(LogLevel.None);
        Add(logLevel);

        performanceLog = new CommandLineArgumentString('p', "PerformanceLog", "Where to log performance ('internal',<filename>)");
        Add(performanceLog);
        
#if WindowsCE
        jediTimer = new CommandLineSwitch('j', "JediTimer", "Adds the jedi timer timestamp to printed commands");
        Add(jediTimer);
#endif
    }
    public override void PrintUsageHeader()
    {
        Console.WriteLine("Usage: NfsServer.exe [options] (<local-share-path1> <remote-share-name1>)...");
    }
}

class Program
{
    static void Main(String[] args)
    {
#if WindowsCE
        try
        {
#endif
        NfsServerLog.stopwatchTicksBase = Stopwatch.GetTimestamp();

        NfsServerProgramOptions options = new NfsServerProgramOptions();
        List<String> nonOptionArguments = options.Parse(args);

        if (nonOptionArguments.Count < 2)
        {
            options.ErrorAndUsage("Expected at least 2 non-option arguments but got '{0}'", nonOptionArguments.Count);
            return;
        }
        if (nonOptionArguments.Count % 2 == 1)
        {
            options.ErrorAndUsage("Expected an even number of non-option arguments but got {0}", nonOptionArguments.Count);
        }
        
        //
        //

        RootShareDirectory[] rootShareDirectories = new RootShareDirectory[nonOptionArguments.Count / 2];
        for (int i = 0; i < rootShareDirectories.Length; i++)
        {
            String localSharePath = nonOptionArguments[2 * i];
            String remoteShareName = nonOptionArguments[2 * i + 1];
            rootShareDirectories[i] = new RootShareDirectory(localSharePath, remoteShareName);
        }

        //
        // Options not exposed via command line yet
        //
        Int32 mountListenPort = Mount.DefaultPort;
        Int32 backlog = 4;

        UInt32 readSizeMax = 65536;
        UInt32 suggestedReadSizeMultiple = 4096;

        //
        // Listen IP Address
        //
        IPAddress listenIPAddress = options.listenIPAddress.ArgValue;

        //
        // Debug Server
        //
        IPEndPoint debugServerEndPoint = !options.debugListenPort.set ? null :
            new IPEndPoint(listenIPAddress, options.debugListenPort.ArgValue);

        //
        // Npc Server
        //
        IPEndPoint npcServerEndPoint = !options.npcListenPort.set ? null :
            new IPEndPoint(listenIPAddress, options.npcListenPort.ArgValue);
                
        //
        // Logging Options
        //                
#if WindowsCE
        JediTimer.printJediTimerPrefix = options.jediTimer.set;
#endif
        if(options.performanceLog.set)
        {
            if(options.performanceLog.ArgValue.Equals("internal", StringComparison.CurrentCultureIgnoreCase))
            {
                NfsServerLog.performanceLog = new InternalPerformanceLog();
                if (!options.debugListenPort.set)
                {
                    options.ErrorAndUsage("Invalid option combination: you cannot set '-i internal' unless you also set -d <port>");
                    return;
                }
            }
            else
            {
                try
                {
                    FileStream fileStream = new FileStream(options.performanceLog.ArgValue, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    NfsServerLog.performanceLog = new WriterPerformanceLog(fileStream);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Failed to create performance log file '{0}'", options.performanceLog.ArgValue);
                    throw e;
                }
            }
        }

        TextWriter selectServerEventsLog = null;
        if (options.logLevel.ArgValue != LogLevel.None)
        {
            TextWriter logWriter;
            if (options.logFile.set)
            {
                logWriter = new StreamWriter(new FileStream(options.logFile.ArgValue, FileMode.Create, FileAccess.Write, FileShare.Read));
            }
            else
            {
                logWriter = Console.Out;
            }

            NfsServerLog.sharedFileSystemLogger             = (options.logLevel.ArgValue >= LogLevel.Info   ) ? logWriter : null;
            NfsServerLog.rpcCallLogger                      = (options.logLevel.ArgValue >= LogLevel.Info   ) ? logWriter : null;
            NfsServerLog.warningLogger                      = (options.logLevel.ArgValue >= LogLevel.Warning) ? logWriter : null;
            NfsServerLog.npcEventsLogger                    = (options.logLevel.ArgValue >= LogLevel.Info   ) ? logWriter : null;

            RpcPerformanceLog.rpcMessageSerializationLogger = (options.logLevel.ArgValue >= LogLevel.Info   ) ? logWriter : null;

            selectServerEventsLog = (options.logLevel.ArgValue >= LogLevel.All) ? logWriter : null;
        }

        //
        // Permissions
        //
        ModeFlags defaultDirectoryPermissions =
            ModeFlags.OtherExecute | ModeFlags.OtherWrite | ModeFlags.OtherRead |
            ModeFlags.GroupExecute | ModeFlags.GroupWrite | ModeFlags.GroupRead |
            ModeFlags.OwnerExecute | ModeFlags.OwnerWrite | ModeFlags.OwnerRead;
        /*ModeFlags.SaveSwappedText | ModeFlags.SetUidOnExec | ModeFlags.SetGidOnExec;*/
        ModeFlags defaultFilePermissions =
            ModeFlags.OtherExecute | ModeFlags.OtherWrite | ModeFlags.OtherRead |
            ModeFlags.GroupExecute | ModeFlags.GroupWrite | ModeFlags.GroupRead |
            ModeFlags.OwnerExecute | ModeFlags.OwnerWrite | ModeFlags.OwnerRead;
        /*ModeFlags.SaveSwappedText | ModeFlags.SetUidOnExec | ModeFlags.SetGidOnExec;*/
        IPermissions permissions = new ConstantPermissions(defaultDirectoryPermissions, defaultFilePermissions);


        IFileIDsAndHandlesDictionary fileIDDictionary = new FreeStackFileIDDictionary(512, 512, 4096, 1024);

        SharedFileSystem sharedFileSystem = new SharedFileSystem(fileIDDictionary, permissions, rootShareDirectories);

        new RpcServicesManager().Run(
            selectServerEventsLog,
            debugServerEndPoint,
            npcServerEndPoint,
            listenIPAddress,
            backlog, sharedFileSystem,
            PortMap.DefaultPort, mountListenPort, Nfs.DefaultPort,
            readSizeMax, suggestedReadSizeMultiple);

#if WindowsCE
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
#endif
    }
}


