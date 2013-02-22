using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;

using Microsoft.Win32.SafeHandles;

namespace Mischel.Synchronization
{
    public delegate void WaitableTimerCallback(object state, DateTime tickTime);

    public class WaitableTimer : WaitHandle
    {
        public const int ErrorNotSupported = Win32WaitableTimer.ERROR_NOT_SUPPORTED;

        public WaitableTimer(bool manualReset)
            : this(manualReset, null)
        {
        }

        public WaitableTimer(bool manualReset, string name)
        {
            bool createdNew;
            SafeWaitHandle = CreateTimerHandle(manualReset, name, out createdNew, null);
        }

        public WaitableTimer(bool manualReset, DateTime dueTime, int period)
            : this(manualReset, dueTime, period, null)
        {
        }

        public WaitableTimer(bool manualReset, TimeSpan dueTime, int period)
            : this(manualReset, dueTime, period, null)
        {
        }

        public WaitableTimer(bool manualReset, DateTime dueTime, int period, string name)
        {
            bool createdNew;
            SafeWaitHandle = CreateTimerHandle(manualReset, name, out createdNew, null);
            if (!createdNew)
            {
                Change(dueTime, period);
            }
        }

        public WaitableTimer(bool manualReset, TimeSpan dueTime, int period, string name)
        {
            bool createdNew;
            SafeWaitHandle = CreateTimerHandle(manualReset, name, out createdNew, null);
            if (!createdNew)
            {
                Change(dueTime, period);
            }
        }

        public WaitableTimer(
            bool manualReset,
            string name,
            out bool createdNew)
            : this(manualReset, name, out createdNew, null)
        {
        }

        public WaitableTimer(
            bool manualReset,
            string name,
            out bool createdNew,
            WaitableTimerSecurity timerSecurity)
        {
            SafeWaitHandle = CreateTimerHandle(manualReset, name, out createdNew, timerSecurity);
        }


        private static Exception GetWin32Exception()
        {
            return Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        private unsafe SafeWaitHandle CreateTimerHandle(bool manualReset,
            string name,
            out bool createdNew,
            WaitableTimerSecurity timerSecurity)
        {
            if (name != null && name.Length > 260)
            {
                throw new ArgumentException("name is too long.");
            }

            SECURITY_ATTRIBUTES secattr = null;
            if (timerSecurity != null)
            {
                // Create a SECURITY_ATTRIBUTES class and populate it
                // from the information stored in timerSecurity
                secattr = new SECURITY_ATTRIBUTES();
                secattr.nLength = Marshal.SizeOf(secattr);
                byte[] binaryForm = timerSecurity.GetSecurityDescriptorBinaryForm();
                byte* pbin = stackalloc byte[binaryForm.Length];
                fixed (byte* psrc = binaryForm)
                {
                    for (int i = 0; i < binaryForm.Length; ++i)
                    {
                        *(pbin + i) = *(psrc + i);
                    }
                }
                secattr.lpSecurityDescriptor = (IntPtr)pbin;
            }

            SafeWaitHandle handle = Win32WaitableTimer.CreateWaitableTimer(secattr, manualReset, name);
            int lastError = Marshal.GetLastWin32Error();
            if (handle.IsInvalid)
            {
                if (name == null)
                {
                    throw GetWin32Exception();
                }
                if (lastError == Win32WaitableTimer.ERROR_INVALID_HANDLE)
                {
                    throw new WaitHandleCannotBeOpenedException("Invalid handle");
                }
                throw new WaitHandleCannotBeOpenedException();
            }
            createdNew = (Marshal.GetLastWin32Error() == Win32WaitableTimer.ERROR_ALREADY_EXISTS);
            return handle;
        }

        private WaitableTimer(SafeWaitHandle handleValue)
        {
            SafeWaitHandle = handleValue;
        }

        public static WaitableTimer OpenExisting(string name)
        {
            return OpenExisting(name, WaitableTimerRights.Synchronize | WaitableTimerRights.Query);
        }

        public static WaitableTimer OpenExisting(string name, WaitableTimerRights rights)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }
            if (name.Length == 0)
            {
                throw new ArgumentException("name cannot be empty.");
            }
            if (name.Length > 260)
            {
                throw new ArgumentException("name is too long");
            }
            SafeWaitHandle handleValue = Win32WaitableTimer.OpenWaitableTimer((uint)rights, false, name);
            if (handleValue.IsInvalid)
            {
                int lastError = Marshal.GetLastWin32Error();
                if (lastError == Win32WaitableTimer.ERROR_FILE_NOT_FOUND ||
                    lastError == Win32WaitableTimer.ERROR_INVALID_NAME)
                {
                    throw new WaitHandleCannotBeOpenedException();
                }
                if (name == null || lastError != Win32WaitableTimer.ERROR_INVALID_HANDLE)
                {
                    throw GetWin32Exception();
                }
                throw new WaitHandleCannotBeOpenedException();
            }
            WaitableTimer timer = new WaitableTimer(handleValue);
            return timer;
        }

        class CallbackContext
        {
            private WaitableTimerCallback callback = null;
            private object callbackState = null;

            public CallbackContext(WaitableTimerCallback cb, object state)
            {
                callback = cb;
                callbackState = state;
            }

            public void TimerTick(IntPtr state, UInt32 timerLow, UInt32 timerHigh)
            {
                long timerValue = timerHigh;
                timerValue = (timerValue << 32) | timerLow;
                DateTime signalTime = new DateTime(timerValue).ToLocalTime();
                callback(callbackState, signalTime);
            }
        }

        public int Change(DateTime dueTime, int period)
        {
            return Change(dueTime, period, null, null, false);
        }

        public int Change(DateTime dueTime, int period, WaitableTimerCallback callback, object state, bool resume)
        {
            long dueTicks = dueTime.ToUniversalTime().Ticks;
            return ChangeInternal(dueTicks, period, callback, state, resume);
        }

        public int Change(TimeSpan dueTime, int period)
        {
            return Change(dueTime, period, null, null, false);
        }

        public int Change(TimeSpan dueTime, int period, WaitableTimerCallback callback, object state, bool resume)
        {
            long dueTicks = dueTime.Ticks;
            if (dueTicks < 0)
            {
                throw new ArgumentException("dueTime cannot be negative");
            }
            return ChangeInternal(-dueTicks, period, callback, state, resume);
        }

        private CallbackContext TimerCompletionCallback;

        private int ChangeInternal(long dueTime, int period, WaitableTimerCallback callback, object state, bool resume)
        {
            if (period < 0)
            {
                throw new ArgumentException("period cannot be negative.");
            }

            // CallbackContext is used to avoid a race condition.
            CallbackContext context = null;
            Win32WaitableTimer.TimerAPCProc completionCallback = null;
            if (callback != null)
            {
                context = new CallbackContext(callback, state);
                completionCallback = context.TimerTick;
            }

            // Have to do this because SetWaitableTimer needs a reference
            long due = dueTime;
            bool rslt = Win32WaitableTimer.SetWaitableTimer(
                SafeWaitHandle,
                ref due,
                period,
                completionCallback,
                IntPtr.Zero,
                resume);
            if (!rslt)
            {
                throw GetWin32Exception();
            }
            TimerCompletionCallback = context;
            return Marshal.GetLastWin32Error();
        }

        public void Cancel()
        {
            bool rslt = Win32WaitableTimer.CancelWaitableTimer(SafeWaitHandle);
            if (!rslt)
            {
                throw GetWin32Exception();
            }
        }

        public WaitableTimerSecurity GetAccessControl()
        {
            AccessControlSections sections =
                AccessControlSections.Access | 
                AccessControlSections.Group | 
                AccessControlSections.Owner;
            return new WaitableTimerSecurity(SafeWaitHandle, sections);
        }

        public void SetAccessControl(WaitableTimerSecurity timerSecurity)
        {
            if (timerSecurity == null)
            {
                throw new ArgumentNullException("timerSecurity");
            }
            timerSecurity.Persist(SafeWaitHandle);
        }
    }
}
