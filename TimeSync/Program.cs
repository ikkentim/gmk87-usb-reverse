using HidSharp;
using HidSharp.Reports;

namespace TimeSync;

public class Program
{
    private static async Task Main(string[] args)
    {
        await ConnectAndSend();
    }
    
    private static async Task ConnectAndSend()
    {
        var empty = new byte[60];

        var hidDevices = DeviceList.Local.GetHidDevices();
        foreach (var device in hidDevices)
        {
            // Vendor ID                : 0x320F
            // Product ID               : 0x5055
            if (device.VendorID == 0x320f && device.ProductID == 0x5055)
            {
                var dsc = device.GetReportDescriptor();
                if (dsc.TryGetReport(ReportType.Output, 0x04, out var rpt))
                {   
                    Console.WriteLine($"Device Found: {device.GetProductName()}");
                    Console.WriteLine($"Vendor ID: {device.VendorID}, Product ID: {device.ProductID}");
                }
                else
                {
                    continue;
                }
                

                // Open the device
                await using var stream = device.Open();

                // Console.WriteLine("start freeze?");
                // Send(stream, 0x23, empty); // freeze?
                //
                // await Task.Delay(1000);
                //
                // Console.WriteLine("save?");
                // Send(stream, 0x02, empty); // save?

                SendConfigFrame(stream);

                return;
            }
        }

        Console.WriteLine("No device with PID 5055 found.");
    }

    private static void Send(HidStream stream, byte command, ReadOnlySpan<byte> data)
    {
        if (data.Length != 60)
        {
            throw new ArgumentException("invalid data len", nameof(data));
        }

        Span<byte> buf = stackalloc byte[64];

        buf[0] = 0x04; // report ID
        buf[3] = command; // command ID
        data.CopyTo(buf[4..]);
        
        var chk = Checksum(buf[3..]);
        buf[1] = (byte)(chk & 0xff); // checksum LSB
        buf[2] = (byte)((chk >> 8) & 0xff); // checksum MSB

        stream.Write(buf);
        stream.Flush();
    }
    private static void SendConfigFrame(HidStream stream)
    {
        var date = DateTime.Now;
        
        const ushort frameDuration = 1000;//in ms
        var frameDurationMsb = (byte)(frameDuration >> 8);
        var frameDurationLsb = (byte)(frameDuration & 0xFF);

        // config command
        var command = new byte[64];

        command[0x04] = 0x30; // ???
        command[0x09] = 0x08; // ???
        command[0x0a] = 0x08; // ???
        command[0x0b] = 0x01; // ???
        command[0x0e] = 0x18; // ???
        command[0x0f] = 0xff; // ???

        command[0x11] = 0x0d; // ???
        command[0x1c] = 0xff; // ???

        command[0x25] = 0x09; // ???
        command[0x26] = 0x02; // ???
        command[0x28] = 0x01; // ???
        command[0x29] = 0;// show image 0(time)/1/2
        command[0x2a] = 2;// frame count  in image 1
        command[0x2b] = ToHexNum(date.Second);
        command[0x2c] = ToHexNum(date.Minute);
        command[0x2d] = ToHexNum(date.Hour);
        command[0x2e] = (byte)date.DayOfWeek;
        command[0x2f] = ToHexNum(date.Day);

        command[0x30] = ToHexNum(date.Month);
        command[0x31] = ToHexNum(date.Year % 100);
        command[0x33] = frameDurationLsb;
        command[0x34] = frameDurationMsb;
        command[0x36] = 3; // frame count in image 2


        Send(stream, 0x06, command.AsSpan(4));
    }

    /// <summary>
    /// Convert a number 0-99 to a hex byte that represents the number.
    /// e.g. 34 -> 0x34, 99 -> 0x99
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static byte ToHexNum(int num)
    {
        if (num is >= 100 or < 0)
            throw new ArgumentOutOfRangeException(nameof(num));
        var low = num % 10;
        var high = num / 10;

        return (byte)(low + high * 16);
    }

    /// <summary>
    /// GMK checksum.
    /// </summary>
    private static ushort Checksum(ReadOnlySpan<byte> buf)
    {
        ushort chk = 0;

        foreach (var v in buf)
        {
            chk += v;
        }

        return chk;
    }
}