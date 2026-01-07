using System;
using System.Collections.Generic;
using System.IO;
using Vortice.Direct2D1;

namespace NormalMap_Iyahon
{
    public static class TextureManager
    {
        class CacheEntry
        {
            public ID2D1Bitmap Bitmap = null!;
            public int ReferenceCount = 0;
        }
    
        private static readonly Dictionary<string, CacheEntry> _fileCache = new();
        private static readonly object _lock = new object();

        public static ID2D1Bitmap LoadTexture(ID2D1DeviceContext dc, string path)
        {
            if (string.IsNullOrEmpty(path)) return null!;

            lock (_lock)
            {
                if (_fileCache.ContainsKey(path))
                {
                    var entry = _fileCache[path];
                    if (entry.Bitmap != null)
                    {
                        entry.ReferenceCount++;
                        return entry.Bitmap;
                    }
                    else
                    {
                        _fileCache.Remove(path);
                    }
                }

                if (File.Exists(path))
                {
                    try
                    {
                        var bitmap = LoadBitmapInternal(dc, path);
                        _fileCache[path] = new CacheEntry
                        {
                            Bitmap = bitmap,
                            ReferenceCount = 1
                        };
                        return bitmap;
                    }
                    catch
                    {
                        return null!;
                    }
                }

                return null!;
            }
        }

        public static void ReleaseTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            lock (_lock)
            {
                if (_fileCache.ContainsKey(path))
                {
                    var entry = _fileCache[path];
                    entry.ReferenceCount--;

                    if (entry.ReferenceCount <= 0)
                    {
                        entry.Bitmap?.Dispose();
                        _fileCache.Remove(path);
                    }
                }
            }
        }

        private static ID2D1Bitmap LoadBitmapInternal(ID2D1DeviceContext dc, string path)
        {
            using var factory = new Vortice.WIC.IWICImagingFactory();
            using var decoder = factory.CreateDecoderFromFileName(path);
            using var frame = decoder.GetFrame(0);
            using var converter = factory.CreateFormatConverter();
            converter.Initialize(frame, Vortice.WIC.PixelFormat.Format32bppPBGRA);
            return dc.CreateBitmapFromWicBitmap(converter, null);
        }
    }



}