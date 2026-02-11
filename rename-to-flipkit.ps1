# Rename CardLister to FlipKit across all files
$files = Get-ChildItem -Path . -Include *.cs,*.csproj,*.sln,*.json,*.md,*.axaml,*.xaml -Recurse |
    Where-Object { $_.FullName -notlike '*\obj\*' -and $_.FullName -notlike '*\bin\*' }

$count = 0
foreach ($file in $files) {
    try {
        $content = Get-Content $file.FullName -Raw -ErrorAction Stop

        $updated = $content `
            -creplace 'CardLister\.Desktop', 'FlipKit.Desktop' `
            -creplace 'CardLister\.Core', 'FlipKit.Core' `
            -creplace 'CardLister\.Web', 'FlipKit.Web' `
            -creplace 'CardLister\.Api', 'FlipKit.Api' `
            -creplace 'CardListerDbContext', 'FlipKitDbContext' `
            -creplace 'CardLister', 'FlipKit' `
            -creplace 'cardlister', 'flipkit'

        if ($content -ne $updated) {
            Set-Content $file.FullName $updated -NoNewline
            $count++
            Write-Host "Updated: $($file.Name)"
        }
    } catch {
        Write-Warning "Skipped: $($file.Name) - $($_.Exception.Message)"
    }
}

Write-Host "`n========================================="
Write-Host "Total files updated: $count"
Write-Host "========================================="
