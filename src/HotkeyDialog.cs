namespace MicOff;

sealed class HotkeyDialog : Form
{
    public Hotkey? Result { get; private set; }

    public HotkeyDialog()
    {
        Text = "Nuevo atajo";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(360, 110);
        Font = new Font("Segoe UI", 10f);

        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Apretá la tecla o combinación\n(también sirven F13-F24, Pausa o los botones laterales del mouse)\n\nEsc para cancelar",
        };
        label.MouseDown += (_, e) => OnMouse(e.Button);
        Controls.Add(label);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        OnMouse(e.Button);
        base.OnMouseDown(e);
    }

    void OnMouse(MouseButtons button)
    {
        var key = button switch
        {
            MouseButtons.XButton1 => Keys.XButton1,
            MouseButtons.XButton2 => Keys.XButton2,
            _ => Keys.None,
        };
        if (key != Keys.None) Accept(new Hotkey(key, HotkeyManager.CurrentModifiers()));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        if (key == Keys.Escape && (keyData & Keys.Modifiers) == Keys.None)
        {
            DialogResult = DialogResult.Cancel;
            return true;
        }
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.None)
            return base.ProcessCmdKey(ref msg, keyData);

        Accept(new Hotkey(key, HotkeyManager.CurrentModifiers()));
        return true;
    }

    void Accept(Hotkey hotkey)
    {
        Result = hotkey;
        DialogResult = DialogResult.OK;
    }
}
