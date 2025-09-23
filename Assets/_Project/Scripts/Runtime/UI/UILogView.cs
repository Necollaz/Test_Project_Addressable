using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UILogView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _logText;
    [SerializeField] private int _maxVisibleLines = 200;
    
    private readonly Queue<string> _linesQueue = new Queue<string>();

    public void Append(string message)
    {
        _linesQueue.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {message}");

        while (_linesQueue.Count > _maxVisibleLines)
            _linesQueue.Dequeue();

        _logText.text = string.Join("\n", _linesQueue);
    }

    public void ClearAll()
    {
        _linesQueue.Clear();
        _logText.text = string.Empty;
    }
}
