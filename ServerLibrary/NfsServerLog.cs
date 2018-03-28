using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;

using More;

using FileID = System.UInt64;

namespace More.Net.Nfs
{
    public interface IPerformanceLogger
    {
        void Log(Nfs3Command command, UInt32 callTimeMicroseconds, Int32 extraData);
        void DumpLog(TextWriter writer);
    }
    public class WriterPerformanceLog : IPerformanceLogger
    {
        readonly FileStream fileStream;
        readonly TextWriter writer;

        readonly Byte[] buffer;
        readonly LineParser lineParser;

        public WriterPerformanceLog(FileStream fileStream)
        {
            this.fileStream = fileStream;
            this.writer = new StreamWriter(fileStream);

            buffer = new Byte[512];
            lineParser = new LineParser(writer.Encoding, 512);
        }
        public void Log(Nfs3Command command, UInt32 callTimeMicroseconds, Int32 extraData)
        {
            Double timestamp = (Stopwatch.GetTimestamp() - NfsServerLog.stopwatchTicksBase).StopwatchTicksAsDoubleMilliseconds();
            writer.WriteLine(NfsServerLog.PerformanceLogString(timestamp, command, callTimeMicroseconds, extraData));
            writer.Flush();
        }
        public void DumpLog(TextWriter writer)
        {
            if (fileStream == null)
                writer.WriteLine("Cannot dump performance log because it is not an internal performance or a file log");

            fileStream.Position = 0;

            Byte[] buffer = new Byte[512];
            while (true)
            {
                Int32 bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                if(bytesRead <= 0) break;

                lineParser.Add(buffer, 0, (UInt32)bytesRead);
                while(true)
                {
                    String line = lineParser.GetLine();
                    if (line == null) break;
                    writer.WriteLine(line);
                }
            }
        }
    }
    public class InternalPerformanceLog : IPerformanceLogger
    {
        public struct PerformanceLogEntry
        {
            public readonly Double timestamp;
            public readonly Nfs3Command command;
            public readonly UInt32 callTimeMicroseconds;
            public readonly Int32 extraData;
            public PerformanceLogEntry(Nfs3Command command, UInt32 callTimeMicroseconds, Int32 extraData)
            {
                this.timestamp = (Stopwatch.GetTimestamp() - NfsServerLog.stopwatchTicksBase).StopwatchTicksAsDoubleMilliseconds();
                this.command = command;
                this.callTimeMicroseconds = callTimeMicroseconds;
                this.extraData = extraData;
            }
            public String LogString()
            {
                return NfsServerLog.PerformanceLogString(timestamp, command, callTimeMicroseconds, extraData);
            }
        }

        readonly List<PerformanceLogEntry> storedCommands;
        public InternalPerformanceLog()
        {
            storedCommands = new List<PerformanceLogEntry>();
        }
        public void Log(Nfs3Command command, UInt32 callTimeMicroseconds, Int32 extraData)
        {
            storedCommands.Add(new PerformanceLogEntry(command, callTimeMicroseconds, extraData));
        }
        public void DumpLog(TextWriter writer)
        {
            for (int i = 0; i < storedCommands.Count; i++)
            {
                writer.WriteLine(storedCommands[i].LogString());
            }
        }
    }
    public static class NfsServerLog
    {
        public static readonly StringBuilder sharedDataStringBuilder = new StringBuilder();

        public static Int64 stopwatchTicksBase;

        public static TextWriter rpcCallLogger;
        public static TextWriter warningLogger;
        public static TextWriter sharedFileSystemLogger;
        public static TextWriter npcEventsLogger;

        public static IPerformanceLogger performanceLog;
        public static String PerformanceLogString(Double timestamp, Nfs3Command command, UInt32 callTimeMicroseconds, Int32 extraData)
        {
            String extraDataString = "";
            if (command == Nfs3Command.READ || command == Nfs3Command.WRITE)
            {
                extraDataString = String.Format(" length was {0}", extraData);
            }
            return String.Format("At {0,8:0.00} milliseconds Call '{1,12}' Took {2,8:0.00} milliseconds{3}",
                 timestamp, command, (Double)callTimeMicroseconds / 1000, extraDataString);
        }
    }
}