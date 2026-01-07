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
    internal class NormalMapCustomEffect : D2D1CustomShaderEffectBase
    {
        public Vector3 LightPos { set => SetValue((int)Props.LightPos, value); }
        public float Intensity { set => SetValue((int)Props.Intensity, value); }
        public Vector3 LightColor { set => SetValue((int)Props.LightColor, value); }
        public float Ambient { set => SetValue((int)Props.Ambient, value); }
        public float Depth { set => SetValue((int)Props.Depth, value); }
        // ★追加
        public float LightType { set => SetValue((int)Props.LightType, value); }
    


        public NormalMapCustomEffect(IGraphicsDevicesAndContext devices) : base(Create<EffectImpl>(devices)) { }

        [StructLayout(LayoutKind.Sequential)]
        struct ConstantBuffer
        {
            public Vector3 LightPos;   // 12
            public float Intensity;    // 4 -> 16
            public Vector3 LightColor; // 12
            public float Ambient;      // 4 -> 32
            public float Depth;        // 4
            public float LightType;    // 4 ★追加 (0=Point, 1=Directional)
            public Vector2 Padding;    // 8 -> 48 (16byte境界)
        }

        private enum Props
        {
            LightPos, Intensity, LightColor, Ambient, Depth, LightType
        }

        [CustomEffect(2)]
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
                if (inputRects.Length > 0) inputRects[0] = outputRect;
                if (inputRects.Length > 1) inputRects[1] = outputRect;
            }

            private static byte[] LoadShader()
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "NormalMap_Iyahon.Shaders.NormalMapShader.cso";
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
                    LightPos = new Vector3(0, 0, 100),
                    Intensity = 1.0f,
                    LightColor = new Vector3(1, 1, 1),
                    Ambient = 0.2f,
                    Depth = 1.0f,
                    LightType = 0.0f
                };
            }

            [CustomEffectProperty(PropertyType.Vector3, (int)Props.LightPos)]
            public Vector3 LightPos { get => constants.LightPos; set { constants.LightPos = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Intensity)]
            public float Intensity { get => constants.Intensity; set { constants.Intensity = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector3, (int)Props.LightColor)]
            public Vector3 LightColor { get => constants.LightColor; set { constants.LightColor = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Ambient)]
            public float Ambient { get => constants.Ambient; set { constants.Ambient = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Depth)]
            public float Depth { get => constants.Depth; set { constants.Depth = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.LightType)]
            public float LightType { get => constants.LightType; set { constants.LightType = value; UpdateConstants(); } }
        }
    }

}