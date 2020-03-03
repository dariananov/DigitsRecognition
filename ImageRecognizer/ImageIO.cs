using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace ImageReadCS
{
    public unsafe class ImageIO
    {

        public static LockBitmapInfo LockBitmap(Bitmap B)
        {
            return LockBitmap(B, PixelFormat.Format32bppRgb, 4);
        }

        public static LockBitmapInfo LockBitmap(Bitmap B, PixelFormat pf, int pixelsize)
        {
            LockBitmapInfo lbi;
            GraphicsUnit unit = GraphicsUnit.Pixel;
            RectangleF boundsF = B.GetBounds(ref unit);
            Rectangle bounds = new Rectangle((int)boundsF.X,
                (int)boundsF.Y,
                (int)boundsF.Width,
                (int)boundsF.Height);
            lbi.B = B;
            lbi.Width = (int)boundsF.Width;
            lbi.Height = (int)boundsF.Height;
            lbi.bitmapData = B.LockBits(bounds, ImageLockMode.ReadWrite, pf);
            lbi.linewidth = lbi.bitmapData.Stride;
            lbi.data = (byte*)(lbi.bitmapData.Scan0.ToPointer());
            return lbi;
        }

        public static void UnlockBitmap(LockBitmapInfo lbi)
        {
            lbi.B.UnlockBits(lbi.bitmapData);
            lbi.bitmapData = null;
            lbi.data = null;
        }

        public unsafe struct LockBitmapInfo
        {
            public Bitmap B;
            public int linewidth;
            public BitmapData bitmapData;
            public byte* data;
            public int Width, Height;
        }

        public static GrayscaleFloatImage BitmapToGrayscaleFloatImage(Bitmap B)
        {
            int W = B.Width, H = B.Height;
            GrayscaleFloatImage res = new GrayscaleFloatImage(W, H);

            if (B.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                Color[] pi = B.Palette.Entries;
                byte[] pal = new byte[1024];
                for (int i = 0; i < pi.Length; i++)
                {
                    Color C = pi[i];
                    pal[i * 4] = C.B;
                    pal[i * 4 + 1] = C.G;
                    pal[i * 4 + 2] = C.R;
                    pal[i * 4 + 3] = C.A;
                }

                LockBitmapInfo lbi = LockBitmap(B, PixelFormat.Format8bppIndexed, 1);
                try
                {
                    for (int j = 0; j < H; j++)
                        for (int i = 0; i < W; i++)
                        {
                            int c = lbi.data[lbi.linewidth * j + i];
                            int b = pal[c * 4];
                            int g = pal[c * 4 + 1];
                            int r = pal[c * 4 + 2];
                            res[i, j] = 0.114f * b + 0.587f * g + 0.299f * r;
                        }
                }
                finally
                {
                    UnlockBitmap(lbi);
                }
            }
            else
            {
                LockBitmapInfo lbi = LockBitmap(B);
                try
                {
                    for (int j = 0; j < H; j++)
                        for (int i = 0; i < W; i++)
                        {
                            int b = lbi.data[lbi.linewidth * j + i * 4];
                            int g = lbi.data[lbi.linewidth * j + i * 4 + 1];
                            int r = lbi.data[lbi.linewidth * j + i * 4 + 2];
                            res[i, j] = 0.114f * b + 0.587f * g + 0.299f * r;
                        }
                }
                finally
                {
                    UnlockBitmap(lbi);
                }
            }

            return res;
        }

        public static GrayscaleFloatImage FileToGrayscaleFloatImage(string filename)
        {

            Bitmap B = new Bitmap(filename);
            GrayscaleFloatImage res = BitmapToGrayscaleFloatImage(B);
            B.Dispose();
            return res;
        }
    }
   
}
