param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,
    [Parameter(Mandatory = $true)]
    [string]$OutDir
)

Set-StrictMode -Version Latest

$sourceLayoutDir = Join-Path $ProjectDir "Resources\layout"
$androidNs = 'http://schemas.android.com/apk/res/android'

function Get-StubTagName {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Node
    )

    $tagName = $Node.Name
    if ([string]::IsNullOrWhiteSpace($tagName)) {
        return 'View'
    }

    if ($tagName -ieq 'view') {
        $className = $null
        if ($Node.Attributes -ne $null -and $Node.Attributes['class'] -ne $null) {
            $className = $Node.Attributes['class'].Value
        }

        if (-not [string]::IsNullOrWhiteSpace($className)) {
            return $className
        }

        return 'View'
    }

    switch -Regex ($tagName) {
        '^(merge|include|requestFocus|blink)$' { return 'View' }
        '^fragment$' { return 'FrameLayout' }
        default { return $tagName }
    }
}

function Add-PlaceholderNode {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[string]]$Lines,
        [Parameter(Mandatory = $true)]
        [string]$TagName,
        [Parameter(Mandatory = $true)]
        [string]$IdName
    )

    [void]$Lines.Add("        <$TagName")
    [void]$Lines.Add("            android:id=`"@+id/$IdName`"")
    [void]$Lines.Add('            android:layout_width="0dp"')
    [void]$Lines.Add('            android:layout_height="0dp"')
    [void]$Lines.Add("            android:visibility=`"gone`" />")
}

if (Test-Path $OutDir) {
    Remove-Item -Recurse -Force $OutDir
}

if (-not (Test-Path $sourceLayoutDir)) {
    exit 0
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

Get-ChildItem -Path $sourceLayoutDir -File -Filter *.xml | ForEach-Object {
    [xml]$xml = Get-Content -Path $_.FullName -Encoding UTF8 -Raw
    $idEntries = [ordered]@{}
    $rootTagName = 'LinearLayout'
    $containerTagName = $null

    if ($xml.DocumentElement -ne $null) {
        $rootTagName = Get-StubTagName -Node $xml.DocumentElement
        if ($rootTagName -ieq 'View') {
            $rootTagName = 'LinearLayout'
        }
    }

    if ($rootTagName -match '(^|\.)(ScrollView|NestedScrollView|HorizontalScrollView)$') {
        $containerTagName = 'LinearLayout'
    }

    $nodes = $xml.SelectNodes('//*')
    foreach ($node in $nodes) {
        $idValue = $node.GetAttribute('id', $androidNs)
        if ([string]::IsNullOrWhiteSpace($idValue)) {
            continue
        }

        $idName = $idValue -replace '^@\+?id/', ''
        if ([string]::IsNullOrWhiteSpace($idName)) {
            continue
        }

        if (-not $idEntries.Contains($idName)) {
            $idEntries[$idName] = Get-StubTagName -Node $node
        }
    }

    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add('<?xml version="1.0" encoding="utf-8"?>')
    [void]$lines.Add("<$rootTagName xmlns:android=`"$androidNs`"")
    [void]$lines.Add('    android:layout_width="match_parent"')
    [void]$lines.Add('    android:layout_height="match_parent"')

    $rootIdValue = $null
    if ($xml.DocumentElement -ne $null) {
        $rootIdValue = $xml.DocumentElement.GetAttribute('id', $androidNs)
    }
    if (-not [string]::IsNullOrWhiteSpace($rootIdValue)) {
        $rootIdName = $rootIdValue -replace '^@\+?id/', ''
        if (-not [string]::IsNullOrWhiteSpace($rootIdName)) {
            [void]$lines.Add("    android:id=`"@+id/$rootIdName`"")
            if (-not $idEntries.Contains($rootIdName)) {
                $idEntries[$rootIdName] = $rootTagName
            }
        }
    }

    if ($rootTagName -match '(^|\.)(LinearLayout)$') {
        [void]$lines.Add('    android:orientation="vertical">')
    }
    else {
        [void]$lines.Add('>')
    }

    if ($containerTagName -ne $null) {
        [void]$lines.Add("    <$containerTagName")
        [void]$lines.Add('        android:layout_width="match_parent"')
        [void]$lines.Add('        android:layout_height="wrap_content"')
        [void]$lines.Add('        android:orientation="vertical">')
    }

    foreach ($id in $idEntries.Keys) {
        $tagName = $idEntries[$id]
        if ([string]::IsNullOrWhiteSpace($tagName)) {
            $tagName = 'View'
        }

        Add-PlaceholderNode -Lines $lines -TagName $tagName -IdName $id
    }

    if ($containerTagName -ne $null) {
        [void]$lines.Add("    </$containerTagName>")
    }

    [void]$lines.Add("</$rootTagName>")

    $targetPath = Join-Path $OutDir $_.Name
    [System.IO.File]::WriteAllText($targetPath, [string]::Join([System.Environment]::NewLine, $lines), $utf8NoBom)
}
