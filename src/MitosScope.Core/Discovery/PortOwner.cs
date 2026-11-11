using System.Runtime.InteropServices;

namespace MitosScope.Core.Discovery;

/// <summary>
/// Finds which process listens on a local TCP port (what "netstat -ano" shows). Used to tie an
/// API port to the game process/window. Windows only for now (Linux: /proc/net/tcp, macOS: lsof).
/// </summary>
public static partial class PortOwner
{
    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_LISTENER = 3;
    private const uint NO_ERROR = 0;
    private const int RowSize = 24; // MIB_TCPROW_OWNER_PID: state, localAddr, localPort, remoteAddr, remotePort, owningPid

    public static int? FindListeningProcessId(int port)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var size = 0;
        _ = GetExtendedTcpTable(0, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0);

        for (var attempt = 0; attempt < 3; attempt++) // the table can grow between the two calls
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(buffer, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0) != NO_ERROR)
                    continue;

                var count = Marshal.ReadInt32(buffer);
                for (var i = 0; i < count; i++)
                {
                    var row = buffer + 4 + i * RowSize;
                    var rawPort = (uint)Marshal.ReadInt32(row, 8);
                    var localPort = (int)(((rawPort & 0xFF) << 8) | ((rawPort >> 8) & 0xFF)); // network byte order
                    if (localPort == port)
                        return Marshal.ReadInt32(row, 20);
                }
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        return null;
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial uint GetExtendedTcpTable(nint pTcpTable, ref int pdwSize,
        [MarshalAs(UnmanagedType.Bool)] bool bOrder, int ulAf, int tableClass, uint reserved);
}
