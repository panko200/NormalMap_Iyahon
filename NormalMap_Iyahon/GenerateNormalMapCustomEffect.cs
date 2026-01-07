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
        public GenerateNormalMapCustomEffect(IGraphicsDevicesAndContext devices) : base(Create<EffectImpl>(devices)) { }

        [StructLayout(LayoutKind.Sequential)]
        struct ConstantBuffer
        {
            public float Strength;
            public float Radius;
            public Vector2 Size;
        }

        private enum Props { Strength, Radius, Size }

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

            // ★修正: 入力画像から出力画像のサイズを計算
            // ここは「入力と同じサイズを出力する」という定義なので変更なしでOK
            // (余裕があればここも厳密にはOutputRect = InputRects[0]からパディングを引いたもの...とすべきですが、
            //  今回は「同じサイズ」で通ります)
            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                if (inputRects.Length > 0)
                {
                    outputRect = inputRects[0];
                }
                else
                {
                    outputRect = new RawRect();
                }
                outputOpaqueSubRect = new RawRect();
            }

            // ★修正: 出力画像を作るために「必要な入力画像の範囲」を計算
            // ここで「半径(Radius)分だけ広くくれ！」と主張することで、タイリングの境目を消すことができます。
            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length > 0)
                {
                    // シェーダーで参照する範囲（Radius）に合わせてマージンを計算
                    // Radiusはピクセル単位の広さ係数なので、少し余裕を持って切り上げる
                    int margin = (int)Math.Ceiling(constants.Radius + 1.0f);

                    inputRects[0] = new RawRect(
                        outputRect.Left - margin,
                        outputRect.Top - margin,
                        outputRect.Right + margin,
                        outputRect.Bottom + margin
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
                constants = new ConstantBuffer { Strength = 5.0f, Radius = 1.0f, Size = new Vector2(0.001f, 0.001f) };
            }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Strength)]
            public float Strength { get => constants.Strength; set { constants.Strength = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Props.Radius)]
            public float Radius { get => constants.Radius; set { constants.Radius = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector2, (int)Props.Size)]
            public Vector2 Size { get => constants.Size; set { constants.Size = value; UpdateConstants(); } }
        }
    }

}