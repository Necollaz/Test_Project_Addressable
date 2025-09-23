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
        await UnloadCurrentSceneAsync();

        long dependenciesSizeBytes = await Addressables.GetDownloadSizeAsync(sceneKey).Task;
        var sceneStopwatch = Stopwatch.StartNew();

        if (dependenciesSizeBytes > 0)
        {
            var downloadDependenciesHandle = Addressables.DownloadDependenciesAsync(sceneKey, true);
            while (!downloadDependenciesHandle.IsDone)
            {
                _currentProgress = downloadDependenciesHandle.PercentComplete * DEPENDENCIES_PROGRESS_PORTION;
                await Task.Yield();
            }
            Addressables.Release(downloadDependenciesHandle);
        }

        var loadSceneHandle = Addressables.LoadSceneAsync(sceneKey, loadSceneMode, true);
        while (!loadSceneHandle.IsDone)
        {
            _currentProgress = DEPENDENCIES_PROGRESS_PORTION + loadSceneHandle.PercentComplete * SCENE_PROGRESS_PORTION;
            await Task.Yield();
        }

        _currentSceneHandle = loadSceneHandle;
        _currentProgress = PROGRESS_COMPLETE_VALUE;

        sceneStopwatch.Stop();
        Debug.Log($"[Scene] Loaded '{sceneKey}' in {sceneStopwatch.ElapsedMilliseconds} ms; deps: {dependenciesSizeBytes} bytes");
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

            var unloadOp = Resources.UnloadUnusedAssets();
            await AwaitAsyncOperation(unloadOp);

            System.GC.Collect();
        }
    }

    private static Task AwaitAsyncOperation(AsyncOperation operation)
    {
        var completionSource = new TaskCompletionSource<bool>();
        operation.completed += _ => completionSource.SetResult(true);
        return completionSource.Task;
    }
}