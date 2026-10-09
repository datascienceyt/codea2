# Compila el proyecto sin abrir Unity: powershell -File Docs/Herramientas/compilar.ps1
#
# Los .csproj los genera Unity y listan los scripts uno a uno: un script creado fuera de Unity
# no está en ellos y dotnet build diría "correcto" sin haberlo compilado. Este script copia los
# .csproj, les añade todo lo que haya en Assets/_Main/Scripts y Assets/_Main/Editor (y quita lo
# que ya no existe), compila el de editor —que arrastra al de juego— y los deja como estaban.

[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runtime = Join-Path $root "Assembly-CSharp.csproj"
$editor = Join-Path $root "Assembly-CSharp-Editor.csproj"

if (-not (Test-Path $runtime)) { Write-Error "No hay .csproj: abre el proyecto en Unity una vez para que los genere."; exit 1 }

$backupRuntime = Join-Path $env:TEMP "codea_runtime.csproj.bak"
$backupEditor = Join-Path $env:TEMP "codea_editor.csproj.bak"
Copy-Item $runtime $backupRuntime -Force
Copy-Item $editor $backupEditor -Force

function Sync-Project([string]$project, [string]$folder) {
    $text = [IO.File]::ReadAllText($project)

    # Fuera los scripts de _Main que ya no existen (movidos o borrados).
    $text = [regex]::Replace($text, '<Compile Include="(Assets\\_Main\\[^"]+)"\s*/>', {
        param($m)
        if (Test-Path (Join-Path $root $m.Groups[1].Value)) { $m.Value } else { "" }
    })

    # Dentro los que falten, delante del primer <Compile>.
    $added = @()
    foreach ($file in Get-ChildItem -Recurse -Filter *.cs (Join-Path $root $folder)) {
        $relative = $file.FullName.Substring($root.Length + 1)
        if (-not $text.Contains("Include=`"$relative`"")) { $added += "<Compile Include=`"$relative`" />" }
    }

    if ($added.Count -gt 0) {
        $at = $text.IndexOf("<Compile Include=")
        $text = $text.Insert($at, ($added -join ""))
    }

    [IO.File]::WriteAllText($project, $text)
    return $added.Count
}

try {
    $n1 = Sync-Project $runtime "Assets\_Main\Scripts"
    $n2 = Sync-Project $editor "Assets\_Main\Editor"
    Write-Output "Scripts añadidos a la copia de los .csproj: $n1 de juego, $n2 de editor"

    # Solo errores y el resumen: los avisos de API obsoleta del SDK de Unity son muchos y conocidos.
    dotnet build $editor -v:q --nologo | Select-String " error |Compilaci|Build succeeded|Errores|Error(es)"
    $code = $LASTEXITCODE
}
finally {
    Copy-Item $backupRuntime $runtime -Force
    Copy-Item $backupEditor $editor -Force
}

exit $code
