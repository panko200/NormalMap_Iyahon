using NormalMap_Iyahon;
using System;
using System.IO;
using System.Numerics;
using System.Windows.Forms;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.WIC;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using static NormalMap_Iyahon.NormalMapGeneratorEffect;

namespace NormalMap_Iyahon
{
    internal class NormalMapGeneratorEffectProcessor : IVideoEffectProcessor, IDisposable
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly NormalMapGeneratorEffect _item;
        private ID2D1Image? _input;

        private GenerateNormalMapCustomEffect? _generatorEffect;
        private GaussianBlur? _blurEffect;

        private ID2D1Image? _lastOutput;

        public NormalMapGeneratorEffectProcessor(IGraphicsDevicesAndContext devices, NormalMapGeneratorEffect item)
        {
            _devices = devices;
            _item = item;

            var dc = _devices.DeviceContext;
            _generatorEffect = new GenerateNormalMapCustomEffect(devices);
            _blurEffect = new GaussianBlur(dc);
        }

        public DrawDescription Update(EffectDescription desc)
        {
            if (_lastOutput != null) { _lastOutput.Dispose(); _lastOutput = null; }
            if (_input == null) return desc.DrawDescription;

            var frame = desc.ItemPosition.Frame;
            var len = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var dc = _devices.DeviceContext;

            float strength = (float)_item.Strength.GetValue(frame, len, fps);
            float radius = (float)_item.Radius.GetValue(frame, len, fps);
            float blur = (float)_item.Blur.GetValue(frame, len, fps);

            float detailStrength = (float)_item.DetailStrength.GetValue(frame, len, fps);
            float detailRadius = (float)_item.DetailRadius.GetValue(frame, len, fps);

            float thresholdAlpha = (float)_item.ThresholdAlpha.GetValue(frame, len, fps) / 100.0f;
            float thresholdBright = (float)_item.ThresholdBright.GetValue(frame, len, fps) / 100.0f;
            float thresholdDiff = (float)_item.ThresholdDiff.GetValue(frame, len, fps) / 100.0f;

            float curve = (float)_item.Curve.GetValue(frame, len, fps);
            int format = (int)_item.Format;

            // ★追加
            int outputMode = (int)_item.OutputMode;
            float invert = _item.Invert ? 1.0f : 0.0f;

            int mode = (int)_item.Algorithm;

            var bounds = dc.GetImageLocalBounds(_input);
            float w = bounds.Right - bounds.Left;
            float h = bounds.Bottom - bounds.Top;
            if (w <= 0 || h <= 0) return desc.DrawDescription;

            // 1. ぼかし
            ID2D1Image sourceImage = _input;
            if (blur > 0.01f)
            {
                _blurEffect!.SetInput(0, _input, true);
                _blurEffect.StandardDeviation = blur;
                _blurEffect.Optimization = GaussianBlurOptimization.Speed;
                _blurEffect.BorderMode = BorderMode.Hard;
                sourceImage = _blurEffect.Output;
            }

            // 2. 生成
            _generatorEffect!.SetInput(0, sourceImage, true);
            _generatorEffect.Strength = strength;
            _generatorEffect.Radius = radius;
            _generatorEffect.Size = new Vector2(1.0f / w, 1.0f / h);
            _generatorEffect.Mode = mode;
            _generatorEffect.DetailStrength = detailStrength;
            _generatorEffect.DetailRadius = detailRadius;
            _generatorEffect.ThresholdAlpha = thresholdAlpha;
            _generatorEffect.ThresholdBright = thresholdBright;
            _generatorEffect.ThresholdDiff = thresholdDiff;
            _generatorEffect.Curve = curve;
            _generatorEffect.Format = format;
            _generatorEffect.OutputMode = outputMode; // ★追加
            _generatorEffect.Invert = invert;         // ★追加

            _lastOutput = _generatorEffect.Output;

            // 画像保存処理
            if (_item.SaveTrigger)
            {
                _item.SaveTrigger = false;
                var captureOutput = _lastOutput;
                int captureW = (int)w;
                int captureH = (int)h;

                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new SaveFileDialog
                        {
                            Filter = "PNG Image|*.png",
                            FileName = $"NormalMap_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                        };

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            try
                            {
                                SaveImageToPng(captureOutput, captureW, captureH, dialog.FileName);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("保存に失敗しました: " + ex.Message);
                            }
                        }
                    });
                }
            }

            return desc.DrawDescription;
        }

        private void SaveImageToPng(ID2D1Image image, int width, int height, string filePath)
        {
            var dc = _devices.DeviceContext;
            var renderProps = new BitmapProperties1(new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96, BitmapOptions.Target);

            using (var renderBitmap = dc.CreateBitmap(new Vortice.Mathematics.SizeI(width, height), IntPtr.Zero, 0, renderProps))
            {
                var oldTarget = dc.Target;
                dc.Target = renderBitmap;
                dc.BeginDraw();
                dc.Clear(new Vortice.Mathematics.Color4(0, 0, 0, 1.0f));
                var bounds = dc.GetImageLocalBounds(image);
                dc.DrawImage(image, new Vector2(-bounds.Left, -bounds.Top), (InterpolationMode)CompositeMode.SourceOver);
                dc.EndDraw();
                dc.Target = oldTarget;

                var cpuProps = new BitmapProperties1(new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96, BitmapOptions.CpuRead | BitmapOptions.CannotDraw);

                using (var cpuBitmap = dc.CreateBitmap(new Vortice.Mathematics.SizeI(width, height), IntPtr.Zero, 0, cpuProps))
                {
                    cpuBitmap.CopyFromBitmap(renderBitmap);
                    var mapBox = cpuBitmap.Map(MapOptions.Read);
                    try
                    {
                        using (var wicFactory = new IWICImagingFactory())
                        using (var wicBitmap = wicFactory.CreateBitmapFromMemory(width, height, Vortice.WIC.PixelFormat.Format32bppPBGRA, mapBox.Pitch, mapBox.Pitch * height, mapBox.Bits))
                        using (var stream = File.OpenWrite(filePath))
                        using (var encoder = wicFactory.CreateEncoder(ContainerFormat.Png))
                        {
                            encoder.Initialize(stream);
                            using (var frame = encoder.CreateNewFrame(out var propsBag))
                            {
                                frame.Initialize(propsBag);
                                frame.SetSize(width, height);
                                var pixelFormat = Vortice.WIC.PixelFormat.Format32bppPBGRA;
                                frame.SetPixelFormat(ref pixelFormat);
                                frame.WriteSource(wicBitmap);
                                frame.Commit();
                            }
                            encoder.Commit();
                        }
                    }
                    finally
                    {
                        cpuBitmap.Unmap();
                    }
                }
            }
        }

        public ID2D1Image Output => _lastOutput ?? _input!;
        public void SetInput(ID2D1Image? input) { _input = input; }
        public void ClearInput() { _input = null; }

        public void Dispose()
        {
            _lastOutput?.Dispose();
            _generatorEffect?.SetInput(0, null, true); _generatorEffect?.Dispose();
            _blurEffect?.SetInput(0, null, true); _blurEffect?.Dispose();
        }
    }
}