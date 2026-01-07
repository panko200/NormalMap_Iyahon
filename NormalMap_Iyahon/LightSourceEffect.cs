using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace NormalMap_Iyahon
{

    [VideoEffect("NMI光源設定", ["描画"], ["light", "光源", "lighting", "NMI"])]
    public class LightSourceEffect : VideoEffectBase
    {
        public override string Label => "NMI光源設定";

        [Display(GroupName = "共通", Name = "光源ID", Description = "ノーマルマップ側のIDと一致させてください。")]
        [AnimationSlider("F0", "番", 0, 9)]
        public Animation LightId { get; } = new Animation(0, 0, 99);

        // ★追加: 光源タイプ選択
        [Display(GroupName = "共通", Name = "種類", Description = "点光源：位置から全方位に光ります。\n平行光源：無限遠から指定した方向(XYZ)に向かって光ります。")]
        [EnumComboBox]
        public LightSourceType Type { get => type; set => Set(ref type, value); }
        private LightSourceType type = LightSourceType.Point;

        [Display(GroupName = "座標", Name = "X", Description = "点光源：X座標\n平行光源：光が来るX方向")]
        [AnimationSlider("F1", "", -1000, 1000)]
        public Animation X { get; } = new Animation(0, -5000, 5000);

        [Display(GroupName = "座標", Name = "Y", Description = "点光源：Y座標\n平行光源：光が来るY方向")]
        [AnimationSlider("F1", "", -1000, 1000)]
        public Animation Y { get; } = new Animation(0, -5000, 5000);

        [Display(GroupName = "座標", Name = "Z", Description = "点光源：Z座標（高さ）\n平行光源：光が来るZ方向（高さ）")]
        [AnimationSlider("F1", "", -1000, 1000)]
        public Animation Z { get; } = new Animation(200, -5000, 5000);

        [Display(GroupName = "光", Name = "強度", Description = "光の強さ")]
        [AnimationSlider("F2", "倍", 0, 5)]
        public Animation Intensity { get; } = new Animation(1.0f, 0, 10);

        [Display(GroupName = "光", Name = "色", Description = "光の色")]
        [ColorPicker]
        public Color LightColor { get => lightColor; set => Set(ref lightColor, value); }
        private Color lightColor = Colors.White;

        [Display(GroupName = "簡易描画", Name = "このアイテムにも適用", Description = "ONにすると、このアイテム自体にライティングを適用します。")]
        [ToggleSlider]
        public bool ApplyToItem { get => applyToItem; set => Set(ref applyToItem, value); }
        private bool applyToItem = false;

        [Display(GroupName = "簡易描画", Name = "環境光", Description = "「このアイテムにも適用」がONの時の、影の明るさ")]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation Ambient { get; } = new Animation(0.3f, 0, 1);

        public enum LightSourceType
        {
            [Display(Name = "点光源 (Point)")]
            Point,
            [Display(Name = "平行光源 (Directional)")]
            Directional
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new LightSourceEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            return [LightId, X, Y, Z, Intensity, Ambient];
        }
    }

}