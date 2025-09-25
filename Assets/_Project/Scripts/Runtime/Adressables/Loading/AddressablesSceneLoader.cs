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
            throw new ArgumentException(nameof(sceneKey));
        
        var sw = Stopwatch.StartNew();

        var previous = _currentSceneHandle;
        var load = Addressables.LoadSceneAsync(sceneKey, loadSceneMode, activateOnLoad: true);

        while (!load.IsDone)
        {
            _currentProgress = load.PercentComplete;
            await Task.Yield();
        }

        if (load.Status != AsyncOperationStatus.Succeeded)
        {
            var msg = load.OperationException?.Message ?? "Unknown";
            Debug.LogError($"[SceneLoader] LoadSceneAsync FAIL '{sceneKey}': {msg}");
            
            if (load.IsValid())
                Addressables.Release(load);
            
            _currentSceneHandle = null;
            _currentProgress = 0f;
            
            throw load.OperationException ?? new InvalidKeyException($"Key '{sceneKey}' is not a Scene.");
        }

        _currentSceneHandle = load;
        _currentProgress = 1f;
        
        if (previous.HasValue && previous.Value.IsValid())
        {
            try
            {
                var unloadPrev = Addressables.UnloadSceneAsync(previous.Value, true);
                while (!unloadPrev.IsDone) await Task.Yield();
                Addressables.Release(previous.Value);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SceneLoader] Unload previous scene failed: {e.Message}");
            }
        }

        sw.Stop();
        Debug.Log($"[SceneLoader] Scene '{sceneKey}' loaded in {sw.ElapsedMilliseconds} ms");
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