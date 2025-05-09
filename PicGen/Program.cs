using System.Drawing;

namespace PicGen
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Encode();
        }

        private static void Encode()
        {
            // 240 x 135
            var bmp = new Bitmap(240, 135);

            // encode each pixel with its coordinate in RGB555
            for (var x = 0; x < bmp.Width; x++)
            {
                for (var y = 0; y < bmp.Height; y++)
                {
                    var position = y << 8 | x;
                    
                    // losing msb of y
                    var nr = (position >> 10) & 0b11111;
                    var ng = (position >> 5) & 0b11111;
                    var nb = position & 0b11111;

                    // encode coordinate in RGB
                    var color = Color.FromArgb(255, nr << 3, ng << 3, nb << 3);
                    bmp.SetPixel(x, y, color);
                }
            }
            
            bmp.Save("encoded-rgb555.bmp");
        }
    }
}