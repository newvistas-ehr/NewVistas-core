# Script to add [Id(n)] attributes to all properties in state classes

$stateFiles = Get-ChildItem "NewVistas.Abstractions\GrainStates\*.cs"

foreach ($file in $stateFiles) {
    Write-Host "Processing $($file.Name)..."
    
    $content = Get-Content $file.FullName -Raw
    $lines = Get-Content $file.FullName
    
    $newLines = @()
    $propertyIndex = 0
    $inClass = $false
    
    for ($i = 0; $i < $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Detect if we're in the class
        if ($line -match 'public class \w+State') {
            $inClass = $true
        }
        
        # Check if this is a property line
        if ($inClass -and $line -match '^\s*public .* \{ get; set; \}') {
            # Check if previous line already has [Id attribute
            if ($i -gt 0 -and $lines[$i-1] -notmatch '\[Id\(') {
                # Add [Id(n)] before the property
                $indent = $line -replace '^(\s*).*', '$1'
                $newLines += "$indent[Id($propertyIndex)]"
                $propertyIndex++
            }
        }
        
        $newLines += $line
    }
    
    $newLines | Set-Content $file.FullName
    Write-Host "Added $propertyIndex [Id] attributes to $($file.Name)"
}

Write-Host "`nDone! Run 'dotnet build' to verify."
