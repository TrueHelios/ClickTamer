using System.Windows.Forms;

namespace ClickTamer;

/// <summary>Runs ClickTamer without showing its main window.</summary>
internal sealed class TrayOnlyApplicationContext : ApplicationContext
{
    private readonly Form _form;

    public TrayOnlyApplicationContext(Form form)
    {
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _form.FormClosed += OnFormClosed;
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _form.FormClosed -= OnFormClosed;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _form.FormClosed -= OnFormClosed;

        base.Dispose(disposing);
    }
}
