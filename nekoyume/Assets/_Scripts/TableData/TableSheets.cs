using System;
using System.Collections;
using System.IO;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Nekoyume.TableData
{
    public class TableSheets
    {
        public readonly ReactiveProperty<float> loadProgress = new ReactiveProperty<float>();

        public BackgroundSheet BackgroundSheet { get; private set; }
        public WorldSheet WorldSheet { get; private set; }
        public StageSheet StageSheet { get; private set; }
        public StageRewardSheet StageRewardSheet { get; private set; }
        public CharacterSheet CharacterSheet { get; private set; }
        public LevelSheet LevelSheet { get; private set; }
        public SkillSheet SkillSheet { get; private set; }
        public BuffSheet BuffSheet { get; private set; }

        public IEnumerator CoInitialize()
        {
#if UNITY_EDITOR
            return CoInitializeForEditor();
#else
            return CoInitializeForPlayer();
#endif
        }

        public IEnumerator CoInitializeForEditor()
        {
            loadProgress.Value = 0f;

            var loadLocationOperation = Addressables.LoadResourceLocationsAsync("TableCSV");
            yield return loadLocationOperation;
            var locations = loadLocationOperation.Result;
            var loadTaskCount = locations.Count;
            var loadedTaskCount = 0;
            foreach (var location in locations)
            {
                var loadAssetOperation = Addressables.LoadAssetAsync<TextAsset>(location);
                yield return loadAssetOperation;
                var textAsset = loadAssetOperation.Result;
                SetToSheet(textAsset.name, textAsset.text);
                loadedTaskCount++;
                loadProgress.Value = (float) loadedTaskCount / loadTaskCount;
            }

            loadProgress.Value = 1f;
        }

        public IEnumerator CoInitializeForPlayer()
        {
            loadProgress.Value = 0f;
            
            var request = Resources.LoadAsync<AddressableAssetsContainer>("AddressableAssetsContainer");
            yield return request;
            if (!(request.asset is AddressableAssetsContainer addressableAssetsContainer))
            {
                throw new NullReferenceException(nameof(addressableAssetsContainer));
            }
            
            var tableCsvAssets = addressableAssetsContainer.tableCsvAssets;
            var loadTaskCount = tableCsvAssets.Count;
            var loadedTaskCount = 0;
            //어드레서블어셋에 새로운 테이블을 추가하면 AddressableAssetsContainer.asset에도 해당 csv파일을 추가해줘야합니다.
            foreach (var textAsset in tableCsvAssets)
            {
                SetToSheet(textAsset.name, textAsset.text);
                loadedTaskCount++;
                loadProgress.Value = (float) loadedTaskCount / loadTaskCount;
            }

            loadProgress.Value = 1f;
        }

        private void SetToSheet(string name, string csv)
        {
            switch (name)
            {
                case nameof(TableData.BackgroundSheet):
                    BackgroundSheet = new BackgroundSheet();
                    BackgroundSheet.Set(csv);
                    break;
                case nameof(TableData.WorldSheet):
                    WorldSheet = new WorldSheet();
                    WorldSheet.Set(csv);
                    break;
                case nameof(TableData.StageSheet):
                    StageSheet = new StageSheet();
                    StageSheet.Set(csv);
                    break;
                case nameof(TableData.StageRewardSheet):
                    StageRewardSheet = new StageRewardSheet();
                    StageRewardSheet.Set(csv);
                    break;
                case nameof(TableData.CharacterSheet):
                    CharacterSheet = new CharacterSheet();
                    CharacterSheet.Set(csv);
                    break;
                case nameof(TableData.LevelSheet):
                    LevelSheet = new LevelSheet();
                    LevelSheet.Set(csv);
                    break;
                case nameof(TableData.SkillSheet):
                    SkillSheet = new SkillSheet();
                    SkillSheet.Set(csv);
                    break;
                case nameof(TableData.BuffSheet):
                    BuffSheet = new BuffSheet();
                    BuffSheet.Set(csv);
                    break;
                default:
                    throw new InvalidDataException($"Not found {name} class in namespace `TableData`");
            }
        }
    }
}
