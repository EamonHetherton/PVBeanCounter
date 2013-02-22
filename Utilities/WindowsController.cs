/*
    WindowsController class for C#
		Version: 1.1

    Copyright © 2002-2003, The KPD-Team
    All rights reserved.
    http://www.mentalis.org/

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    - Redistributions of source code must retain the above copyright
       notice, this list of conditions and the following disclaimer. 

    - Neither the name of the KPD-Team, nor the names of its contributors
       may be used to endorse or promote products derived from this
       software without specific prior written permission. 

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL
  THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
  SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
  HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
  STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
  OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetworkLib {
    /// <summary>
    /// Implements methods to exit Windows.
    /// http://www.codeproject.com/csharp/timercomputershutdown.asp
    /// </summary>
    public class WindowsController {
        /// <summary>Required to enable or disable the privileges in an access token.</summary>
        private const int TOKEN_ADJUST_PRIVILEGES = 0x20;
        /// <summary>Required to query an access token.</summary>
        private const int TOKEN_QUERY = 0x8;
        /// <summary>The privilege is enabled.</summary>
        private const int SE_PRIVILEGE_ENABLED = 0x2;
        /// <summary>Specifies that the function should search the system message-table resource(s) for the requested message.</summary>
        private const int FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;
        /// <summary>Forces processes to terminate. When this flag is set, the system does not send the WM_QUERYENDSESSION and WM_ENDSESSION messages. This can cause the applications to lose data. Therefore, you should only use this flag in an emergency.</summary>
        private const int EWX_FORCE = 4;

        /// <summary>
        /// Exits windows (and tries to enable any required access rights, if necesarry).
        /// </summary>
        /// <param name="how">One of the RestartOptions values that specifies how to exit windows.</param>
        /// <param name="force">True if the exit has to be forced, false otherwise.</param>
        /// <exception cref="PrivilegeException">There was an error while requesting a required privilege.</exception>
        /// <exception cref="PlatformNotSupportedException">The requested exit method is not supported on this platform.</exception>
        public static void ExitWindows(RestartOptions how, bool force) {
            switch(how) {
                case RestartOptions.Suspend: SuspendSystem(false, force); break;
                case RestartOptions.Hibernate: SuspendSystem(true, force); break;
                default: ExitWindows((int)how, force); break;
            }
        }
        /// <summary>
        /// Exits windows (and tries to enable any required access rights, if necesarry).
        /// </summary>
        /// <param name="how">One of the RestartOptions values that specifies how to exit windows.</param>
        /// <param name="force">True if the exit has to be forced, false otherwise.</param>
        /// <remarks>This method cannot hibernate or suspend the system.</remarks>
        /// <exception cref="PrivilegeException">There was an error while requesting a required privilege.</exception>
        protected static void ExitWindows(int how, bool force) {
            EnableToken("SeShutdownPrivilege");
            if(force) how = how | EWX_FORCE;
            if(Win32.ExitWindowsEx(how, 0) == 0) throw new PrivilegeException(FormatError(Marshal.GetLastWin32Error()));
        }
        /// <summary>
        /// Tries to enable the specified privilege.
        /// </summary>
        /// <param name="privilege">The privilege to enable.</param>
        /// <exception cref="PrivilegeException">There was an error while requesting a required privilege.</exception>
        /// <remarks>Thanks to Michael S. Muegel for notifying us about a bug in this code.</remarks>
        protected static void EnableToken(string privilege) {
            if(Environment.OSVersion.Platform != PlatformID.Win32NT || !CheckEntryPoint("advapi32.dll", "AdjustTokenPrivileges")) return;
            IntPtr tokenHandle = IntPtr.Zero;
            LUID privilegeLUID = new LUID();
            TOKEN_PRIVILEGES newPrivileges = new TOKEN_PRIVILEGES();
            TOKEN_PRIVILEGES tokenPrivileges;
            if(Win32.OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref tokenHandle) == 0)
                throw new PrivilegeException(FormatError(Marshal.GetLastWin32Error()));
            if(Win32.LookupPrivilegeValue(string.Empty, privilege, ref privilegeLUID) == 0)
                throw new PrivilegeException(FormatError(Marshal.GetLastWin32Error()));
            tokenPrivileges.PrivilegeCount = 1;
            tokenPrivileges.Privileges.Attributes = SE_PRIVILEGE_ENABLED;
            tokenPrivileges.Privileges.pLuid = privilegeLUID;
            int size = 4;
            if(Win32.AdjustTokenPrivileges(tokenHandle, 0, ref tokenPrivileges, 4 + (12 * tokenPrivileges.PrivilegeCount), ref newPrivileges, ref size) == 0)
                throw new PrivilegeException(FormatError(Marshal.GetLastWin32Error()));
        }
        /// <summary>
        /// Suspends or hibernates the system.
        /// </summary>
        /// <param name="hibernate">True if the system has to hibernate, false if the system has to be suspended.</param>
        /// <param name="force">True if the exit has to be forced, false otherwise.</param>
        /// <exception cref="PlatformNotSupportedException">The requested exit method is not supported on this platform.</exception>
        protected static void SuspendSystem(bool hibernate, bool force) {
            if(!CheckEntryPoint("powrprof.dll", "SetSuspendState"))
                throw new PlatformNotSupportedException("The SetSuspendState method is not supported on this system!");
            Win32.SetSuspendState((int)(hibernate ? 1 : 0), (int)(force ? 1 : 0), 0);
        }
        /// <summary>
        /// Checks whether a specified method exists on the local computer.
        /// </summary>
        /// <param name="library">The library that holds the method.</param>
        /// <param name="method">The entry point of the requested method.</param>
        /// <returns>True if the specified method is present, false otherwise.</returns>
        protected static bool CheckEntryPoint(string library, string method) {
            IntPtr libPtr = Win32.LoadLibrary(library);
            if(!libPtr.Equals(IntPtr.Zero)) {
                if(!Win32.GetProcAddress(libPtr, method).Equals(IntPtr.Zero)) {
                    Win32.FreeLibrary(libPtr);
                    return true;
                }
                Win32.FreeLibrary(libPtr);
            }
            return false;
        }
        /// <summary>
        /// Formats an error number into an error message.
        /// </summary>
        /// <param name="number">The error number to convert.</param>
        /// <returns>A string representation of the specified error number.</returns>
        protected static string FormatError(int number) {
            try {
                StringBuilder buffer = new StringBuilder(255);
                Win32.FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, number, 0, buffer, buffer.Capacity, 0);
                return buffer.ToString();
            } catch(Exception) {
                return "Unspecified error [" + number.ToString() + "]";
            }
        }
    }
    /// <summary>
    /// The exception that is thrown when an error occures when requesting a specific privilege.
    /// </summary>
    public class PrivilegeException: Exception {
        /// <summary>
        /// Initializes a new instance of the PrivilegeException class.
        /// </summary>
        public PrivilegeException() : base() { }
        /// <summary>
        /// Initializes a new instance of the PrivilegeException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public PrivilegeException(string message) : base(message) { }
    }
}
