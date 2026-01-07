using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using static NormalMap_Iyahon.LightSourceEffect;

namespace NormalMap_Iyahon
{
    internal class LightSourceEffectProcessor : IVideoEffectProcessor, IDisposable, ILightSourceProvider
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly LightSourceEffect _item;
        private ID2D1Image? _input;

        private NormalMapCustomEffect? _customEffect;
        private AffineTransform2D? _transformEffect;
        private ID2D1Bitmap? _flatNormalBitmap;

        private int _currentId = -1;
        private bool _isRegistered = false;

        private LightData _currentData;

        public LightSourceEffectProcessor(IGraphicsDevicesAndContext devices, LightSourceEffect item)
        {
            _devices = devices;
            _item = item;

            // 初期化
            _currentData = new LightData
            {
                Position = new Vector3(0, 0, 200),
                Intensity = 1.0f,
                Color = new Vector3(1, 1, 1),
                Type = LightType.Point
            };
        }

        // ★修正: 引数なし（インターフェースに合わせる）
        public LightData GetLightData()
        {
            return _currentData;
        }

        public DrawDescription Update(EffectDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var len = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            // 1. 先に計算してキャッシュに入れる
            float x = (float)_item.X.GetValue(frame, len, fps);
            float y = (float)_item.Y.GetValue(frame, len, fps);
            float z = (float)_item.Z.GetValue(frame, len, fps);
            float intensity = (float)_item.Intensity.GetValue(frame, len, fps);
            var color = new Vector3(
                _item.LightColor.R / 255f,
                _item.LightColor.G / 255f,
                _item.LightColor.B / 255f
            );
            LightType type = _item.Type == LightSourceType.Directional ? LightType.Directional : LightType.Point;

            _currentData = new LightData
            {
                Position = new Vector3(x, y, z),
                Intensity = intensity,
                Color = color,
                Type = type
            };

            // 2. 登録（データ付き）
            int id = (int)_item.LightId.GetValue(frame, len, fps);

            if (!_isRegistered || _currentId != id)
            {
                if (_isRegistered)
                {
                    LightSourceManager.Unregister(_currentId, this);
                }
                // ★修正: 第3引数に _currentData を渡す
                LightSourceManager.Register(id, this, _currentData);
                _currentId = id;
                _isRegistered = true;
            }
            else
            {
                // 登録済みならバックアップだけ更新
                LightSourceManager.UpdateData(id, _currentData);
            }

            // --- 簡易描画 ---
            if (!_item.ApplyToItem || _input == null)
            {
                return desc.DrawDescription;
            }

            var dc = _devices.DeviceContext;

            if (_customEffect == null)
            {
                _customEffect = new NormalMapCustomEffect(_devices);
                _transformEffect = new AffineTransform2D(dc);
            }

            ID2D1Bitmap normalMapBitmap = GetFlatNormalBitmap();

            var inputBounds = dc.GetImageLocalBounds(_input);
            float inputW = inputBounds.Right - inputBounds.Left;
            float inputH = inputBounds.Bottom - inputBounds.Top;

            if (inputW <= 0 || inputH <= 0) return desc.DrawDescription;

            var mapSize = normalMapBitmap.Size;
            float scaleX = inputW / mapSize.Width;
            float scaleY = inputH / mapSize.Height;

            _transformEffect!.SetInput(0, normalMapBitmap, true);
            _transformEffect.InterPolationMode = (AffineTransform2DInterpolationMode)InterpolationMode.NearestNeighbor;

            Matrix3x2 transformMatrix = Matrix3x2.CreateScale(scaleX, scaleY) *
                                        Matrix3x2.CreateTranslation(inputBounds.Left, inputBounds.Top);
            _transformEffect.TransformMatrix = transformMatrix;

            float ambient = (float)_item.Ambient.GetValue(frame, len, fps);

            _customEffect!.SetInput(0, _input, true);
            _customEffect!.SetInput(1, _transformEffect.Output, true);

            // キャッシュしたデータを使う
            _customEffect.LightPos = _currentData.Position;
            _customEffect.Intensity = _currentData.Intensity;
            _customEffect.LightColor = _currentData.Color;
            _customEffect.Ambient = ambient;
            _customEffect.Depth = 1.0f;
            _customEffect.LightType = (float)_currentData.Type;

            return desc.DrawDescription;
        }

        public ID2D1Image? Output => (_item.ApplyToItem && _customEffect != null) ? _customEffect.Output : _input;

        public void SetInput(ID2D1Image? input) { _input = input; }
        public void ClearInput() { _input = null; }

        private ID2D1Bitmap GetFlatNormalBitmap()
        {
            if (_flatNormalBitmap != null) return _flatNormalBitmap;

            int size = 16;
            var pixelColor = new byte[] { 255, 128, 128, 255 };
            byte[] pixels = new byte[size * size * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 0] = pixelColor[0];
                pixels[i + 1] = pixelColor[1];
                pixels[i + 2] = pixelColor[2];
                pixels[i + 3] = pixelColor[3];
            }

            var sizeI = new Vortice.Mathematics.SizeI(size, size);
            var pixelFormat = new Vortice.DCommon.PixelFormat(
                Vortice.DXGI.Format.B8G8R8A8_UNorm,
                Vortice.DCommon.AlphaMode.Premultiplied
            );
            var props = new BitmapProperties(pixelFormat);

            unsafe
            {
                fixed (byte* p = pixels)
                {
                    _flatNormalBitmap = _devices.DeviceContext.CreateBitmap(sizeI, (IntPtr)p, size * 4, props);
                }
            }
            return _flatNormalBitmap;
        }

        public void Dispose()
        {
            if (_isRegistered)
            {
                LightSourceManager.Unregister(_currentId, this);
            }

            _customEffect?.Dispose();
            _transformEffect?.Dispose();
            _flatNormalBitmap?.Dispose();
        }
    }
}