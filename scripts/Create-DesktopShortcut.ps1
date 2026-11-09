[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$executablePath = Join-Path $repositoryRoot "src\DiscordFloatingWidget\bin\$Configuration\net10.0-windows\DiscordFloatingWidget.exe"

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Build the widget first: dotnet build .\src\DiscordFloatingWidget\DiscordFloatingWidget.csproj --configuration $Configuration"
}

# Windows resolves redirected desktops (including OneDrive) for the current user.
$desktopDirectory = [Environment]::GetFolderPath('DesktopDirectory')
if ([string]::IsNullOrWhiteSpace($desktopDirectory) -or
    -not (Test-Path -LiteralPath $desktopDirectory -PathType Container)) {
    throw 'Windows could not locate your desktop folder.'
}

$shortcutPath = Join-Path $desktopDirectory 'Discord Widget.lnk'
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcut = $shortcutShell.CreateShortcut($shortcutPath)

if ((Test-Path -LiteralPath $shortcutPath) -and
    -not [string]::Equals($shortcut.TargetPath, $executablePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "A different shortcut already exists at '$shortcutPath'. Rename or remove it before running this script."
}

if ($PSCmdlet.ShouldProcess($shortcutPath, 'Create or refresh the Discord Widget desktop shortcut')) {
    $shortcut.TargetPath = $executablePath
    $shortcut.WorkingDirectory = Split-Path -Parent $executablePath
    $shortcut.Description = 'Open Discord Widget'
    $shortcut.IconLocation = "$executablePath,0"
    $shortcut.Save()

    $savedShortcut = $shortcutShell.CreateShortcut($shortcutPath)
    if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf) -or
        $savedShortcut.TargetPath -ne $executablePath -or
        $savedShortcut.WorkingDirectory -ne (Split-Path -Parent $executablePath)) {
        throw 'The desktop shortcut could not be verified.'
    }

    Write-Output "Desktop shortcut ready: $shortcutPath"
}
