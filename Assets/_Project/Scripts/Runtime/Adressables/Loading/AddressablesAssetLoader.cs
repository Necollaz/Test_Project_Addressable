using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zenject;
using Debug = UnityEngine.Debug;

public class AddressablesAssetLoader
{
    private readonly AddressablesDiagnostics addressablesDiagnostics;

    private readonly Dictionary<string, AsyncOperationHandle> assetHandlesByKey =
        new Dictionary<string, AsyncOperationHandle>(256);

    private readonly HashSet<string> loadedAssetKeys = new HashSet<string>();

    [Inject]
    public AddressablesAssetLoader(AddressablesDiagnostics addressablesDiagnostics)
    {
        this.addressablesDiagnostics = addressablesDiagnostics;
    }

    public IReadOnlyCollection<string> LoadedAssetKeys => loadedAssetKeys;

    public async Task<TAsset> LoadAssetAsync<TAsset>(string assetKey) where TAsset : class
    {
        if (assetHandlesByKey.ContainsKey(assetKey) && assetHandlesByKey[assetKey].IsValid())
            return assetHandlesByKey[assetKey].Result as TAsset;

        long expectedBytes = await GetExpectedDownloadBytesAsync(assetKey);

        if (expectedBytes > 0)
            await EnsureDependenciesDownloadedWithLogsAsync(assetKey);

        addressablesDiagnostics.StartTimer(assetKey);

        var loadHandle = Addressables.LoadAssetAsync<TAsset>(assetKey);

        await loadHandle.Task;

        assetHandlesByKey[assetKey] = loadHandle;
        loadedAssetKeys.Add(assetKey);
        addressablesDiagnostics.NotifyAssetLoaded(assetKey);

        return loadHandle.Result;
    }

    public async Task UnloadAllAssetsAsync()
    {
        foreach (var pair in assetHandlesByKey)
        {
            if (pair.Value.IsValid())
                Addressables.Release(pair.Value);
        }

        assetHandlesByKey.Clear();

        foreach (var key in loadedAssetKeys)
            addressablesDiagnostics.NotifyAssetUnloaded(key);

        loadedAssetKeys.Clear();

        AsyncOperation unloadOperation = Resources.UnloadUnusedAssets();
        await AwaitAsyncOperation(unloadOperation);

        GC.Collect();
    }

    public async Task<long> GetExpectedDownloadBytesAsync(object keyOrLabel)
    {
        var sizeHandle = Addressables.GetDownloadSizeAsync(keyOrLabel);
        long bytes = await sizeHandle.Task;

        Addressables.Release(sizeHandle);

        return bytes;
    }

    public async Task<bool> EnsureDependenciesDownloadedWithLogsAsync(object keyOrLabel)
    {
        var sizeHandle = Addressables.GetDownloadSizeAsync(keyOrLabel);
        long expectedBytes = await sizeHandle.Task;
        Addressables.Release(sizeHandle);

        if (expectedBytes <= 0)
        {
            Debug.Log($"[Addressables][Deps] {keyOrLabel} expected 0 B (cached).");
            
            return true;
        }

        var stopwatch = Stopwatch.StartNew();
        
        var downloadDependencies = Addressables.DownloadDependenciesAsync(keyOrLabel, true);
        long lastBytes = -1;

        while (!downloadDependencies.IsDone)
        {
            var status = downloadDependencies.GetDownloadStatus();
            
            if (status.TotalBytes > 0 && status.DownloadedBytes != lastBytes)
            {
                lastBytes = status.DownloadedBytes;
                
                Debug.Log($"[Addressables][Deps] {keyOrLabel} {lastBytes}/{status.TotalBytes} B");
            }
            
            await Task.Yield();
        }
        
        var finalStatus = downloadDependencies.GetDownloadStatus();
        stopwatch.Stop();
        
        Debug.Log($"[Addressables][Deps][Done] {keyOrLabel} downloaded {finalStatus.DownloadedBytes} B in {stopwatch.ElapsedMilliseconds} ms");

        return true;
    }

    private Task AwaitAsyncOperation(AsyncOperation operation)
    {
        var completionSource = new TaskCompletionSource<bool>();
        operation.completed += _ => completionSource.SetResult(true);

        return completionSource.Task;
    }
}