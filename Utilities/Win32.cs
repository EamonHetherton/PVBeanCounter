
using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace NetworkLib {
    internal class Win32 {
        /// <summary>
        /// Netapi32.dll : The NetServerEnum function lists all servers
        /// of the specified type that are visible in a domain. For example, an 
        /// application can call NetServerEnum to list all domain controllers only
        /// or all SQL servers only.
        ///	You can combine bit masks to list several types. For example, a value 
        /// of 0x00000003  combines the bit masks for SV_TYPE_WORKSTATION 
        /// (0x00000001) and SV_TYPE_SERVER (0x00000002)
        /// </summary>
        [DllImport("Netapi32", CharSet = CharSet.Auto, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern int NetServerEnum(string ServerNane, int dwLevel, ref IntPtr pBuf, int dwPrefMaxLen, out int dwEntriesRead,
            out int dwTotalEntries, int dwServerType, string domain, out int dwResumeHandle);

        /// <summary>
        /// Netapi32.dll : The NetApiBufferFree function frees 
        /// the memory that the NetApiBufferAllocate function allocates. 
        /// Call NetApiBufferFree to free the memory that other network 
        /// management functions return.
        /// </summary>
        [DllImport("Netapi32", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern int NetApiBufferFree(IntPtr pBuf);

        /// <summary>
        /// The LoadLibrary function maps the specified executable module into the address space of the calling process.
        /// </summary>
        /// <param name="lpLibFileName">Pointer to a null-terminated string that names the executable module (either a .dll or .exe file). The name specified is the file name of the module and is not related to the name stored in the library module itself, as specified by the LIBRARY keyword in the module-definition (.def) file.</param>
        /// <returns>If the function succeeds, the return value is a handle to the module.<br></br><br>If the function fails, the return value is NULL. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryA", CharSet = CharSet.Ansi)]
        internal static extern IntPtr LoadLibrary(string lpLibFileName);
        /// <summary>
        /// The FreeLibrary function decrements the reference count of the loaded dynamic-link library (DLL). When the reference count reaches zero, the module is unmapped from the address space of the calling process and the handle is no longer valid.
        /// </summary>
        /// <param name="hLibModule">Handle to the loaded DLL module. The LoadLibrary or GetModuleHandle function returns this handle.</param>
        /// <returns>If the function succeeds, the return value is nonzero.<br></br><br>If the function fails, the return value is zero. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
        [DllImport("kernel32.dll", EntryPoint = "FreeLibrary", CharSet = CharSet.Ansi)]
        internal static extern int FreeLibrary(IntPtr hLibModule);
        /// <summary>
        /// The GetProcAddress function retrieves the address of an exported function or variable from the specified dynamic-link library (DLL).
        /// </summary>
        /// <param name="hModule">Handle to the DLL module that contains the function or variable. The LoadLibrary or GetModuleHandle function returns this handle.</param>
        /// <param name="lpProcName">Pointer to a null-terminated string containing the function or variable name, or the function's ordinal value. If this parameter is an ordinal value, it must be in the low-order word; the high-order word must be zero.</param>
        /// <returns>If the function succeeds, the return value is the address of the exported function or variable.<br></br><br>If the function fails, the return value is NULL. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        /// <summary>
        /// The SetSuspendState function suspends the system by shutting power down. Depending on the Hibernate parameter, the system either enters a suspend (sleep) state or hibernation (S4). If the ForceFlag parameter is TRUE, the system suspends operation immediately; if it is FALSE, the system requests permission from all applications and device drivers before doing so.
        /// </summary>
        /// <param name="Hibernate">Specifies the state of the system. If TRUE, the system hibernates. If FALSE, the system is suspended.</param>
        /// <param name="ForceCritical">Forced suspension. If TRUE, the function broadcasts a PBT_APMSUSPEND event to each application and driver, then immediately suspends operation. If FALSE, the function broadcasts a PBT_APMQUERYSUSPEND event to each application to request permission to suspend operation.</param>
        /// <param name="DisableWakeEvent">If TRUE, the system disables all wake events. If FALSE, any system wake events remain enabled.</param>
        /// <returns>If the function succeeds, the return value is nonzero.<br></br><br>If the function fails, the return value is zero. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
        [DllImport("powrprof.dll", EntryPoint = "SetSuspendState", CharSet = CharSet.Ansi)]
        internal static extern int SetSuspendState(int Hibernate, int ForceCritical, int DisableWakeEvent);
        /// <summary>
        /// The OpenProcessToken function opens the access token associated with a process.
        /// </summary>
        /// <param name="ProcessHandle">Handle to the process whose access token is opened.</param>
        /// <param name="DesiredAccess">Specifies an access mask that specifies the requested types of access to the access token. These requested access types are compared with the token's discretionary access-control list (DACL) to determine which accesses are granted or denied.</param>
        /// <param name="TokenHandle">Pointer to a handle identifying the newly-opened access token when the function returns.</param>
        /// <returns>If the function succeeds, the return value is nonzero.<br></br><br>If the function fails, the return value is zero. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
        [DllImport("advapi32.dll", EntryPoint = "OpenProcessToken", CharSet = CharSet.Ansi)]
        internal static extern int OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, ref IntPtr TokenHandle);
        /// <summary>
        /// The LookupPrivilegeValue function retrieves the locally unique identifier (LUID) used on a specified system to locally represent the specified privilege name.
        /// </summary>
        /// <param name="lpSystemName">Pointer to a null-terminated string specifying the name of the system on which the privilege name is looked up. If a null string is specified, the function attempts to find the privilege name on the local system.</param>
        /// <param name="lpName">Pointer to a null-terminated string that specifies the name of the privilege, as defined in the Winnt.h header file. For example, this parameter could specify the constant SE_SECURITY_NAME, or its corresponding string, "SeSecurityPrivilege".</param>
        /// <param name="lpLuid">Pointer to a variable that receives the locally unique identifier by which the privilege is known on the system, specified by the lpSystemName parameter.</param>
        /// <returns>If the function succeeds, the return value is nonzero.<br></br><br>If the function fails, the return value is zero. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
        [DllImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueA", CharSet = CharSet.Ansi)]
        internal static extern int LookupPrivilegeValue(string lpSystemName, string lpName, ref LUID lpLuid);
        /// <summary>
        /// The AdjustTokenPrivileges function enables or disables privileges in the specified access token. Enabling or disabling privileges in an access token requires TOKEN_ADJUST_PRIVILEGES access.
        /// </summary>
        /// <param name="TokenHandle">Handle to the access token that contains the privileges to be modified. The handle must have TOKEN_ADJUST_PRIVILEGES access to the token. If the PreviousState parameter is not NULL, the handle must also have TOKEN_QUERY access.</param>
        /// <param name="DisableAllPrivileges">Specifies whether the function disables all of the token's privileges. If this value is TRUE, the function disables all privileges and ignores the NewState parameter. If it is FALSE, the function modifies privileges based on the information pointed to by the NewState parameter.</param>
        /// <param name="NewState">Pointer to a TOKEN_PRIVILEGES structure that specifies an array of privileges and their attributes. If the DisableAllPrivileges parameter is FALSE, AdjustTokenPrivileges enables or disables these privileges for the token. If you set the SE_PRIVILEGE_ENABLED attribute for a privilege, the function enables that privilege; otherwise, it disables the privilege. If DisableAllPrivileges is TRUE, the function ignores this parameter.</param>
        /// <param name="BufferLength">Specifies the size, in bytes, of the buffer pointed to by the PreviousState parameter. This parameter can be zero if the PreviousState parameter is NULL.</param>
        /// <param name="PreviousState">Pointer to a buffer that the function fills with a TOKEN_PRIVILEGES structure that contains the previous state of any privileges that the function modifies. This parameter can be NULL.</param>
        /// <param name="ReturnLength">Pointer to a variable that receives the required size, in bytes, of the buffer pointed to by the PreviousState parameter. This parameter can be NULL if PreviousState is NULL.</param>
        /// <returns>If the function succeeds, the return value is nonzero. To determine whether the function adjusted all of the specified privileges, call Marshal.GetLastWin32Error.</returns>
        [DllImport("advapi32.dll", EntryPoint = "AdjustTokenPrivileges", CharSet = CharSet.Ansi)]
        internal static extern int AdjustTokenPrivileges(IntPtr TokenHandle, int DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, ref TOKEN_PRIVILEGES PreviousState, ref int ReturnLength);
        /// <summary>
        /// The ExitWindowsEx function either logs off the current user, shuts down the system, or shuts down and restarts the system. It sends the WM_QUERYENDSESSION message to all applications to determine if they can be terminated.
        /// </summary>
        /// <param name="uFlags">Specifies the type of shutdown.</param>
        /// <param name="dwReserved">This parameter is ignored.</param>
        /// <returns>If the function succeeds, the return value is nonzero.<br></br><br>If the function fails, the return value is zero. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
        [DllImport("user32.dll", EntryPoint = "ExitWindowsEx", CharSet = CharSet.Ansi)]
        internal static extern int ExitWindowsEx(int uFlags, int dwReserved);
        /// <summary>
        /// The FormatMessage function formats a message string. The function requires a message definition as input. The message definition can come from a buffer passed into the function. It can come from a message table resource in an already-loaded module. Or the caller can ask the function to search the system's message table resource(s) for the message definition. The function finds the message definition in a message table resource based on a message identifier and a language identifier. The function copies the formatted message text to an output buffer, processing any embedded insert sequences if requested.
        /// </summary>
        /// <param name="dwFlags">Specifies aspects of the formatting process and how to interpret the lpSource parameter. The low-order byte of dwFlags specifies how the function handles line breaks in the output buffer. The low-order byte can also specify the maximum width of a formatted output line.</param>
        /// <param name="lpSource">Specifies the location of the message definition. The type of this parameter depends upon the settings in the dwFlags parameter.</param>
        /// <param name="dwMessageId">Specifies the message identifier for the requested message. This parameter is ignored if dwFlags includes FORMAT_MESSAGE_FROM_STRING.</param>
        /// <param name="dwLanguageId">Specifies the language identifier for the requested message. This parameter is ignored if dwFlags includes FORMAT_MESSAGE_FROM_STRING.</param>
        /// <param name="lpBuffer">Pointer to a buffer for the formatted (and null-terminated) message. If dwFlags includes FORMAT_MESSAGE_ALLOCATE_BUFFER, the function allocates a buffer using the LocalAlloc function, and places the pointer to the buffer at the address specified in lpBuffer.</param>
        /// <param name="nSize">If the FORMAT_MESSAGE_ALLOCATE_BUFFER flag is not set, this parameter specifies the maximum number of TCHARs that can be stored in the output buffer. If FORMAT_MESSAGE_ALLOCATE_BUFFER is set, this parameter specifies the minimum number of TCHARs to allocate for an output buffer. For ANSI text, this is the number of bytes; for Unicode text, this is the number of characters.</param>
        /// <param name="Arguments">Pointer to an array of values that are used as insert values in the formatted message. A %1 in the format string indicates the first value in the Arguments array; a %2 indicates the second argument; and so on.</param>
        /// <returns>If the function succeeds, the return value is the number of TCHARs stored in the output buffer, excluding the terminating null character.<br></br><br>If the function fails, the return value is zero. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
        [DllImport("user32.dll", EntryPoint = "FormatMessageA", CharSet = CharSet.Ansi)]
        internal static extern int FormatMessage(int dwFlags, IntPtr lpSource, int dwMessageId, int dwLanguageId, StringBuilder lpBuffer, int nSize, int Arguments);
    }

    /// <summary>
    /// Specifies the type of restart options that an application can use.
    /// </summary>
    public enum RestartOptions {
        /// <summary>
        /// Shuts down all processes running in the security context of the process that called the ExitWindowsEx function. Then it logs the user off.
        /// </summary>
        LogOff = 0,
        /// <summary>
        /// Shuts down the system and turns off the power. The system must support the power-off feature.
        /// </summary>
        PowerOff = 8,
        /// <summary>
        /// Shuts down the system and then restarts the system.
        /// </summary>
        Reboot = 2,
        /// <summary>
        /// Shuts down the system to a point at which it is safe to turn off the power. All file buffers have been flushed to disk, and all running processes have stopped. If the system supports the power-off feature, the power is also turned off.
        /// </summary>
        ShutDown = 1,
        /// <summary>
        /// Suspends the system.
        /// </summary>
        Suspend = -1,
        /// <summary>
        /// Hibernates the system.
        /// </summary>
        Hibernate = -2,
    }

    /// <summary>
    /// An LUID is a 64-bit value guaranteed to be unique only on the system on which it was generated. The uniqueness of a locally unique identifier (LUID) is guaranteed only until the system is restarted.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LUID {
        /// <summary>
        /// The low order part of the 64 bit value.
        /// </summary>
        public int LowPart;
        /// <summary>
        /// The high order part of the 64 bit value.
        /// </summary>
        public int HighPart;
    }

    /// <summary>
    /// The LUID_AND_ATTRIBUTES structure represents a locally unique identifier (LUID) and its attributes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LUID_AND_ATTRIBUTES {
        /// <summary>
        /// Specifies an LUID value.
        /// </summary>
        public LUID pLuid;
        /// <summary>
        /// Specifies attributes of the LUID. This value contains up to 32 one-bit flags. Its meaning is dependent on the definition and use of the LUID.
        /// </summary>
        public int Attributes;
    }

    /// <summary>
    /// The TOKEN_PRIVILEGES structure contains information about a set of privileges for an access token.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TOKEN_PRIVILEGES {
        /// <summary>
        /// Specifies the number of entries in the Privileges array.
        /// </summary>
        public int PrivilegeCount;
        /// <summary>
        /// Specifies an array of LUID_AND_ATTRIBUTES structures. Each structure contains the LUID and attributes of a privilege.
        /// </summary>
        public LUID_AND_ATTRIBUTES Privileges;
    }

    /// <summary>
    /// Create a _SERVER_INFO_100 STRUCTURE
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SERVER_INFO_100 {
        internal int sv100_platform_id;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string sv100_name;
    }
}
