using NormalMap_Iyahon;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace NormalMap_Iyahon
{
    [VideoEffect("Testノーマルマップ生成", ["描画", "加工"], ["normal", "generate", "作成"])]
    public class NormalMapGeneratorEffect : VideoEffectBase
    {
        public override string Label => "ノーマルマップ生成";

        [Display(GroupName = "設定", Name = "アルゴリズム", Description = "Sobel: 模様や文字のディテールを拾います。\nSDF: 距離計算で綺麗なベベルを作ります(重い)。\nBlend: 両方をミックスします。")]
        [EnumComboBox]
        public GenAlgorithm Algorithm { get => algorithm; set => Set(ref algorithm, value); }
        private GenAlgorithm algorithm = GenAlgorithm.Blend;

        [Display(GroupName = "設定", Name = "出力モード", Description = "ノーマルマップ(法線)か、ハイトマップ(高さ)かを選択します。")]
        [EnumComboBox]
        public OutputType OutputMode { get => outputMode; set => Set(ref outputMode, value); }
        private OutputType outputMode = OutputType.NormalMap;

        [Display(GroupName = "設定", Name = "形式", Description = "ノーマルマップのY軸(緑)の向き。\nYMM4やUnityはDirectX、Blender等はOpenGLを選びます。")]
        [EnumComboBox]
        public NormalFormat Format { get => format; set => Set(ref format, value); }
        private NormalFormat format = NormalFormat.DirectX;

        [Display(GroupName = "設定", Name = "凹凸反転", Description = "ONにすると、出っ張りと凹みを逆にします。\n(ハイトマップの場合は白黒が反転します)")]
        [ToggleSlider]
        public bool Invert { get => invert; set => Set(ref invert, value); }
        private bool invert = false;

        [Display(GroupName = "共通", Name = "ぼかし", Description = "生成前に画像をぼかして、ノイズを減らします。")]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Blur { get; } = new Animation(0f, 0, 50);

        // --- SDF用 ---
        [Display(GroupName = "SDF (立体)", Name = "立体強度", Description = "SDFモード時のベベルの角度の急さ。")]
        [AnimationSlider("F1", "", 0, 20)]
        public Animation Strength { get; } = new Animation(5.0f, 0, 100);

        [Display(GroupName = "SDF (立体)", Name = "探索半径", Description = "ベベルの幅(px)。実質的なSDFの適用範囲です。\nこの値を超えた内側は平らになります。")]
        [AnimationSlider("F0", "px", 0, 50)]
        public Animation Radius { get; } = new Animation(10.0f, 0, 100);

        [Display(GroupName = "SDF (立体)", Name = "カーブ", Description = "ベベルの丸み。\nプラス: ふんわり(ドーム状)\n0: 直線\nマイナス: 鋭く反り立つ")]
        [AnimationSlider("F2", "", -1, 1)]
        public Animation Curve { get; } = new Animation(0.0f, -10.0f, 10.0f);


        // --- Sobel/Blend用 ---
        [Display(GroupName = "Sobel (詳細)", Name = "詳細強度", Description = "Sobel/Blendモード時の、表面の凸凹の強さ。")]
        [AnimationSlider("F1", "倍", 0, 10)]
        public Animation DetailStrength { get; } = new Animation(2.0f, 0, 50);

        [Display(GroupName = "Sobel (詳細)", Name = "詳細半径", Description = "ディテール検出の太さ(px)。\n値を上げると太い線を検出できるようになります。")]
        [AnimationSlider("F1", "px", 1, 10)]
        public Animation DetailRadius { get; } = new Animation(1.0f, 1, 50);

        // --- 閾値設定 ---
        [Display(GroupName = "閾値", Name = "不透明度", Description = "これより薄い色は「背景(低い場所)」として扱います。")]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation ThresholdAlpha { get; } = new Animation(10.0f, 0, 100);

        [Display(GroupName = "閾値", Name = "輝度", Description = "これより暗い色は「背景(低い場所)」として扱います。")]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation ThresholdBright { get; } = new Animation(0.0f, 0, 100);

        [Display(GroupName = "閾値", Name = "差分(エッジ)", Description = "隣との明るさの差がこれ以上あれば「段差」とみなします。\n★不透明な画像から文字だけ浮き出させる場合、ここを上げてください(10-30%)。")]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation ThresholdDiff { get; } = new Animation(0.0f, 0, 100);


        [Display(GroupName = "出力", Name = "画像を保存", Description = "クリックすると現在の結果をPNG保存します。")]
        [ToggleSlider]
        public bool SaveTrigger { get => saveTrigger; set => Set(ref saveTrigger, value); }
        private bool saveTrigger = false;

        public enum GenAlgorithm
        {
            [Display(Name = "Sobel (ディテール重視)")]
            Sobel,
            [Display(Name = "SDF (形状重視・重い)")]
            SDF,
            [Display(Name = "Blend (ハイブリッド)")]
            Blend
        }

        public enum OutputType
        {
            [Display(Name = "ノーマルマップ")]
            NormalMap,
            [Display(Name = "ハイトマップ")]
            HeightMap
        }

        public enum NormalFormat
        {
            [Display(Name = "DirectX (Y-)")]
            DirectX,
            [Display(Name = "OpenGL (Y+)")]
            OpenGL
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new NormalMapGeneratorEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            return [Blur, Strength, Radius, Curve, DetailStrength, DetailRadius, ThresholdAlpha, ThresholdBright, ThresholdDiff];
        }
    }
}