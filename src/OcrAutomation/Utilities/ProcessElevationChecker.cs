using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Diagnostics;

namespace OcrAutomation.Utilities;

public static class ProcessElevationChecker
{
    /// <summary>
    /// Checks if a process with the given process ID is running with elevated privileges.
    /// </summary>
    public static bool IsProcessElevated(int processId)
    {
        try
        {
            // Try to open the process with query information access
            var processHandle = Win32Api.OpenProcess(
                Win32Api.ProcessAccessFlags.QueryInformation,
                false,
                processId);

            if (processHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                // Access denied typically means the process is elevated
                if (error == Win32Api.ERROR_ACCESS_DENIED)
                {
                    return true;
                }
                return false;
            }

            try
            {
                // Successfully opened process, check token elevation
                if (Win32Api.OpenProcessToken(processHandle, Win32Api.TOKEN_QUERY, out var tokenHandle))
                {
                    try
                    {
                        var elevationResult = new Win32Api.TOKEN_ELEVATION();
                        var elevationResultSize = Marshal.SizeOf(elevationResult);
                        var elevationPtr = Marshal.AllocHGlobal(elevationResultSize);

                        try
                        {
                            var success = Win32Api.GetTokenInformation(
                                tokenHandle,
                                Win32Api.TokenElevation,
                                elevationPtr,
                                (uint)elevationResultSize,
                                out _);

                            if (success)
                            {
                                elevationResult = Marshal.PtrToStructure<Win32Api.TOKEN_ELEVATION>(elevationPtr);
                                return elevationResult.TokenIsElevated != 0;
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(elevationPtr);
                        }
                    }
                    finally
                    {
                        Win32Api.CloseHandle(tokenHandle);
                    }
                }
            }
            finally
            {
                Win32Api.CloseHandle(processHandle);
            }
        }
        catch (Exception)
        {
            // If any error occurs, assume not elevated for safety
            return false;
        }

        return false;
    }

    /// <summary>
    /// Checks if the current process is running with elevated privileges.
    /// </summary>
    public static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
