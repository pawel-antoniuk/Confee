using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfeeDemoWPF
{
    static class BitmapLoader
    {
        private static readonly Random Random = new Random();

        public static Dictionary<string, List<Bitmap>> LoadLibrary(string pathStr)
        {
            var bitmaps = new Dictionary<string, List<Bitmap>>();
            var path = new DirectoryInfo(pathStr);
            foreach (var dict in path.EnumerateDirectories())
            {
                foreach (var file in dict.EnumerateFiles())
                {
                    var bitmap = LoadBitmap(file.FullName);
                    if (!bitmaps.ContainsKey(dict.Name))
                    {
                        bitmaps.Add(dict.Name, new List<Bitmap>());
                    }
                    bitmaps[dict.Name].Add(bitmap);
                }
            }
            return bitmaps;
        }

        public static Bitmap LoadBitmap(string pathStr)
        {
            return (Bitmap)Image.FromFile(pathStr);
        }

        public static void SaveInLibrary(string pathStr, string label, IntPtr data,
            int width, int height, int stride, PixelFormat format)
        {
            var bitmap = new Bitmap(width, height, stride, format, data);
            var outputPath = pathStr + "\\" + label + "\\" + Random.Next() + ".bmp";
            bitmap.Save(outputPath, ImageFormat.Bmp);
        }
    }
}
