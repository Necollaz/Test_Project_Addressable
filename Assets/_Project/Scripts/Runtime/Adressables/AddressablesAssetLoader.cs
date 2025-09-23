using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zenject;

public class AddressablesAssetLoader
{
    private const float BYTES_IN_KILOBYTE = 1024f;
    private const float KILOBYTES_IN_MEGABYTE = 1024f;

    private readonly System.Collections.Generic.Dictionary<string, AsyncOperationHandle> _assetHandlesByKey 
        = new System.Collections.Generic.Dictionary<string, AsyncOperationHandle>(256);

    private readonly System.Collections.Generic.HashSet<string> _loadedAssetKeys 
        = new System.Collections.Generic.HashSet<string>();

    private readonly AddressablesDiagnostics _addressablesDiagnostics;

    [Inject]
    public AddressablesAssetLoader(AddressablesDiagnostics addressablesDiagnostics)
    {
        _addressablesDiagnostics = addressablesDiagnostics;
    }

    public System.Collections.Generic.IReadOnlyCollection<string> LoadedAssetKeys => _loadedAssetKeys;

    public async Task<TAsset> LoadAssetAsync<TAsset>(string assetKey) where TAsset : class
    {
        if (_assetHandlesByKey.ContainsKey(assetKey) && _assetHandlesByKey[assetKey].IsValid())
            return _assetHandlesByKey[assetKey].Result as TAsset;

        long expectedBytes = await GetExpectedDownloadBytesAsync(assetKey);
        if (expectedBytes > 0)
            await EnsureDependenciesDownloadedWithRealtimeLogAsync(assetKey);

        _addressablesDiagnostics.StartTimer(assetKey);

        var loadHandle = Addressables.LoadAssetAsync<TAsset>(assetKey);
        await loadHandle.Task;

        _assetHandlesByKey[assetKey] = loadHandle;
        _loadedAssetKeys.Add(assetKey);
        _addressablesDiagnostics.NotifyAssetLoaded(assetKey);

        return loadHandle.Result;
    }

    public async Task UnloadAssetAsync(string assetKey)
    {
        if (_assetHandlesByKey.TryGetValue(assetKey, out var handle))
        {
            if (handle.IsValid())
                Addressables.Release(handle);

            _assetHandlesByKey.Remove(assetKey);

            if (_loadedAssetKeys.Remove(assetKey))
                _addressablesDiagnostics.NotifyAssetUnloaded(assetKey);
        }

        await Task.Yield();
    }

    public async Task UnloadAllAssetsAsync()
    {
        foreach (var pair in _assetHandlesByKey)
        {
            if (pair.Value.IsValid())
                Addressables.Release(pair.Value);
        }

        _assetHandlesByKey.Clear();

        foreach (var key in _loadedAssetKeys)
            _addressablesDiagnostics.NotifyAssetUnloaded(key);

        _loadedAssetKeys.Clear();

        var unloadOp = Resources.UnloadUnusedAssets();
        await AwaitAsyncOperation(unloadOp);

        System.GC.Collect();
    }

    public async Task<long> GetExpectedDownloadBytesAsync(object keyOrLabel)
    {
        var sizeHandle = Addressables.GetDownloadSizeAsync(keyOrLabel);
        long bytes = await sizeHandle.Task;
        Addressables.Release(sizeHandle);
        return bytes;
    }

    public async Task<bool> EnsureDependenciesDownloadedAsync(object keyOrLabel)
    {
        long expectedBytes = await GetExpectedDownloadBytesAsync(keyOrLabel);
        if (expectedBytes <= 0)
            return true;

        var stopwatch = Stopwatch.StartNew();
        var downloadHandle = Addressables.DownloadDependenciesAsync(keyOrLabel, true);
        await downloadHandle.Task;
        stopwatch.Stop();

        Addressables.Release(downloadHandle);

        float megabytes = expectedBytes / (BYTES_IN_KILOBYTE * KILOBYTES_IN_MEGABYTE);
        UnityEngine.Debug.Log($"Downloaded dependencies: {megabytes:F2} MB in {stopwatch.ElapsedMilliseconds} ms for [{keyOrLabel}]");

        return true;
    }

    // Вариант с живым прогрессом и байтами — красиво для консоли/UILogView
    public async Task<bool> EnsureDependenciesDownloadedWithRealtimeLogAsync(object keyOrLabel)
    {
        long expectedBytes = await GetExpectedDownloadBytesAsync(keyOrLabel);
        if (expectedBytes <= 0)
            return true;

        var stopwatch = Stopwatch.StartNew();
        var handle = Addressables.DownloadDependenciesAsync(keyOrLabel, true);

        while (!handle.IsDone)
        {
            var status = handle.GetDownloadStatus(); // DownloadedBytes / TotalBytes
            float percent = status.Percent;
            UnityEngine.Debug.Log($"[Download] {percent:P0} ({status.DownloadedBytes}/{status.TotalBytes} bytes) for [{keyOrLabel}]");
            await Task.Yield();
        }

        stopwatch.Stop();
        Addressables.Release(handle);

        float megabytes = expectedBytes / (BYTES_IN_KILOBYTE * KILOBYTES_IN_MEGABYTE);
        UnityEngine.Debug.Log($"[Download] Completed: {megabytes:F2} MB in {stopwatch.ElapsedMilliseconds} ms for [{keyOrLabel}]");

        return true;
    }

    public async Task ClearCacheForAsync(object keyOrLabel)
    {
        var clearHandle = Addressables.ClearDependencyCacheAsync(keyOrLabel, true);
        await clearHandle.Task;
        Addressables.Release(clearHandle);
    }

    private static Task AwaitAsyncOperation(AsyncOperation operation)
    {
        var completionSource = new TaskCompletionSource<bool>();
        operation.completed += _ => completionSource.SetResult(true);
        return completionSource.Task;
    }
}