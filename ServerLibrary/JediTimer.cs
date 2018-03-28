using System;
using System.Runtime.InteropServices;

namespace More.Net.Nfs
{
#if WindowsCE
    public class JediTimer
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("coredll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean DeviceIoControl(
            [In]  IntPtr handle,
            [In]  Int32 dwIoControlCode,
            ref HPTimerHWtruct timer,// [In]  Byte[] lpInBuffer, //[In] IntPtr lpInBuffer,
            [In]  Int32 nInBufferSize,
            [Out] Byte[] lpOutBuffer, //[Out] IntPtr lpOutBuffer,
            [In]  Int32 nOutBufferSize,
            out   Int32 lpBytesReturned,
            [In]  IntPtr lpOverlapped);

        const UInt32 GenericRead  = 0x80000000;
        const UInt32 GenericWrite = 0x40000000;
        const UInt32 OpenExisting = 3;
        
        const Int32 FileDeviceSerialPort = 0x0000001B;
        const Int32 MethodBuffered = 0;
        const Int32 FileAnyAccess = 0;

        static Int32 ControlCode(Int32 t, Int32 f, Int32 m, Int32 a)
        {
            return (((t)<<16)|((a)<<14)|((f)<<2)|(m));
        }
        static readonly Int32 MapTimerHw = ControlCode(FileDeviceSerialPort, 2049, MethodBuffered, FileAnyAccess);

        [StructLayout(LayoutKind.Sequential)]
        public struct HPTimerHWtruct
        {
            public UInt32 Revision;
            public UInt32 HeaderSize;
            public IntPtr TimerRegPtr;
            public UInt32 TimerEndCount;
            public UInt32 TimerFrequencyHz;      
        }

        static unsafe UInt32* timerAddress = null;

        static UInt32 GetTime()
        {
            UInt32 time;

            unsafe
            {
                if (timerAddress == null)
                {
                    IntPtr handle = IntPtr.Zero;
                    HPTimerHWtruct timerStruct;
                    try
                    {
                        handle = WindowsCESafeNativeMethods.CreateFile("TRC1:", GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                            throw new InvalidOperationException("CreateFile returned invalid handle");

                        timerStruct = new HPTimerHWtruct();
                        timerStruct.Revision = 1;
                        Int32 bytesReturned;
                        if (!DeviceIoControl(handle, MapTimerHw, ref timerStruct, 20, null, 0, out bytesReturned, IntPtr.Zero))
                            throw new InvalidOperationException("DeviceIoControl returned 0");
                    }
                    finally
                    {
                        if (handle != IntPtr.Zero && handle != new IntPtr(-1)) WindowsCESafeNativeMethods.CloseHandle(handle);
                    }

                    if (timerStruct.TimerRegPtr == IntPtr.Zero)
                        throw new InvalidOperationException("HPTimerHWStruct.TimerRegPtr is 0");

                    timerAddress = (UInt32*)(timerStruct.TimerRegPtr);
                }
                time = *timerAddress;
            }

            return time;
        }

        public static Boolean printJediTimerPrefix;
        public static String JediTimerPrefix()
        {
            return GetTime() + " ";
        }
    }
#endif
}
