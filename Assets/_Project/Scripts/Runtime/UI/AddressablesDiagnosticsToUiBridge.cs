using UnityEngine;
using Zenject;

public class AddressablesDiagnosticsToUiBridge : MonoBehaviour
{
    [SerializeField] private UILogView _uiLogView;

    [Inject] private readonly AddressablesDiagnostics addressablesDiagnostics;

    private void OnEnable()
    {
        addressablesDiagnostics.OnAssetLoaded += HandleLoaded;
        addressablesDiagnostics.OnAssetUnloaded += HandleUnloaded;
    }

    private void OnDisable()
    {
        addressablesDiagnostics.OnAssetLoaded -= HandleLoaded;
        addressablesDiagnostics.OnAssetUnloaded -= HandleUnloaded;
    }

    private void HandleLoaded(string key)
    {
        _uiLogView.Append($"Loaded: {key}");
    }

    private void HandleUnloaded(string key)
    {
        _uiLogView.Append($"Unloaded: {key}");
    }
}