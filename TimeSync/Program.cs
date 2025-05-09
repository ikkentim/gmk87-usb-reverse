using System;
using HidSharp;
using HidSharp.Reports;

class Program
{
    static void PicGen()
    {

    }
    static void Main(string[] args)
    {
        // Initialize the HID device list
        DeviceList deviceList = DeviceList.Local;

        // Find all HID devices
        var hidDevices = deviceList.GetHidDevices();

        // Search for a device with PID 5055
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
                using (var stream = device.Open())
                {
                    // blueprint
                    byte[] command =
                    [
                        0x04, 0x2c, 0x03, 0x06, 0x30, 0x00, 0x00, 0x00, 0x00, 0x08, 0x08, 0x01, 0x00, 0x00, 0x18, 0xff,
                        0x00, 0x0d, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x09, 0x02, 0x00, 0x01, 0x00, 0x01, 0x04, 0x16, 0x00, 0x04, 0x08,
                        0x05, 0x25, 0x00, 0x64, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                    ];

                    var date = DateTime.Now;
                    command[0x2b] = ToHexNum(date.Second);
                    command[0x2c] = ToHexNum(date.Minute);
                    command[0x2d] = ToHexNum(date.Hour);
                    command[0x2e] = (byte)date.DayOfWeek;
                    command[0x2f] = ToHexNum(date.Day);
                    command[0x30] = ToHexNum(date.Month);
                    command[0x31] = ToHexNum(date.Year % 100);
                    command[0x29] = 0x0;// show image 0(time)/1/2

                    const ushort frameDuration = 1000;//in ms
                    var frameDurationMsb = (byte)(frameDuration >> 8);
                    var frameDurationLsb = (byte)(frameDuration & 0xFF);
                    command[0x33] = frameDurationLsb;
                    command[0x34] = frameDurationMsb;
                    command[0x2a] = 2;// frame count  in image 1
                    command[0x36] = 3; // frame count in image 2

                    command[0x02] = 0x03; // I've seen 0x03 and 0x04; meaning unclear
                    command[0x11] = 0x0d; // I've only ever seen 0x0d; meaning unclear
                    command[0x1c] = 0xff; // I've only ever seen 0xff; meaning unclear

                    command[0x25] = 0x09; // I've only ever seen 0x09; meaning unclear
                    command[0x26] = 0x02; // I've only ever seen 0x02; meaning unclear
                    command[0x28] = 0x01; // I've only ever seen 0x01; meaning unclear

                    command[0x01] = Checksum(command.AsSpan(2));

                    command[0x03] = 0x02; // I've only ever seen 0x06; meaning unclear

                    // command theory:
                    // 0x06: "config" frame
                    // 0x02: "end of stream" frame?
                    // 0x21: image upload

                    // overwrite stuff
                    stream.Write(command, 0, command.Length);
                    Console.WriteLine("Command sent to device.");
                }
                return;
            }
        }

        Console.WriteLine("No device with PID 5055 found.");
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
    /// GMK checksum. At least for the "config" frame we're working with.
    /// </summary>
    private static byte Checksum(ReadOnlySpan<byte> buf)
    {
        byte chk = 0xfd;

        foreach (var v in buf)
        {
            chk += v;
        }

        return chk;
    }
}
