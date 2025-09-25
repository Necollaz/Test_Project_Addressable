using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class AddressablesSceneLoader
{
    private const float DEPENDENCIES_PROGRESS_PORTION = 0.20f;
    private const float SCENE_PROGRESS_PORTION = 0.80f;
    private const float PROGRESS_COMPLETE_VALUE = 1f;

    private AsyncOperationHandle<SceneInstance>? _currentSceneHandle;
    private float _currentProgress;

    public float CurrentSceneLoadingProgress => _currentProgress;

    public async Task LoadSceneAsync(string sceneKey, LoadSceneMode loadSceneMode)
    {
        if (string.IsNullOrWhiteSpace(sceneKey))
            throw new ArgumentException("Scene key is null or empty.", nameof(sceneKey));

        long depsBytes = 0;

        try
        {
            depsBytes = await Addressables.GetDownloadSizeAsync(sceneKey).Task;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneLoader] GetDownloadSize failed for '{sceneKey}': {e.Message}");
            throw;
        }

        var swTotal = Stopwatch.StartNew();

        if (depsBytes > 0)
        {
            Debug.Log($"[SceneLoader] Downloading deps for '{sceneKey}'… ({depsBytes} B expected)");
            var deps = Addressables.DownloadDependenciesAsync(sceneKey, true);

            while (!deps.IsDone)
            {
                _currentProgress = deps.PercentComplete * DEPENDENCIES_PROGRESS_PORTION;
                await Task.Yield();
            }

            if (deps.Status != AsyncOperationStatus.Succeeded)
            {
                Exception exception = deps.OperationException ??
                                      new InvalidOperationException($"Failed to download deps for '{sceneKey}'.");
                Addressables.Release(deps);
                _currentProgress = 0f;

                throw exception;
            }

            Addressables.Release(deps);
        }

        var previous = _currentSceneHandle;
        UnityEngine.Debug.Log($"[SceneLoader] Loading scene '{sceneKey}' ({loadSceneMode})…");

        var load = Addressables.LoadSceneAsync(sceneKey, loadSceneMode, true);

        while (!load.IsDone)
        {
            _currentProgress = DEPENDENCIES_PROGRESS_PORTION + load.PercentComplete * SCENE_PROGRESS_PORTION;
            await Task.Yield();
        }

        if (load.Status != AsyncOperationStatus.Succeeded)
        {
            var msg = load.OperationException?.Message ?? "Unknown";
            Debug.LogError($"[SceneLoader] LoadSceneAsync FAIL '{sceneKey}': {msg}");
            Addressables.Release(load);
            _currentSceneHandle = null;
            _currentProgress = 0f;
            throw load.OperationException ?? new InvalidKeyException($"Key '{sceneKey}' is not a Scene (SceneInstance).");
        }

        _currentSceneHandle = load;
        _currentProgress = PROGRESS_COMPLETE_VALUE;

        if (previous.HasValue)
        {
            var preview = previous.Value;

            if (preview.IsValid())
            {
                try
                {
                    var unloadPrev = Addressables.UnloadSceneAsync(preview, true);

                    while (!unloadPrev.IsDone)
                        await Task.Yield();

                    Addressables.Release(preview);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SceneLoader] Unload previous scene failed: {e.Message}");
                }
            }
        }

        swTotal.Stop();
        Debug.Log($"[SceneLoader] Scene '{sceneKey}' loaded in {swTotal.ElapsedMilliseconds} ms");
    }

    public async Task UnloadCurrentSceneAsync()
    {
        if (_currentSceneHandle.HasValue)
        {
            var handle = _currentSceneHandle.Value;

            if (handle.IsValid())
            {
                var unloadHandle = Addressables.UnloadSceneAsync(handle, true);

                while (!unloadHandle.IsDone)
                    await Task.Yield();

                Addressables.Release(handle);
            }

            _currentSceneHandle = null;
            _currentProgress = 0f;

            AsyncOperation unloadOperation = Resources.UnloadUnusedAssets();
            await AwaitAsyncOperation(unloadOperation);

            GC.Collect();
        }
    }

    private Task AwaitAsyncOperation(AsyncOperation operation)
    {
        var completionSource = new TaskCompletionSource<bool>();
        operation.completed += _ => completionSource.SetResult(true);

        return completionSource.Task;
    }
}