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

                // Send(stream, 0x01);
                SendConfigFrame(stream, 1, 1, 1);
                // Send(stream, 0x02);
                // Send(stream, 0x23); // TODO: is this a reset command?
                // Send(stream, 0x01);

                // // TODO: there is a weird issue that's sometimes resolved by a delay, but also sometimes not, where the first few pixels in the first row stay white.
                // Thread.Sleep(500);
                //
                // // TODO: change to non-hardcoded paths
                // SendPicture(stream, @"D:\projects\gmk87-usb-reverse\nyan.bmp", 0);
                // SendPicture(stream, @"D:\projects\gmk87-usb-reverse\encoded-rgb555.bmp", 1);
                
                // Send(stream, 0x02);

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
        /*
         * 0x04 values:
         * 0x30; normal lighting
         * 0x31; same
         * 0x32; same
         * 0x33; same
         *
         * 0x29; backlight and led logo off
         * 0x28 - 0x24; same
         *
         */

        command[0x09] = 0x08; // underglow effect; mine: 0x08
        /*
         * 0x09 values:
         * 0x00: off
         * 0x01: horizontal dimming wave
         * 0x02: horizontal pulse wave
         * 0x03: waterfall
         * 0x04: full on, cycling colors
         * 0x05: breathing
         * 0x06: full on, one color
         * 0x07: glow pressed key
         * 0x08: glow spreading from pressed key
         * 0x09: glow row of pressed key
         * 0x0a: random pattern, one color
         * 0x0b: full on, rainbow color cycle
         * 0x0c: full on, rainbow waterfall
         * 0x0d: continuous wave originating from center, one color
         * 0x0e: circling j/k keys, then spreading outward, one color
         * 0x0f: raining, one color
         * 0x10: wave left/right back and forth, one color
         * 0x11: full on, slow color saturation cycle
         * 0x12: full on, slow outward rainbow origination from center
         * 0x13+ not tested
         */

        command[0x0a] = 0x08; // underglow brightness; mine: 0x08 (0x00=off 0x01=dim .. 0x09=bright; 0x0a+ weird behavior)
        command[0x0b] = 0x01; // underglow speed; mine: 0x01; 0x00=fast, higher=slower (max 0xff, but 0x10 is already quite slow)

        command[0x0e] = 0x18; // ??? mine: 0x18 ??? 0x00 color is green, towards value 0xff it goes yellow
        command[0x0f] = 0xff; // ??? mine: 0xff
        /*
         * 0x0f values:
         * 0x00: dim red
         * 0x10: orange-yellow
         * 0x20: yellow-green
         * 0x40: green
         * 0x80: greener
         * up higher still more pure green?
         */

        command[0x11] = 0x0d; // ??? mine: 0x0d
        command[0x1c] = 0xff; // ??? mine: 0xff

        command[0x24] = 0x00; // mine: 0x00; "logo led" effect??
        /*
         * 0x24 values:
         * 0x00: color pulse
         * 0x01: rainbow
         * 0x02: color pulse
         * 0x03: solid color
         * 0x04: solid color
         * 0x05: solid color
         * 0x06-0x08: off
         * 0x09+: not tested
         */

        command[0x25] = 0x09; // ??? mine: 0x09; "logo led" brightness 0x00=off .. 0x09=bright
        command[0x26] = 0x02; // ??? mine: 0x02
        command[0x27] = 0x00; // mine: 0x00; "logo led" effect as well??
        /*
         * 0x27 values:
         * 0x00: color pulse
         * 0x01 - 0x0a: various rainbow/color cycle effects
         *
         */

        command[0x28] = 0x01; // mine: 0x01; "logo led" color
        /*
         * 0x28 values:
         * 0x00: red
         * 0x01: orange
         * 0x02: yellow
         * 0x03: green
         * 0x04: teal
         * 0x05: blue
         * 0x06: purple
         * 0x07: white
         * 0x08: off
         * 0x09+ are repeating colors in some order?
         * 0x09: blue
         * 0x0a: red
         * 0x0b: white
         * 0x0c: teal
         * 0x0d: white
         * 0x0e: blue
         * 0x0f: white
         * 0x10+: not tested
         */

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