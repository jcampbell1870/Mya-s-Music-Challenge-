namespace MyasMusicChallenge;

/// <summary>Minimal modal text input used for the player name and wallet address.</summary>
public sealed class TextPromptDialog : Form
{
    private readonly TextBox _input;

    private TextPromptDialog(string title, string message, string initialValue, int maxLength)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(520, 150);

        var label = new Label { Text = message, Left = 12, Top = 12, Width = 496, Height = 40 };
        _input = new TextBox { Left = 12, Top = 58, Width = 496, MaxLength = maxLength, Text = initialValue };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 336, Top = 104, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 428, Top = 104, Width = 80 };
        Controls.AddRange([label, _input, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public static string? Show(IWin32Window owner, string title, string message, string initialValue, int maxLength)
    {
        using var dialog = new TextPromptDialog(title, message, initialValue, maxLength);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._input.Text.Trim() : null;
    }
}
