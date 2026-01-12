using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace NormalMap_Iyahon
{
    internal class GenerateNormalMapCustomEffect : D2D1CustomShaderEffectBase
    {
        public float Strength { set => SetValue((int)Props.Strength, value); }
        public float Radius { set => SetValue((int)Props.Radius, value); }
        public Vector2 Size { set => SetValue((int)Props.Size, value); }

        public int Mode { set => SetValue((int)Props.Mode, value); }
        public float DetailStrength { set => SetValue((int)Props.DetailStrength, value); }
        public float DetailRadius { set => SetValue((int)Props.DetailRadius, value); }
        public float ThresholdAlpha { set => SetValue((int)Props.ThresholdAlpha, value); }
        public float ThresholdBright { set => SetValue((int)Props.ThresholdBright, value); }
        public float ThresholdDiff { set => SetValue((int)Props.ThresholdDiff, value); }

        public float Curve { set => SetValue((int)Props.Curve, value); }
        public int Format { set => SetValue((int)Props.Format, value); }

        // ★追加
        public int OutputMode { set => SetValue((int)Props.OutputMode, value); }
        public float Invert { set => SetValue((int)Props.Invert, value); }

        public GenerateNormalMapCustomEffect(IGraphicsDevicesAndContext devices) : base(Create<EffectImpl>(devices)) { }

        [StructLayout(LayoutKind.Sequential)]
        struct ConstantBuffer
        {
            // Slot 1 (16 bytes)
            public float Strength;
            public float Radius;
            public Vector2 Size;

            // Slot 2 (16 bytes)
            public int Mode;
            public float DetailStrength;
            public float ThresholdAlpha;
            public float ThresholdBright;

            // Slot 3 (16 bytes)
            public float DetailRadius;
            public float ThresholdDiff;
            public float Curve;
            public int Format;

            // Slot 4 (16 bytes) ★追加
            public int OutputMode;
            public float Invert;
            public Vector2 Padding;
        }

        private enum Props { Strength, Radius, Size, Mode, DetailStrength, DetailRadius, ThresholdAlpha, ThresholdBright, ThresholdDiff, Curve, Format, OutputMode, Invert }

        [CustomEffect(1)]
        private class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer constants;

            protected override void UpdateConstants()
            {
                if (drawInformation != null)
                {
                    drawInformation.SetPixelShaderConstantBuffer(constants);
                }
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length > 0) outputRect = inputRects[0];
                else outputRect = new RawRect();
                outputOpaqueSubRect = new RawRect();
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length > 0)
                {
                    int safeMargin = 104;
                    inputRects[0] = new RawRect(
                        outputRect.Left - safeMargin,
                        outputRect.Top - safeMargin,
                        outputRect.Right + safeMargin,
                        outputRect.Bottom + safeMargin
                    );
                }
            }

            private static byte[] LoadShader()
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "NormalMap_Iyahon.Shaders.GenerateNormalMapShader.cso";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) return Array.Empty<byte>();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }

            public EffectImpl() : base(LoadShader())
            {
                constants = new ConstantBuffer
                {
                    Strength = 5.0f,
                    Radius = 10.0f,
                    Size = new Vector2(0.001f, 0.001f),
                    Mode = 2,
                    DetailStrength = 2.0f,
                    DetailRadius = 1.0f,
                    ThresholdAlpha = 0.1f,
                    ThresholdBright = 0.0f,
                    ThresholdDiff = 0.0f,
                    Curve = 0.0f,
                    Format = 0,
                    OutputMode = 0,
                    Invert = 0.0f
                };
            }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Strength)]
            public float Strength { get => constants.Strength; set { constants.Strength = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Radius)]
            public float Radius { get => constants.Radius; set { constants.Radius = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector2, (int)Props.Size)]
            public Vector2 Size { get => constants.Size; set { constants.Size = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Props.Mode)]
            public int Mode { get => constants.Mode; set { constants.Mode = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.DetailStrength)]
            public float DetailStrength { get => constants.DetailStrength; set { constants.DetailStrength = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.DetailRadius)]
            public float DetailRadius { get => constants.DetailRadius; set { constants.DetailRadius = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.ThresholdAlpha)]
            public float ThresholdAlpha { get => constants.ThresholdAlpha; set { constants.ThresholdAlpha = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.ThresholdBright)]
            public float ThresholdBright { get => constants.ThresholdBright; set { constants.ThresholdBright = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.ThresholdDiff)]
            public float ThresholdDiff { get => constants.ThresholdDiff; set { constants.ThresholdDiff = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Curve)]
            public float Curve { get => constants.Curve; set { constants.Curve = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Props.Format)]
            public int Format { get => constants.Format; set { constants.Format = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Props.OutputMode)]
            public int OutputMode { get => constants.OutputMode; set { constants.OutputMode = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Invert)]
            public float Invert { get => constants.Invert; set { constants.Invert = value; UpdateConstants(); } }
        }
    }
}