using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SOLTEC.SPOS.Negocio.Comun
{
    // Ref: https://stackoverflow.com/questions/19776716/c-sharp-windows-service-creates-process-but-doesnt-executes-it
    [SuppressUnmanagedCodeSecurity]
    public class ProcessHandler
    {
        public const int GENERIC_ALL_ACCESS = 0x10000000;
        //public const int CREATE_NO_WINDOW = 0x08000000;
        public const int STARTF_USESHOWWINDOW = 0x00000001;

        public const int SE_PRIVILEGE_ENABLED = 0x00000002;
        public const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
        internal const string SE_TCB_NAME = "SeTcbPrivilege";

        enum CreateProcessFlags
        {
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NEW_CONSOLE = 0x00000010,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_NO_WINDOW = 0x08000000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_SEPARATE_WOW_VDM = 0x00000800,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            CREATE_SUSPENDED = 0x00000004,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            DEBUG_PROCESS = 0x00000001,
            DETACHED_PROCESS = 0x00000008,
            EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
            INHERIT_PARENT_AFFINITY = 0x00010000
        }

        enum TOKEN_INFORMATION_CLASS
        {

            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            TokenElevationType,
            TokenLinkedToken,
            TokenElevation,
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            MaxTokenInfoClass
        }

        public enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        public enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }

        #region struct
        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public Int32 dwProcessID;
            public Int32 dwThreadID;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public Int32 Length;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TokPriv1Luid
        {
            public int Count;
            public long Luid;
            public int Attr;
        }
        #endregion

        #region Win32 API
        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr relen);

        [DllImport("advapi32.dll", EntryPoint = "ImpersonateLoggedOnUser", SetLastError = true,
         CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr ImpersonateLoggedOnUser(IntPtr hToken);

        [
           DllImport("kernel32.dll",
              EntryPoint = "CloseHandle", SetLastError = true,
              CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)
        ]

        public static extern bool CloseHandle(IntPtr handle);

        [
           DllImport("advapi32.dll",
              EntryPoint = "CreateProcessAsUser", SetLastError = true,
              CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)
        ]
        public static extern bool
           CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine,
                               ref SECURITY_ATTRIBUTES lpProcessAttributes, ref SECURITY_ATTRIBUTES lpThreadAttributes,
                               bool bInheritHandle, Int32 dwCreationFlags, IntPtr lpEnvrionment,
                               string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
                               ref PROCESS_INFORMATION lpProcessInformation);

        [
           DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx")
        ]
        public static extern bool
           DuplicateTokenEx(IntPtr hExistingToken, Int32 dwDesiredAccess,
                            ref SECURITY_ATTRIBUTES lpThreadAttributes,
                            Int32 ImpersonationLevel, Int32 dwTokenType,
                            ref IntPtr phNewToken);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool RevertToSelf();

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern IntPtr WTSGetActiveConsoleSessionId();

        [DllImport("advapi32.dll")]
        public static extern IntPtr SetTokenInformation(IntPtr TokenHandle, IntPtr TokenInformationClass, IntPtr TokenInformation, IntPtr TokenInformationLength);


        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr Token);
        #endregion

        private static int GetCurrentUserSessionID()
        {
            uint dwSessionId = (uint)WTSGetActiveConsoleSessionId();

            // gets the Id of the User logged in with WinLogOn
            Process[] processes = Process.GetProcessesByName("winlogon");
            foreach (Process p in processes)
            {
                if ((uint)p.SessionId == dwSessionId)
                {

                    //　this is the process controlled by the same sessionID
                    return p.SessionId;
                }
            }

            return -1;
        }

        /// <summary>
        /// Main method for Create process used advapi32: CreateProcessAsUser
        /// </summary>
        /// <param name="filePath">Execute path, for example: c:\app\myapp.exe</param>
        /// <param name="args">Arugments passing to execute application</param>
        /// <returns>Process just been created</returns>
        public static Process CreateProcessAsUser(string filePath, string args)
        {
            var dupedToken = IntPtr.Zero;

            var pi = new PROCESS_INFORMATION();
            var sa = new SECURITY_ATTRIBUTES();
            sa.Length = Marshal.SizeOf(sa);

            try
            {
                // get current token
                var token = WindowsIdentity.GetCurrent().Token;

                var si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(si);
                si.lpDesktop = "";
                si.dwFlags = STARTF_USESHOWWINDOW;

                var dir = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                // Create new access token for current token
                if (!DuplicateTokenEx(
                    token,
                    GENERIC_ALL_ACCESS,
                    ref sa,
                    (int)SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
                    (int)TOKEN_TYPE.TokenPrimary,
                    ref dupedToken
                ))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // got the session Id from user level
                uint curSessionid = (uint)GetCurrentUserSessionID();

#if !DEBUG      //The next block of code is on if !DEBUG because privilege not held by client, we need to sure your service was logged on as Local System account for grant the desired permission
                // retrieve the primary access token for the user associated with the specified session Id.
                if (!WTSQueryUserToken(curSessionid, out dupedToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
#endif
                WindowsIdentity.RunImpersonated(WindowsIdentity.GetCurrent().AccessToken, () =>
                {
                    if (!CreateProcessAsUser(
                                          dupedToken, // user token
                                          filePath, // app name or path
                                          string.Format("\"{0}\" {1}", fileName.Replace("\"", "\"\""), args), // command line
                                          ref sa, // process attributes
                                          ref sa, // thread attributes
                                          false, // do not inherit handles
                                          (int)CreateProcessFlags.CREATE_NEW_CONSOLE, //flags
                                          IntPtr.Zero, // environment block
                                          dir, // current dir
                                          ref si, // startup info
                                          ref pi // process info
                                  ))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                });

                return Process.GetProcessById(pi.dwProcessID);
            }
            finally
            {
                // close all open resource
                if (pi.hProcess != IntPtr.Zero)
                    CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero)
                    CloseHandle(pi.hThread);
                if (dupedToken != IntPtr.Zero)
                    CloseHandle(dupedToken);
            }
        }
    }
}
