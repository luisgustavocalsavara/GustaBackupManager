using System.Runtime.InteropServices;

namespace GustaBackupManager;

[ComVisible(true)]
public class WinFormInterop(Action hideLoadingOverlay)
{
    public void HideLoadingOverlayFromJs() => hideLoadingOverlay();
}
