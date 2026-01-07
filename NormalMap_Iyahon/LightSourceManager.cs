using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Windows;
using YukkuriMovieMaker.Commons;

//グループ制御の値読み取れへん！！！！！！！！！おこるぞ！　！！,きれました　がちぎれ

namespace NormalMap_Iyahon
{
    public enum LightType
    {
        Point,
        Directional
    }
    public struct LightData
    {
        public Vector3 Position;
        public float Intensity;
        public Vector3 Color;
        public LightType Type;
    }

    public interface ILightSourceProvider
    {
        LightData GetLightData();
    }

    public static class LightSourceManager
    {
        private static readonly Dictionary<int, List<ILightSourceProvider>> _providers = new();
        private static readonly Dictionary<int, LightData> _backupData = new();

        private static readonly object _lock = new object();

        private static readonly LightData _defaultData = new LightData
        {
            Position = new Vector3(0, 0, 200),
            Intensity = 1.0f,
            Color = new Vector3(1, 1, 1),
            Type = LightType.Point
        };

        // --- Reflection用キャッシュ ---
        private static PropertyInfo? _currentProjectProp;
        private static PropertyInfo? _timelineProp;
        private static PropertyInfo? _itemsProp;
        private static PropertyInfo? _layerProp;
        private static PropertyInfo? _startProp;
        private static PropertyInfo? _lengthProp;
        private static PropertyInfo? _videoEffectsProp;
        private static PropertyInfo? _modelProp;
        private static PropertyInfo? _groupItemsProp;

        // ★追加: 現在の再生位置を取得するためのプロパティ
        private static PropertyInfo? _currentFrameProp;

        private static bool _reflectionFailed = false;

        public static void Register(int id, ILightSourceProvider provider, LightData currentData)
        {
            lock (_lock)
            {
                if (!_providers.ContainsKey(id)) _providers[id] = new List<ILightSourceProvider>();
                if (!_providers[id].Contains(provider)) _providers[id].Add(provider);

                _backupData[id] = currentData;
            }
        }

        public static void UpdateData(int id, LightData currentData)
        {
            lock (_lock)
            {
                _backupData[id] = currentData;
            }
        }

        public static void Unregister(int id, ILightSourceProvider provider)
        {
            lock (_lock)
            {
                if (_providers.ContainsKey(id))
                {
                    _providers[id].Remove(provider);
                    if (_providers[id].Count == 0) _providers.Remove(id);
                }
            }
        }

        // ★修正: frame引数を削除（内部で取得するから）
        public static LightData GetData(int id)
        {
            lock (_lock)
            {
                // 1. 登録済みのプロバイダがあればそれを使う（最優先・高速）
                if (_providers.ContainsKey(id) && _providers[id].Count > 0)
                {
                    return _providers[id].Last().GetLightData();
                }

                // 2. バックアップがあればそれを使う（次点）
                if (_backupData.ContainsKey(id))
                {
                    return _backupData[id];
                }
            }

            // 3. なければタイムラインをスキャン (Reflection)
            if (!_reflectionFailed)
            {
                try
                {
                    var scannedData = ScanTimelineForLight(id);
                    if (scannedData.HasValue)
                    {
                        return scannedData.Value;
                    }
                }
                catch
                {
                    _reflectionFailed = true;
                }
            }

            return _defaultData;
        }

        private static LightData? ScanTimelineForLight(int targetId)
        {
            if (Application.Current == null) return null;
            var mainWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.GetType().FullName == "YukkuriMovieMaker.Views.MainView");
            if (mainWindow == null) return null;

            var mainVM = mainWindow.DataContext;
            if (mainVM == null) return null;

            if (_currentProjectProp == null)
            {
                var mainType = mainVM.GetType();
                _currentProjectProp = mainType.GetProperty("CurrentProject");
                if (_currentProjectProp == null) return null;
            }

            var currentProjectReactive = _currentProjectProp.GetValue(mainVM);
            var currentProject = GetReactiveValue(currentProjectReactive);
            if (currentProject == null) return null;

            // ★追加: 現在のフレーム（絶対時間）を取得
            if (_currentFrameProp == null) _currentFrameProp = currentProject.GetType().GetProperty("CurrentFrame");
            var currentFrameReactive = _currentFrameProp?.GetValue(currentProject);
            var currentFrameObj = GetReactiveValue(currentFrameReactive);

            if (currentFrameObj == null) return null;
            long currentFrame = (long)currentFrameObj; // 絶対フレーム数

            if (_timelineProp == null) _timelineProp = currentProject.GetType().GetProperty("Timeline");
            var timelineReactive = _timelineProp?.GetValue(currentProject);
            var timeline = GetReactiveValue(timelineReactive);
            if (timeline == null) return null;

            if (_itemsProp == null) _itemsProp = timeline.GetType().GetProperty("Items");
            var itemsCollection = _itemsProp?.GetValue(timeline) as IEnumerable;
            if (itemsCollection == null) return null;

            // 取得した絶対時間を使ってスキャン
            return ScanItemsRecursive(itemsCollection, targetId, currentFrame, 0);
        }

        private static LightData? ScanItemsRecursive(IEnumerable items, int targetId, long frame, long offsetTime)
        {
            foreach (var itemVM in items)
            {
                var itemType = itemVM.GetType();
                if (_layerProp == null) _layerProp = itemType.GetProperty("Layer");
                if (_startProp == null) _startProp = itemType.GetProperty("Start");
                if (_lengthProp == null) _lengthProp = itemType.GetProperty("Length");
                if (_videoEffectsProp == null) _videoEffectsProp = itemType.GetProperty("VideoEffects");

                long start = (long)(_startProp?.GetValue(itemVM) ?? 0L);
                long length = (long)(_lengthProp?.GetValue(itemVM) ?? 0L);

                long absoluteStart = start + offsetTime;

                // 絶対時間で判定
                if (frame < absoluteStart || frame >= absoluteStart + length) continue;

                var effects = _videoEffectsProp?.GetValue(itemVM) as IEnumerable;
                if (effects != null)
                {
                    foreach (var effectVM in effects)
                    {
                        if (_modelProp == null) _modelProp = effectVM.GetType().GetProperty("Model");
                        var effectModel = _modelProp?.GetValue(effectVM);

                        if (effectModel is LightSourceEffect lightEffect)
                        {
                            int id = (int)lightEffect.LightId.Values[0].Value;

                            if (id == targetId)
                            {
                                // アイテム相対時間に変換して値を計算
                                long itemRelativeTime = frame - absoluteStart;
                                int fps = 60;

                                float x = (float)lightEffect.X.GetValue(itemRelativeTime, length, fps);
                                float y = (float)lightEffect.Y.GetValue(itemRelativeTime, length, fps);
                                float z = (float)lightEffect.Z.GetValue(itemRelativeTime, length, fps);
                                float intensity = (float)lightEffect.Intensity.GetValue(itemRelativeTime, length, fps);

                                var color = new Vector3(
                                    lightEffect.LightColor.R / 255f,
                                    lightEffect.LightColor.G / 255f,
                                    lightEffect.LightColor.B / 255f
                                );

                                LightType type = lightEffect.Type == LightSourceEffect.LightSourceType.Directional
                                    ? LightType.Directional
                                    : LightType.Point;

                                return new LightData
                                {
                                    Position = new Vector3(x, y, z),
                                    Intensity = intensity,
                                    Color = color,
                                    Type = type
                                };
                            }
                        }
                    }
                }

                if (_groupItemsProp == null) _groupItemsProp = itemType.GetProperty("Items");
                var subItems = _groupItemsProp?.GetValue(itemVM) as IEnumerable;
                if (subItems != null)
                {
                    var result = ScanItemsRecursive(subItems, targetId, frame, absoluteStart);
                    if (result.HasValue) return result;
                }
            }

            return null;
        }

        private static object? GetReactiveValue(object? reactiveProp)
        {
            if (reactiveProp == null) return null;
            var propInfo = reactiveProp.GetType().GetProperty("Value");
            return propInfo?.GetValue(reactiveProp);
        }
    }



}