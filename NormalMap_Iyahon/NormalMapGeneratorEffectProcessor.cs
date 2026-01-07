using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
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
        private Grayscale? _grayscaleEffect;

        private ID2D1Image? _lastOutput;

        public NormalMapGeneratorEffectProcessor(IGraphicsDevicesAndContext devices, NormalMapGeneratorEffect item)
        {
            _devices = devices;
            _item = item;

            var dc = _devices.DeviceContext;
            _generatorEffect = new GenerateNormalMapCustomEffect(devices);
            _blurEffect = new GaussianBlur(dc);
            _grayscaleEffect = new Grayscale(dc);
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
            if (_item.Mode == GeneratorMode.Normal)
            {
                _generatorEffect!.SetInput(0, sourceImage, true);
                _generatorEffect.Strength = strength;
                _generatorEffect.Radius = radius;
                _generatorEffect.Size = new Vector2(1.0f / w, 1.0f / h);
                _lastOutput = _generatorEffect.Output;
            }
            else
            {
                _grayscaleEffect!.SetInput(0, sourceImage, true);
                _lastOutput = _grayscaleEffect.Output;
            }

            return desc.DrawDescription;
        }

        public ID2D1Image Output => _lastOutput ?? _input!;

        public void SetInput(ID2D1Image? input) { _input = input; }
        public void ClearInput() { _input = null; }

        public void Dispose()
        {
            _lastOutput?.Dispose();
            _generatorEffect?.SetInput(0, null, true); _generatorEffect?.Dispose();
            _blurEffect?.SetInput(0, null, true); _blurEffect?.Dispose();
            _grayscaleEffect?.SetInput(0, null, true); _grayscaleEffect?.Dispose();
        }
    }
}