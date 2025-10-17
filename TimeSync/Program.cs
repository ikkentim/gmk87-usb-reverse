using HidSharp;
using HidSharp.Reports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TimeSync;

public static class Program
{
    private static byte[] empty = new byte[60];

    private static async Task Main(string[] args)
    {
        await ConnectAndSend();
    }
    
    private static async Task ConnectAndSend()
    {

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
                // Send(stream, 0x23); // freeze?
                //
                // await Task.Delay(1000);
                //
                // Console.WriteLine("save?");
                // Send(stream, 0x02); // save?
                
                Send(stream, 0x01);
                SendConfigFrame(stream, 1, 1, 1);
                Send(stream, 0x02);
                Send(stream, 0x23);
                Send(stream, 0x01);

                // TODO: there is a weird issue that's sometimes resolved by a delay, but also sometimes not, where the first few pixels in the first row stay white.
                // Thread.Sleep(500);

                SendPicture(stream, @"D:\projects\gmk87-usb-reverse\nyan.bmp", 0);
                SendPicture(stream, @"D:\projects\gmk87-usb-reverse\encoded-rgb555.bmp", 1);
                
                Send(stream, 0x02);

                return;
            }
        }

        Console.WriteLine("No device with PID 5055 found.");
    }

    private static void SendPicture(HidStream stream, string path, byte imageIndex)
    {
        using var image = Image.Load<Rgba32>(path);
        SendImageFrame(stream, image, imageIndex);
    }

    private static void SendImageFrame(HidStream stream, Image<Rgba32> image, byte imageIndex)
    {
        const int imageWidth = 240;
        const int imageHeight = 135;

        var startOffset = imageIndex * 0x28; // ?? TODO: investigate further

        var bufIndex = 0x08;
        var command = new byte[64];

        void Transmit()
        {
            if (bufIndex == 0x08)
            {
                return;
            }

            var startOffsetLsb = (byte)(startOffset & 0xff);
            var startOffsetMsb = (byte)((startOffset >> 8) & 0xff);

            command[0x04] = 0x38; // data bytes in frame
            command[0x05] = startOffsetLsb;
            command[0x06] = startOffsetMsb;
            command[0x07] = imageIndex; // maybe an encoding type identifier

            Send(stream, 0x21, command.AsSpan(4));

            // reset buffer
            const int num = 64 - 8;
            startOffset += num;

            bufIndex = 0x08;
            for (var q = bufIndex; q < command.Length; q++)
            {
                command[q] = 0;
            }
        }

        for (var y = 0; y < imageHeight; y++)
        {
            for (var x = 0; x < imageWidth; x++)
            {
                var pixel = image[x, y];

                // convert to RGB565
                var r = (byte)(pixel.R >> 3);
                var g = (byte)(pixel.G >> 2);
                var b = (byte)(pixel.B >> 3);

                var rgb565 = (ushort)((r << 11) | (g << 5) | b);
                
                command[bufIndex++] = (byte)((rgb565 >> 8) & 0xff); // MSB
                command[bufIndex++] =  (byte)(rgb565 & 0xff); // LSB

                // command[bufIndex++] = (byte)(rgb555 & 0xff); // LSB
                // command[bufIndex++] = (byte)((rgb555 >> 8) & 0xff); // MSB
                 
                if (bufIndex >= 64)
                {
                    Transmit();
                }
            }
        }

        Transmit();
    }

    private static void SendConfigFrame(HidStream stream, byte shownImage = 0, byte image0NumOfFrames = 1, byte image1NumOfFrames = 1)
    {
        var date = DateTime.Now;
        
        const ushort frameDuration = 1000;//in ms
        const byte frameDurationMsb = (byte)(frameDuration >> 8);
        const byte frameDurationLsb = (byte)(frameDuration & 0xFF);

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

        command[0x29] = shownImage;// show image 0(time)/1/2
        command[0x2a] = image0NumOfFrames;// frame count  in image 1
        command[0x2b] = ToHexNum(date.Second);
        command[0x2c] = ToHexNum(date.Minute);
        command[0x2d] = ToHexNum(date.Hour);
        command[0x2e] = (byte)date.DayOfWeek;
        command[0x2f] = ToHexNum(date.Day);

        command[0x30] = ToHexNum(date.Month);
        command[0x31] = ToHexNum(date.Year % 100);
        command[0x33] = frameDurationLsb;
        command[0x34] = frameDurationMsb;
        command[0x36] = image1NumOfFrames; // frame count in image 2


        Send(stream, 0x06, command.AsSpan(4));
    }

    private static void Send(HidStream stream, byte command)
    {
        Send(stream, command, empty);
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