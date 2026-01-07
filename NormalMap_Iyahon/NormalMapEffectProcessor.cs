using System;
using System.IO;
using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using static NormalMap_Iyahon.NormalMapEffect;

namespace NormalMap_Iyahon
{
    internal class NormalMapEffectProcessor : IVideoEffectProcessor, IDisposable
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly NormalMapEffect _item;

        private ID2D1Image? _input;

        private AffineTransform2D? _transformEffect;
        private GaussianBlur? _blurEffect;

        private NormalMapCustomEffect? _customEffect;
        private PointDiffuse? _diffuseEffect;
        private DistantDiffuse? _distantDiffuseEffect;
        private ArithmeticComposite? _compositeEffect;
        private LuminanceToAlpha? _lumToAlphaEffect;

        private Composite? _maskEffect;

        // ★修正: ファイル用Bitmapだけにする
        private ID2D1Bitmap? _fileBitmap;

        private ID2D1Bitmap? _flatNormalBitmap;
        private ID2D1Bitmap? _flatHeightBitmap;
        private string _loadedPath = string.Empty;
        private ID2D1Image? _lastOutput;

        public NormalMapEffectProcessor(IGraphicsDevicesAndContext devices, NormalMapEffect item)
        {
            _devices = devices;
            _item = item;

            var dc = _devices.DeviceContext;

            _transformEffect = new AffineTransform2D(dc);
            _blurEffect = new GaussianBlur(dc);

            _customEffect = new NormalMapCustomEffect(devices);
            _diffuseEffect = new PointDiffuse(dc);
            _distantDiffuseEffect = new DistantDiffuse(dc);
            _compositeEffect = new ArithmeticComposite(dc);
            _lumToAlphaEffect = new LuminanceToAlpha(dc);
            _maskEffect = new Composite(dc);
        }

        public DrawDescription Update(EffectDescription desc)
        {
            if (_lastOutput != null) { _lastOutput.Dispose(); _lastOutput = null; }
            if (_input == null) return desc.DrawDescription;

            var frame = desc.ItemPosition.Frame;
            var len = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var dc = _devices.DeviceContext;

            // 1. 光源計算
            int id = (int)_item.LightId.GetValue(frame, len, fps);
            var lightData = LightSourceManager.GetData(id);
            Vector3 finalLightPos = lightData.Position;
            var lightColor = lightData.Color;
            float intensity = lightData.Intensity;
            LightType lightType = lightData.Type;

            if (_item.LightcalcMode == LightCalcType.Absolute)
            {
                var itemPos = desc.DrawDescription.Draw;
                var itemRot = desc.DrawDescription.Rotation;
                var itemScale = desc.DrawDescription.Zoom;

                float radX = itemRot.X * (float)Math.PI / 180.0f;
                float radY = itemRot.Y * (float)Math.PI / 180.0f;
                float radZ = itemRot.Z * (float)Math.PI / 180.0f;

                Matrix4x4 rotationMatrix = Matrix4x4.CreateRotationZ(-radZ) *
                                           Matrix4x4.CreateRotationY(-radY) *
                                           Matrix4x4.CreateRotationX(-radX);

                if (lightType == LightType.Point)
                {
                    Vector3 relativePos = finalLightPos - itemPos;
                    finalLightPos = Vector3.Transform(relativePos, rotationMatrix);
                }
                else
                {
                    finalLightPos = Vector3.Transform(finalLightPos, rotationMatrix);
                }

                if (itemScale.X < 0) finalLightPos.X *= -1;
                if (itemScale.Y < 0) finalLightPos.Y *= -1;
            }

            // 2. リソース準備
            UpdateNormalMapResource();

            // ★修正: 連携分岐削除
            ID2D1Image mapImage;
            if (_fileBitmap != null)
                mapImage = _fileBitmap;
            else
                mapImage = (_item.Type == MapType.Normal) ? GetFlatNormalBitmap() : GetFlatHeightBitmap();

            // 3. 配置・変形
            var inputBounds = dc.GetImageLocalBounds(_input);
            float inputW = inputBounds.Right - inputBounds.Left;
            float inputH = inputBounds.Bottom - inputBounds.Top;

            if (inputW <= 0 || inputH <= 0) return desc.DrawDescription;

            var mapBounds = dc.GetImageLocalBounds(mapImage);
            float mapW = mapBounds.Right - mapBounds.Left;
            float mapH = mapBounds.Bottom - mapBounds.Top;

            Matrix3x2 transformMatrix;

            if (_item.AutoFit)
            {
                float scaleX = inputW / mapW;
                float scaleY = inputH / mapH;
                transformMatrix = Matrix3x2.CreateScale(scaleX, scaleY) *
                                  Matrix3x2.CreateTranslation(inputBounds.Left, inputBounds.Top);
                _transformEffect!.SetInput(0, mapImage, true);
            }
            else
            {
                float mapX = (float)_item.MapX.GetValue(frame, len, fps);
                float mapY = (float)_item.MapY.GetValue(frame, len, fps);
                float mapScale = (float)_item.MapScale.GetValue(frame, len, fps) / 100.0f;
                float mapRot = (float)_item.MapRotation.GetValue(frame, len, fps);
                float mapBlur = (float)_item.MapBlur.GetValue(frame, len, fps);

                float offsetX = -mapW / 2.0f;
                float offsetY = -mapH / 2.0f;
                float itemCenterX = inputBounds.Left + inputW / 2.0f;
                float itemCenterY = inputBounds.Top + inputH / 2.0f;
                float rad = mapRot * (float)Math.PI / 180.0f;

                transformMatrix = Matrix3x2.CreateTranslation(offsetX, offsetY) *
                                  Matrix3x2.CreateScale(mapScale, mapScale) *
                                  Matrix3x2.CreateRotation(rad) *
                                  Matrix3x2.CreateTranslation(itemCenterX + mapX, itemCenterY + mapY);

                if (mapBlur > 0.01f)
                {
                    _blurEffect!.SetInput(0, mapImage, true);
                    _blurEffect.StandardDeviation = mapBlur;
                    _blurEffect.Optimization = GaussianBlurOptimization.Speed;
                    _blurEffect.BorderMode = BorderMode.Hard;
                    _transformEffect!.SetInput(0, _blurEffect.Output, true);
                }
                else
                {
                    _transformEffect!.SetInput(0, mapImage, true);
                }
            }

            _transformEffect.InterPolationMode = (AffineTransform2DInterpolationMode)InterpolationMode.Linear;
            _transformEffect.TransformMatrix = transformMatrix;

            float scale = (float)_item.SurfaceScale.GetValue(frame, len, fps);
            float ambient = (float)_item.Ambient.GetValue(frame, len, fps);

            if (_item.Type == MapType.Normal)
            {
                scale /= 20.0f;
            }

            // 4. エフェクト適用
            using (var transformedMap = _transformEffect.Output)
            {
                if (_item.Type == MapType.Normal)
                {
                    _customEffect!.SetInput(0, _input, true);
                    _customEffect!.SetInput(1, transformedMap, true);

                    _customEffect.LightPos = finalLightPos;
                    _customEffect.Intensity = intensity;
                    _customEffect.LightColor = lightColor;
                    _customEffect.Ambient = ambient;
                    _customEffect.Depth = scale;
                    _customEffect.LightType = (lightType == LightType.Directional) ? 1.0f : 0.0f;

                    _lastOutput = _customEffect.Output;
                }
                else
                {
                    _lumToAlphaEffect!.SetInput(0, transformedMap, true);

                    using (var heightMap = _lumToAlphaEffect.Output)
                    {
                        ID2D1Image rawLightMap;

                        if (lightType == LightType.Directional)
                        {
                            float x = finalLightPos.X; float y = finalLightPos.Y; float z = finalLightPos.Z;
                            double azimuthRad = Math.Atan2(y, x);
                            float azimuthDeg = (float)(azimuthRad * 180.0 / Math.PI);
                            float xyLen = (float)Math.Sqrt(x * x + y * y);
                            double elevationRad = Math.Atan2(z, xyLen);
                            float elevationDeg = (float)(elevationRad * 180.0 / Math.PI);

                            _distantDiffuseEffect!.SetInput(0, heightMap, true);
                            _distantDiffuseEffect.Azimuth = azimuthDeg;
                            _distantDiffuseEffect.Elevation = elevationDeg;
                            _distantDiffuseEffect.Color = lightColor;
                            _distantDiffuseEffect.SurfaceScale = scale;
                            _distantDiffuseEffect.KernelUnitLength = new Vector2(1, 1);
                            rawLightMap = _distantDiffuseEffect.Output;
                        }
                        else
                        {
                            _diffuseEffect!.SetInput(0, heightMap, true);
                            _diffuseEffect.LightPosition = finalLightPos;
                            _diffuseEffect.Color = lightColor;
                            _diffuseEffect.SurfaceScale = scale;
                            _diffuseEffect.KernelUnitLength = new Vector2(1, 1);
                            rawLightMap = _diffuseEffect.Output;
                        }

                        using (rawLightMap)
                        {
                            _maskEffect!.SetInput(0, rawLightMap, true);
                            _maskEffect!.SetInput(1, _input, true);
                            _maskEffect!.Mode = CompositeMode.DestinationIn;

                            using (var clippedLightMap = _maskEffect.Output)
                            {
                                _compositeEffect!.SetInput(0, _input, true);
                                _compositeEffect!.SetInput(1, clippedLightMap, true);
                                _compositeEffect.Coefficients = new Vector4(intensity, 0, ambient, 0);

                                _lastOutput = _compositeEffect.Output;
                            }
                        }
                    }
                }
            }

            return desc.DrawDescription;
        }

        public ID2D1Image Output => _lastOutput ?? _input!;

        public void SetInput(ID2D1Image? input) { _input = input; }
        public void ClearInput() { _input = null; }

        private void UpdateNormalMapResource()
        {
            // 単純なファイルロードのみ
            string path = _item.NormalMapPath;
            if (path == _loadedPath && _fileBitmap != null) return;

            if (!string.IsNullOrEmpty(_loadedPath))
            {
                TextureManager.ReleaseTexture(_loadedPath);
                _fileBitmap = null;
            }

            _loadedPath = path;

            if (!string.IsNullOrEmpty(path))
            {
                _fileBitmap = TextureManager.LoadTexture(_devices.DeviceContext, path);
            }
        }

        private ID2D1Bitmap GetFlatNormalBitmap()
        {
            if (_flatNormalBitmap != null) return _flatNormalBitmap;
            int size = 16; var pixelColor = new byte[] { 255, 128, 128, 255 };
            return CreateDummyBitmap(size, pixelColor, ref _flatNormalBitmap);
        }

        private ID2D1Bitmap GetFlatHeightBitmap()
        {
            if (_flatHeightBitmap != null) return _flatHeightBitmap;
            int size = 16; var pixelColor = new byte[] { 0, 0, 0, 255 };
            return CreateDummyBitmap(size, pixelColor, ref _flatHeightBitmap);
        }

        private ID2D1Bitmap CreateDummyBitmap(int size, byte[] color, ref ID2D1Bitmap? target)
        {
            byte[] pixels = new byte[size * size * 4];
            for (int i = 0; i < pixels.Length; i += 4) { pixels[i + 0] = color[0]; pixels[i + 1] = color[1]; pixels[i + 2] = color[2]; pixels[i + 3] = color[3]; }
            var sizeI = new Vortice.Mathematics.SizeI(size, size);
            var pixelFormat = new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var props = new BitmapProperties(pixelFormat);
            unsafe { fixed (byte* p = pixels) { target = _devices.DeviceContext.CreateBitmap(sizeI, (IntPtr)p, size * 4, props); } }
            return target!;
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(_loadedPath))
            {
                TextureManager.ReleaseTexture(_loadedPath);
                _fileBitmap = null;
                _loadedPath = string.Empty;
            }

            _lastOutput?.Dispose();
            _lastOutput = null;

            _transformEffect?.SetInput(0, null, true);
            _transformEffect?.Dispose();

            _blurEffect?.SetInput(0, null, true);
            _blurEffect?.Dispose();

            _customEffect?.SetInput(0, null, true);
            _customEffect?.SetInput(1, null, true);
            _customEffect?.Dispose();

            _lumToAlphaEffect?.SetInput(0, null, true);
            _lumToAlphaEffect?.Dispose();

            _diffuseEffect?.SetInput(0, null, true);
            _diffuseEffect?.Dispose();

            _distantDiffuseEffect?.SetInput(0, null, true);
            _distantDiffuseEffect?.Dispose();

            _compositeEffect?.SetInput(0, null, true);
            _compositeEffect?.SetInput(1, null, true);
            _compositeEffect?.Dispose();

            _maskEffect?.SetInput(0, null, true);
            _maskEffect?.SetInput(1, null, true);
            _maskEffect?.Dispose();

            _flatNormalBitmap?.Dispose();
            _flatHeightBitmap?.Dispose();
        }
    }

}