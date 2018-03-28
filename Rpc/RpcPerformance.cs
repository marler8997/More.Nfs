using System;
using System.IO;
using System.Diagnostics;

using More;

namespace More.Net.Rpc
{
    public static class RpcPerformanceLog
    {
        public static TextWriter rpcMessageSerializationLogger;

        private static UInt64 totalRpcCallMicroseconds = 0;
        private static UInt64 totalRpcCalls            = 0;

        private static UInt64 totalRpcSerializationMicroseconds = 0;
        private static UInt64 totalRpcSerializations            = 0;

        public static void PrintPerformance(UInt64 totalEllapsedMilliseconds)
        {
            UInt64 totalRpcCallMilliseconds = totalRpcCallMicroseconds / 1000;
            Console.WriteLine("[Performance] AvgRpcCallTime {0} microseconds CallTimePercentage {1:0.00}% TotalCallTime {2} milliseconds TotalTime {3} milliseconds",
                totalRpcCallMicroseconds / totalRpcCalls, 
            (Double)totalRpcCallMilliseconds / (Double)totalEllapsedMilliseconds, totalRpcCallMilliseconds, totalEllapsedMilliseconds);
        }

        private static Int64 serializeStartTime;
        public static void StartSerialize()
        {
            serializeStartTime = Stopwatch.GetTimestamp();
        }
        public static void StopSerializationAndLog(String serializationMethod)
        {
            Int64 serializeStopTime = Stopwatch.GetTimestamp();
            Int64 microseconds = (serializeStopTime - serializeStartTime).StopwatchTicksAsMicroseconds();
            totalRpcSerializationMicroseconds += (UInt64)microseconds;
            totalRpcSerializations++;
            rpcMessageSerializationLogger.WriteLine("[Performance] Rpc {0} took {1} microseconds", serializationMethod, microseconds);
        }
    }
}
