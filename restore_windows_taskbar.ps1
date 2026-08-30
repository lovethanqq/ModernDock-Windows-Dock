[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

if (-not ('PublicTaskbarRecovery' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class PublicTaskbarRecovery {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr handle, int command);
}
'@
}

$shown = 0
foreach ($className in @('Shell_TrayWnd', 'Shell_SecondaryTrayWnd')) {
    $handle = [PublicTaskbarRecovery]::FindWindow($className, $null)
    if ($handle -ne [IntPtr]::Zero) {
        if ([PublicTaskbarRecovery]::ShowWindow($handle, 5)) { $shown++ }
    }
}

Write-Output "Native taskbar restore requested. Windows=$shown"
