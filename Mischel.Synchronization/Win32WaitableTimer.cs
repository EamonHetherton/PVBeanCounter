using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Mischel.Synchronization
{
    [StructLayout(LayoutKind.Sequential)]
    public class SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    public class Win32WaitableTimer
    {
        // Access

        // Access rights
        // Standard access rights
        public const Int32 DELETE = 0x00010000;
        public const Int32 READ_CONTROL = 0x00020000;
        public const Int32 WRITE_DAC = 0x00040000;
        public const Int32 WRITE_OWNER = 0x00080000;
        public const Int32 SYNCHRONIZE = 0x00100000;

        // Specific to waitable timer
        public const Int32 TIMER_MODIFY_STATE = 0x0002;
        public const Int32 TIMER_QUERY_STATE = 0x0001;
        public const Int32 TIMER_ALL_ACCESS = 0x1F0003;

        // Creation flags
        public const Int32 CREATE_WAITABLE_TIMER_MANUAL_RESET = 0x0001;

        // Error codes
        public const int ERROR_FILE_NOT_FOUND = 2;
        public const int ERROR_INVALID_HANDLE = 6;
        public const int ERROR_INVALID_NAME = 123;

        public const int ERROR_NOT_SUPPORTED = 50;
        public const int ERROR_ALREADY_EXISTS = 183;

        public delegate void TimerAPCProc(
            IntPtr completionArg,
            UInt32 timerLowValue,
            UInt32 timerHighValue);

        [DllImport("kernel32", SetLastError = true)]
        public static extern SafeWaitHandle CreateWaitableTimer(
            SECURITY_ATTRIBUTES timerAttributes,
            bool manualReset,
            string timerName);

        [DllImport("kernel32", SetLastError = true)]
        public static extern SafeWaitHandle CreateWaitableTimerEx(
            SECURITY_ATTRIBUTES timerAttributes,
            string timerName,
            Int32 flags,
            UInt32 desiredAccess);

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool SetWaitableTimer(
            SafeHandle hTimer,
            ref long dueTime,
            int period,
            TimerAPCProc completionRoutine,
            IntPtr completionArg,
            bool fResume);

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool CancelWaitableTimer(SafeWaitHandle hTimer);

        [DllImport("kernel32", SetLastError = true)]
        public static extern SafeWaitHandle OpenWaitableTimer(
            UInt32 desiredAccess,
            bool inheritHandle,
            string timerName);

        public const int INFINITE = -1;

        [DllImport("kernel32", SetLastError = true)]
        public static extern UInt32 WaitForSingleObject(
            SafeWaitHandle handle,
            Int32 timeout);
    }
}
