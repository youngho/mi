using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core
{
    [Serializable]
    public class MissionMetadata
    {
        public string missionId = "";
        public string title = "";
        public string description = "";
        public string author = "";
        public string version = "1.0.0";
        public string bundleUrl = "";
        public string bundleHash = "";
        public int requiredLevel = 1;
        public int entryFee = 0;
        public int timeLimit = 180;
        public int targetScore = 5000;
        public string category = "official";
    }

    public enum BundleLoadState
    {
        Idle,
        Downloading,
        Loading,
        Loaded,
        Failed
    }

    /// <summary>
    /// Addressables 기반 미션 동적 로드.
    /// Unity Editor에서는 Resources/StreamingAssets 폴백 지원.
    /// </summary>
    public sealed class MissionBundleLoader
    {
        readonly string _cacheDir;
        readonly Dictionary<string, GameObject> _loaded = new();

        public BundleLoadState State { get; private set; } = BundleLoadState.Idle;
        public string? LastError { get; private set; }

        public MissionBundleLoader(string? cacheDir = null)
        {
            _cacheDir = cacheDir ?? Path.Combine(Application.persistentDataPath, "mission_cache");
            Directory.CreateDirectory(_cacheDir);
        }

        public bool ValidateMetadata(MissionMetadata meta)
        {
            if (string.IsNullOrEmpty(meta.missionId) || string.IsNullOrEmpty(meta.bundleUrl))
            {
                LastError = "Invalid metadata";
                return false;
            }
            if (!MissionSDKVersion.IsCompatible(meta.version))
            {
                LastError = $"SDK version mismatch: mission={meta.version} core={MissionSDKVersion.Current}";
                return false;
            }
            return true;
        }

        public async Task<GameObject?> LoadMissionAsync(MissionMetadata meta)
        {
            if (!ValidateMetadata(meta))
            {
                State = BundleLoadState.Failed;
                return null;
            }

            if (_loaded.TryGetValue(meta.missionId, out var cached))
            {
                State = BundleLoadState.Loaded;
                return cached;
            }

            try
            {
                State = BundleLoadState.Downloading;
                var localPath = await EnsureCachedAsync(meta);

                State = BundleLoadState.Loading;
                var prefab = await LoadPrefabFromBundleAsync(localPath, meta.missionId);
                if (prefab == null)
                {
                    LastError = "Prefab not found in bundle";
                    State = BundleLoadState.Failed;
                    return null;
                }

                _loaded[meta.missionId] = prefab;
                State = BundleLoadState.Loaded;
                return prefab;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                State = BundleLoadState.Failed;
                return null;
            }
        }

        async Task<string> EnsureCachedAsync(MissionMetadata meta)
        {
            var fileName = $"{meta.missionId}_{meta.version}.bundle";
            var path = Path.Combine(_cacheDir, fileName);
            if (File.Exists(path))
            {
                if (string.IsNullOrEmpty(meta.bundleHash) || VerifyHash(path, meta.bundleHash))
                    return path;
                File.Delete(path);
            }

#if UNITY_ADDRESSABLES
            // 프로덕션: UnityWebRequest로 meta.bundleUrl 다운로드
            await DownloadAsync(meta.bundleUrl, path);
#else
            await Task.Run(() =>
            {
                var fallback = Path.Combine(Application.streamingAssetsPath, "Missions", fileName);
                if (File.Exists(fallback))
                    File.Copy(fallback, path, true);
                else
                    throw new FileNotFoundException($"Bundle not found: {fallback}");
            });
#endif
            if (!string.IsNullOrEmpty(meta.bundleHash) && !VerifyHash(path, meta.bundleHash))
                throw new InvalidDataException("Bundle hash mismatch");

            return path;
        }

        static async Task DownloadAsync(string url, string dest)
        {
            using var www = UnityEngine.Networking.UnityWebRequest.Get(url);
            www.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(dest);
            var op = www.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                throw new Exception(www.error);
        }

        static async Task<GameObject?> LoadPrefabFromBundleAsync(string bundlePath, string missionId)
        {
#if UNITY_EDITOR || !UNITY_ADDRESSABLES
            await Task.CompletedTask;
            var fallback = GameObject.Find($"Mission_{missionId}");
            if (fallback != null)
                return fallback;
            return new GameObject($"Mission_{missionId}");
#else
            var bundle = await AssetBundle.LoadFromFileAsync(bundlePath);
            await bundle;
            var loaded = (bundle as AssetBundleCreateRequest)?.assetBundle;
            var prefab = loaded?.LoadAssetAsync<GameObject>(missionId);
            await prefab;
            return (prefab as AssetBundleRequest)?.asset as GameObject;
#endif
        }

        static bool VerifyHash(string path, string expectedHex)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            var hash = sha.ComputeHash(fs);
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            return hex == expectedHex.ToLowerInvariant();
        }

        public void UnloadMission(string missionId)
        {
            if (_loaded.TryGetValue(missionId, out var go))
            {
                if (go != null)
                    UnityEngine.Object.Destroy(go);
                _loaded.Remove(missionId);
            }
#if UNITY_ADDRESSABLES
            // Addressables.Release(handle);
#endif
            if (_loaded.Count == 0)
                State = BundleLoadState.Idle;
        }

        public void UnloadAll()
        {
            foreach (var id in new List<string>(_loaded.Keys))
                UnloadMission(id);
        }
    }
}
