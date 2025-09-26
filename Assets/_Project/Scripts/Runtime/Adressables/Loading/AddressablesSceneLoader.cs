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
    private AsyncOperationHandle<SceneInstance>? _currentSceneHandle;
    private float _currentProgress;

    public float CurrentSceneLoadingProgress => _currentProgress;

    public async Task LoadSceneAsync(string sceneKey, LoadSceneMode mode)
    {
        if (sceneKey == null)
            throw new ArgumentNullException(nameof(sceneKey));

        var stopwatch = Stopwatch.StartNew();
        var previous = _currentSceneHandle;
        var load = Addressables.LoadSceneAsync(sceneKey, mode, activateOnLoad: true);

        while (!load.IsDone)
        {
            _currentProgress = load.PercentComplete;
            await Task.Yield();
        }

        if (load.Status != AsyncOperationStatus.Succeeded)
        {
            var message = load.OperationException?.Message ?? "Unknown";
            
            Debug.LogError($"[SceneLoader] LoadSceneAsync FAIL '{sceneKey}': {message}");
            
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
                var unloadPreview = Addressables.UnloadSceneAsync(previous.Value, true);
                
                while (!unloadPreview.IsDone)
                    await Task.Yield();
                
                Addressables.Release(previous.Value);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SceneLoader] Unload previous scene failed: {exception.Message}");
            }
        }

        stopwatch.Stop();
        
        Debug.Log($"[SceneLoader] Scene '{sceneKey}' loaded in {stopwatch.ElapsedMilliseconds} ms | Active: {SceneManager.GetActiveScene().name}");
    }

    public async Task LoadSceneWithExplicitActivationAsync(string sceneKey, LoadSceneMode mode)
    {
        var load = Addressables.LoadSceneAsync(sceneKey, mode, activateOnLoad: false);

        while (load.PercentComplete < 0.9f)
        {
            Debug.Log($"[Diag] Pre-activate progress: {load.PercentComplete:P0}");
            
            await Task.Yield();
        }

        Debug.Log("[Diag] ReadyToActivate");
        var activate = load.Result.ActivateAsync();

        while (!activate.isDone)
        {
            Debug.Log($"[Diag] Activating... {activate.progress:P0}");
            
            await Task.Yield();
        }

        if (load.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"[Diag] FAIL: {load.OperationException}");
            
            if (load.IsValid())
                Addressables.Release(load);
            
            throw load.OperationException ?? new Exception("Activation failed");
        }

        Debug.Log($"[Diag] Activated. ActiveScene={SceneManager.GetActiveScene().name}");
    }
    
    // public async Task UnloadCurrentSceneAsync()
    // {
    //     if (_currentSceneHandle.HasValue)
    //     {
    //         var handle = _currentSceneHandle.Value;
    //
    //         if (handle.IsValid())
    //         {
    //             var unloadHandle = Addressables.UnloadSceneAsync(handle, true);
    //
    //             while (!unloadHandle.IsDone)
    //                 await Task.Yield();
    //
    //             Addressables.Release(handle);
    //         }
    //
    //         _currentSceneHandle = null;
    //         _currentProgress = 0f;
    //
    //         AsyncOperation unloadOperation = Resources.UnloadUnusedAssets();
    //         await AwaitAsyncOperation(unloadOperation);
    //
    //         GC.Collect();
    //     }
    // }
    //
    // private Task AwaitAsyncOperation(AsyncOperation operation)
    // {
    //     var completionSource = new TaskCompletionSource<bool>();
    //     operation.completed += _ => completionSource.SetResult(true);
    //     
    //     return completionSource.Task;
    // }
}