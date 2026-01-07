using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Settings;
using static NormalMap_Iyahon.NormalMapEffect;

namespace NormalMap_Iyahon
{
    [VideoEffect("NMIノーマルマップ", ["描画"], ["normal", "NMI", "map"])]
    public class NormalMapEffect : VideoEffectBase
    {
        public override string Label => "NMIノーマルマップ";

        [Display(GroupName = "設定", Name = "光源ID", Description = "使用する光源設定のIDを指定してください。")]
        [AnimationSlider("F0", "番", 0, 9)]
        public Animation LightId { get; } = new Animation(0, 0, 99);

        [Display(GroupName = "設定", Name = "光源モード", Description = "光源の扱い方を指定します。")]
        [EnumComboBox]
        public LightCalcType LightcalcMode { get => lightcalcMode; set => Set(ref lightcalcMode, value); }
        private LightCalcType lightcalcMode = LightCalcType.Absolute;

        [Display(GroupName = "設定", Name = "マップの種類", Description = "画像のタイプに合わせて切り替えてください。")]
        [EnumComboBox]
        public MapType Type { get => type; set => Set(ref type, value); }
        private MapType type = MapType.Normal;

        // SourceTypeとMapIdを削除

        [Display(GroupName = "設定", Name = "マップ画像", Description = "画像を選択してください。")]
        [FileSelector(FileGroupType.ImageItem)]
        public string NormalMapPath { get => normalMapPath; set => Set(ref normalMapPath, value); }
        private string normalMapPath = string.Empty;

        [Display(GroupName = "配置", Name = "自動調整", Description = "ON: アイテムのサイズに合わせて画像を自動で伸縮させます。\nOFF: 下のパラメータで位置やサイズを手動調整します。")]
        [ToggleSlider]
        public bool AutoFit { get => autoFit; set => Set(ref autoFit, value); }
        private bool autoFit = true;

        [Display(GroupName = "配置", Name = "X座標")][AnimationSlider("F1", "px", -1000, 1000)] public Animation MapX { get; } = new Animation(0, -10000, 10000);
        [Display(GroupName = "配置", Name = "Y座標")][AnimationSlider("F1", "px", -1000, 1000)] public Animation MapY { get; } = new Animation(0, -10000, 10000);
        [Display(GroupName = "配置", Name = "拡大率")][AnimationSlider("F1", "%", 0, 200)] public Animation MapScale { get; } = new Animation(100f, 0, 5000);
        [Display(GroupName = "配置", Name = "回転")][AnimationSlider("F1", "°", -360, 360)] public Animation MapRotation { get; } = new Animation(0, -36000, 36000);
        [Display(GroupName = "配置", Name = "ぼかし")][AnimationSlider("F1", "px", 0, 20)] public Animation MapBlur { get; } = new Animation(0, 0, 100);

        [Display(GroupName = "質感", Name = "深度")][AnimationSlider("F1", "", 0, 100)] public Animation SurfaceScale { get; } = new Animation(30.0f, 0, 500);
        [Display(GroupName = "質感", Name = "環境光")][AnimationSlider("F2", "", 0, 1)] public Animation Ambient { get; } = new Animation(0.3f, 0, 1);

        public enum MapType
        {
            [Display(Name = "ノーマルマップ (紫)")]
            Normal,
            [Display(Name = "ハイトマップ (白黒)")]
            Height
        }

        public enum LightCalcType
        {
            [Display(Name = "画面固定 (絶対座標)")]
            Absolute,
            [Display(Name = "アイテム追従 (相対座標)")]
            Relative
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new NormalMapEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            return [LightId, SurfaceScale, Ambient, MapX, MapY, MapScale, MapRotation, MapBlur];
        }
    }



}