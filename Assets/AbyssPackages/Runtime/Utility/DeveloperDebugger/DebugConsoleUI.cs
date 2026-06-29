using UnityEngine;
using TMPro;
using JackyUtility;

public class DebugConsoleUI : MonoBehaviour
{
    [SerializeField] private GameObject consolePanel;
    [SerializeField] private TMP_InputField inputField;
    //[SerializeField] private TextMeshProUGUI logText;
    //[SerializeField] private int maxLogLines = 50;
    [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote; // ~ key

    private bool isOpen;

    private void Start()
    {
        consolePanel.SetActive(false);
        isOpen = false;

        //Application.logMessageReceived += OnLogMessageReceived;
    }

    private void OnDestroy()
    {
        //Application.logMessageReceived -= OnLogMessageReceived;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleConsole();
        }

        if (isOpen && Input.GetKeyDown(KeyCode.Return))
        {
            SubmitCommand();
        }
    }

    private void ToggleConsole()
    {
        isOpen = !isOpen;
        consolePanel.SetActive(isOpen);

        if (isOpen)
        {
            inputField.text = "";
            inputField.ActivateInputField();
        }
        DebugConsoleManager.Instance.InvokeOnConsoleToggled(isOpen);
    }

    private void SubmitCommand()
    {
        string input = inputField.text.Trim();
        if (string.IsNullOrEmpty(input))
            return;

        //AppendLog($"> {input}");
        DebugConsoleManager.Instance.ExecuteCommand(input);

        inputField.text = "";
        inputField.ActivateInputField();
    }

    //private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    //{
    //    if (!isOpen) return;

    //    string prefix = type switch
    //    {
    //        LogType.Error => "<color=red>[ERROR]</color> ",
    //        LogType.Warning => "<color=yellow>[WARN]</color> ",
    //        _ => ""
    //    };

    //    AppendLog($"{prefix}{condition}");
    //}

    //private void AppendLog(string message)
    //{
    //    if (logText == null) return;

    //    logText.text += message + "\n";

    //    // Trim old lines if exceeding limit
    //    string[] lines = logText.text.Split('\n');
    //    if (lines.Length > maxLogLines)
    //    {
    //        logText.text = string.Join("\n", lines, lines.Length - maxLogLines, maxLogLines);
    //    }
    //}
}