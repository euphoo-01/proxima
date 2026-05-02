param(
    [string]$Target = "src/Proxima.App/Views"
)

if (-not (Test-Path $Target)) {
    Write-Host "Style guard target does not exist yet: $Target"
    Write-Host "Treat as pass only during guardrail setup. Once Views exist, this command must scan them."
    exit 0
}

$patterns = @(
    @{ Pattern = '#[0-9A-Fa-f]{6,8}'; Message = 'Raw hex colors are forbidden in production Views.' },
    @{ Pattern = 'Background="'; Message = 'Inline Background is forbidden in production Views.' },
    @{ Pattern = 'Foreground="'; Message = 'Inline Foreground is forbidden in production Views.' },
    @{ Pattern = 'BorderBrush="'; Message = 'Inline BorderBrush is forbidden in production Views.' },
    @{ Pattern = 'CornerRadius="'; Message = 'Inline CornerRadius is forbidden in production Views.' },
    @{ Pattern = 'FontSize="'; Message = 'Inline FontSize is forbidden in production Views.' },
    @{ Pattern = 'FontWeight="'; Message = 'Inline FontWeight is forbidden in production Views.' },
    @{ Pattern = 'BoxShadow="'; Message = 'Inline BoxShadow is forbidden in production Views.' },
    @{ Pattern = '<UserControl\.Styles>|<Window\.Styles>'; Message = 'Page-local reusable styles are forbidden in production Views.' }
)

$failed = $false
$files = Get-ChildItem -Path $Target -Filter *.axaml -Recurse
foreach ($file in $files) {
    $lines = Get-Content $file.FullName
    for ($i = 0; $i -lt $lines.Length; $i++) {
        foreach ($entry in $patterns) {
            if ($lines[$i] -match $entry.Pattern) {
                Write-Host "$($file.FullName):$($i + 1): $($lines[$i])"
                Write-Host "FAIL: $($entry.Message)"
                $failed = $true
            }
        }
    }
}

if ($failed) {
    Write-Host "AXAML style guard failed."
    exit 1
}

Write-Host "AXAML style guard passed."
