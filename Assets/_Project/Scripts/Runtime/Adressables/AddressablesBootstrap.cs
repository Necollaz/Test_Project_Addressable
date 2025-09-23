using System.Threading.Tasks;
using UnityEngine;
using Zenject;

public class AddressablesBootstrap : MonoBehaviour
{
    [Inject] private readonly AddressablesInitializer addressablesInitializer;
    [Inject] private readonly AddressablesDiagnostics addressablesDiagnostics;

    private async void Awake()
    {
        addressablesDiagnostics.Enable();
        await SafeInitialize();
    }

    private async Task SafeInitialize()
    {
        try
        {
            await addressablesInitializer.InitializeAndUpdateCatalogsAsync();
        }
        catch (System.SystemException exception)
        {
            Debug.LogError($"Addressables initialization failed: {exception.Message}");
        }
    }

    private void OnDestroy()
    {
        addressablesDiagnostics.Disable();
    }
}