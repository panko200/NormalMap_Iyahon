using System;
using System.Collections.Generic;
using System.IO;
using Vortice.Direct2D1;
using Vortice.WIC;

namespace NormalMap_Iyahon
{
    public interface IGeneratedMapProvider
    {
        ID2D1Image? GenerateImage();
    }

    public static class TextureManager
    {
        class CacheEntry
        {
            public IWICBitmapSource Bitmap = null!;
            public int ReferenceCount = 0;
        }

        private static readonly Dictionary<string, CacheEntry> _fileCache = new();
        private static readonly object _lock = new object();


        private static readonly Dictionary<int, IGeneratedMapProvider> _providers = new();
        public static void RegisterProvider(int id, IGeneratedMapProvider provider) { lock (_lock) _providers[id] = provider; }
        public static void UnregisterProvider(int id, IGeneratedMapProvider provider) { lock (_lock) { if (_providers.ContainsKey(id) && _providers[id] == provider) _providers.Remove(id); } }
        public static IGeneratedMapProvider? GetProvider(int id) { lock (_lock) { return _providers.ContainsKey(id) ? _providers[id] : null; } }


        public static IWICBitmapSource? LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            lock (_lock)
            {
                if (_fileCache.ContainsKey(path))
                {
                    var entry = _fileCache[path];
                    entry.ReferenceCount++;
                    return entry.Bitmap;
                }

                if (File.Exists(path))
                {
                    try
                    {
                        // ★修正: ここで毎回ファクトリを生成してusingする
                        using var wicFactory = new IWICImagingFactory();

                        using var decoder = wicFactory.CreateDecoderFromFileName(path);
                        using var frame = decoder.GetFrame(0);

                        var converter = wicFactory.CreateFormatConverter();
                        converter.Initialize(frame, PixelFormat.Format32bppPBGRA);

                        // ファクトリ経由でBitmapSourceを作成
                        var bitmap = wicFactory.CreateBitmapFromSource(converter, BitmapCreateCacheOption.CacheOnLoad);

                        converter.Dispose();

                        _fileCache[path] = new CacheEntry { Bitmap = bitmap, ReferenceCount = 1 };
                        return bitmap;
                    }
                    catch
                    {
                        return null;
                    }
                }
                return null;
            }
        }

        // ... (ReleaseTexture は変更なし) ...
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
                        entry.Bitmap.Dispose();
                        _fileCache.Remove(path);
                    }
                }
            }
        }
    }

}