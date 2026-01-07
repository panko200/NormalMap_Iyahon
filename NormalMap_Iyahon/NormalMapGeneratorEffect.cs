using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace NormalMap_Iyahon
{


    [VideoEffect("NMIノーマルマップ生成", ["描画", "加工"], ["normal", "generate", "作成", "NMI", "map"])]
    public class NormalMapGeneratorEffect : VideoEffectBase
    {
        public override string Label => "NMIノーマルマップ生成";

        [Display(GroupName = "設定", Name = "生成モード", Description = "生成する画像の種類")]
        [EnumComboBox]
        public GeneratorMode Mode { get => mode; set => Set(ref mode, value); }
        private GeneratorMode mode = GeneratorMode.Normal;

        [Display(GroupName = "生成", Name = "強度", Description = "凹凸の強さ")]
        [AnimationSlider("F1", "", 0, 20)]
        public Animation Strength { get; } = new Animation(5.0f, 0, 100);

        [Display(GroupName = "生成", Name = "広さ", Description = "線を拾う範囲（太さ）。値を上げると太い線も立体化されます。")]
        [AnimationSlider("F1", "px", 1, 10)]
        public Animation Radius { get; } = new Animation(1.0f, 1, 50);

        [Display(GroupName = "生成", Name = "ぼかし", Description = "生成前に画像をぼかして、ノイズを減らします。")]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Blur { get; } = new Animation(0f, 0, 50);

        public enum GeneratorMode
        {
            [Display(Name = "ノーマルマップ")]
            Normal,
            [Display(Name = "ハイトマップ (白黒)")]
            Height
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
            return [Strength, Radius, Blur];
        }
    }



}