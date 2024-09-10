using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class MemoryManager
{
    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    private Process _process;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    public bool WriteFloat(IntPtr address, float value)
    {
        byte[] buffer = BitConverter.GetBytes(value);
        return WriteProcessMemory(_process.Handle, address, buffer, buffer.Length, out _);
    }

    public MemoryManager(Process process)
    {
        _process = process;
    }

    public int ReadInt32(IntPtr address)
    {
        byte[] buffer = new byte[4];
        ReadProcessMemory(_process.Handle, address, buffer, buffer.Length, out int bytesRead);
        return BitConverter.ToInt32(buffer, 0);
    }

    public float ReadFloat(IntPtr address)
    {
        byte[] buffer = new byte[4];
        ReadProcessMemory(_process.Handle, address, buffer, buffer.Length, out int bytesRead);
        return BitConverter.ToSingle(buffer, 0);
    }

    public IntPtr ReadIntPtr(IntPtr address)
    {
        byte[] buffer = new byte[IntPtr.Size];
        ReadProcessMemory(_process.Handle, address, buffer, buffer.Length, out int bytesRead);
        return (IntPtr.Size == 8) ? (IntPtr)BitConverter.ToInt64(buffer, 0) : (IntPtr)BitConverter.ToInt32(buffer, 0);
    }
}