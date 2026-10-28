<#
.SYNOPSIS
    Regenerates every checked-in website-template image manifest from the real files.

.DESCRIPTION
    The backend seeder must not read the frontend checkout at runtime, so the
    discovered image inventories are embedded in the Infrastructure assembly as
    Seed/Assets/<template-key>.assets.json.

    This is the ONE generator for all templates. It processes every manifest in
    the asset directory (or a single one with -TemplateKey) and refreshes it
    deterministically:

      * only supported image files are considered;
      * entries are written sorted by file name (ordinal);
      * width, height, byte size and SHA-256 are recomputed from the files;
      * reviewed alternative text and the hero/about/gallery placements are
        preserved verbatim;
      * every image must be either placed or explicitly listed as unassigned.

    It fails loudly when the directory and the manifest disagree, because a
    silent removal or a new image without alternative text would produce a
    configuration nobody reviewed. To add an image: add an entry (with alt
    text) to the manifest, then re-run this script. Asset IDs and URLs are NOT
    stored here: the seeder derives them deterministically from the template's
    assetIdPrefix (default: templateKey) and the file name.

.EXAMPLE
    pwsh scripts/generate-template-asset-manifests.ps1

.EXAMPLE
    pwsh scripts/generate-template-asset-manifests.ps1 -TemplateKey ember-oak

.EXAMPLE
    pwsh scripts/generate-template-asset-manifests.ps1 `
        -FrontendRoot 'C:\path\to\Resto.pkfrontend'
#>
[CmdletBinding()]
param(
    [string]$FrontendRoot,
    [string]$ManifestDirectory,
    [string]$TemplateKey
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 leaves $PSScriptRoot empty while it evaluates parameter
# defaults for an advanced script, so default the paths here instead.
$scriptDirectory = $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($scriptDirectory)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
}

if ([string]::IsNullOrWhiteSpace($FrontendRoot)) {
    $FrontendRoot = Join-Path $scriptDirectory '..\..\Resto.pkfrontend'
}

if ([string]::IsNullOrWhiteSpace($ManifestDirectory)) {
    $ManifestDirectory = Join-Path $scriptDirectory '..\src\RestaurantSaaS.Infrastructure\Seed\Assets'
}

$FrontendRoot = [System.IO.Path]::GetFullPath($FrontendRoot)
$ManifestDirectory = [System.IO.Path]::GetFullPath($ManifestDirectory)

if (-not (Test-Path -LiteralPath $FrontendRoot -PathType Container)) {
    throw "Frontend root not found: $FrontendRoot"
}

if (-not (Test-Path -LiteralPath $ManifestDirectory -PathType Container)) {
    throw "Manifest directory not found: $ManifestDirectory"
}

$supportedExtensions = @('.png', '.jpg', '.jpeg', '.webp', '.avif', '.gif')

$manifestFiles = Get-ChildItem -LiteralPath $ManifestDirectory -File -Filter '*.assets.json' |
    Sort-Object -Property Name -CaseSensitive

if (-not [string]::IsNullOrWhiteSpace($TemplateKey)) {
    $manifestFiles = @($manifestFiles | Where-Object {
        $_.Name -eq "$($TemplateKey.Trim().ToLowerInvariant()).assets.json"
    })

    if ($manifestFiles.Count -eq 0) {
        throw "No manifest named '$($TemplateKey.Trim().ToLowerInvariant()).assets.json' in $ManifestDirectory"
    }
}

if ($manifestFiles.Count -eq 0) {
    throw "No *.assets.json manifests found in $ManifestDirectory"
}

# ConvertTo-Json's whitespace differs between PowerShell versions, so the
# manifests get a tiny deterministic printer (two-space indent, LF friendly)
# instead. Scalars are still escaped by ConvertTo-Json itself.
function Format-JsonValue($value, [int]$depth) {
    $pad = ' ' * ($depth * 2)
    $innerPad = ' ' * (($depth + 1) * 2)

    if ($null -eq $value -or
        $value -is [string] -or
        $value -is [bool] -or
        $value -is [int] -or
        $value -is [long] -or
        $value -is [double] -or
        $value -is [decimal]) {
        return Format-JsonScalar $value
    }

    if ($value -is [System.Collections.IDictionary]) {
        $keys = @($value.Keys)

        if ($keys.Count -eq 0) {
            return '{}'
        }

        $parts = foreach ($key in $keys) {
            '{0}{1}: {2}' -f $innerPad,
                (Format-JsonScalar ([string]$key)),
                (Format-JsonValue $value[$key] ($depth + 1))
        }

        return "{`n" + ($parts -join ",`n") + "`n$pad}"
    }

    if ($value -is [System.Management.Automation.PSCustomObject]) {
        $properties = @($value.PSObject.Properties)

        if ($properties.Count -eq 0) {
            return '{}'
        }

        $parts = foreach ($property in $properties) {
            '{0}{1}: {2}' -f $innerPad,
                (Format-JsonScalar $property.Name),
                (Format-JsonValue $property.Value ($depth + 1))
        }

        return "{`n" + ($parts -join ",`n") + "`n$pad}"
    }

    if ($value -is [System.Collections.IEnumerable]) {
        $items = @($value)

        if ($items.Count -eq 0) {
            return '[]'
        }

        $parts = foreach ($item in $items) {
            $innerPad + (Format-JsonValue $item ($depth + 1))
        }

        return "[`n" + ($parts -join ",`n") + "`n$pad]"
    }

    return Format-JsonScalar $value
}

function Format-JsonScalar($value) {
    if ($null -eq $value) {
        return 'null'
    }

    if ($value -is [bool]) {
        return $(if ($value) { 'true' } else { 'false' })
    }

    if ($value -is [string]) {
        # ConvertTo-Json escapes HTML-sensitive characters (for example '&' as
        # \u0026). Undo that for ampersands only: it keeps the directory names in
        # the reviewed manifests readable and the output byte-stable.
        return (($value | ConvertTo-Json -Compress) -replace '\\u0026', '&')
    }

    return [string]$value
}

function Convert-ToAssetId([string]$prefix, [string]$fileName) {
    $builder = [System.Text.StringBuilder]::new($prefix.ToLowerInvariant())
    [void]$builder.Append('-')
    $previousWasDash = $false

    foreach ($character in $fileName.Trim().ToLowerInvariant().ToCharArray()) {
        if (($character -ge [char]'a' -and $character -le [char]'z') -or
            ($character -ge [char]'0' -and $character -le [char]'9')) {
            [void]$builder.Append($character)
            $previousWasDash = $false
            continue
        }

        if (-not $previousWasDash) {
            [void]$builder.Append('-')
            $previousWasDash = $true
        }
    }

    return $builder.ToString().TrimEnd('-')
}

Add-Type -AssemblyName System.Drawing

$written = 0

foreach ($manifestFile in $manifestFiles) {
    $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json

    if ($manifest.schemaVersion -ne 1) {
        throw "$($manifestFile.Name): unsupported schemaVersion '$($manifest.schemaVersion)'."
    }

    if ([string]::IsNullOrWhiteSpace($manifest.templateKey)) {
        throw "$($manifestFile.Name): templateKey is required."
    }

    $templateKey = [string]$manifest.templateKey

    if ($manifestFile.Name -ne "$templateKey.assets.json") {
        throw "$($manifestFile.Name): expected file name '$templateKey.assets.json'."
    }

    $assetIdPrefix = if ($manifest.PSObject.Properties.Name -contains 'assetIdPrefix' -and
        -not [string]::IsNullOrWhiteSpace($manifest.assetIdPrefix)) {
        [string]$manifest.assetIdPrefix
    }
    else {
        $templateKey
    }

    if ([string]::IsNullOrWhiteSpace($manifest.directory) -or
        -not ([string]$manifest.directory).StartsWith('assets/', [System.StringComparison]::Ordinal)) {
        throw "$($manifestFile.Name): directory must be a relative path under 'assets/'."
    }

    if (([string]$manifest.directory).Contains('..') -or
        ([string]$manifest.directory).Contains('\') -or
        ([string]$manifest.directory).Contains(':')) {
        throw "$($manifestFile.Name): directory must not contain traversal, backslashes or schemes."
    }

    $relativeDirectory = ([string]$manifest.directory).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $imageDirectory = Join-Path (Join-Path $FrontendRoot 'public') $relativeDirectory

    if (-not (Test-Path -LiteralPath $imageDirectory -PathType Container)) {
        throw "$($manifestFile.Name): image directory not found: $imageDirectory"
    }

    $existingByName = @{}

    foreach ($asset in @($manifest.assets)) {
        if ([string]::IsNullOrWhiteSpace($asset.fileName)) {
            throw "$($manifestFile.Name): every asset requires a fileName."
        }

        $name = [string]$asset.fileName

        if ($existingByName.ContainsKey($name)) {
            throw "$($manifestFile.Name): manifest declares '$name' more than once."
        }

        $alt = if ($null -eq $asset.alt) { '' } else { ([string]$asset.alt).Trim() }

        if ([string]::IsNullOrWhiteSpace($alt)) {
            throw "$($manifestFile.Name): '$name' requires reviewed alternative text."
        }

        if ($alt.Length -gt 240) {
            throw "$($manifestFile.Name): '$name' alternative text exceeds 240 characters."
        }

        $existingByName[$name] = $asset
    }

    $files = @(Get-ChildItem -LiteralPath $imageDirectory -File |
        Where-Object { $supportedExtensions -contains $_.Extension.ToLowerInvariant() } |
        Sort-Object -Property Name -CaseSensitive)

    if ($files.Count -eq 0) {
        throw "$($manifestFile.Name): no supported image files found in $imageDirectory"
    }

    foreach ($file in $files) {
        if (-not $existingByName.ContainsKey($file.Name)) {
            throw "$($manifestFile.Name): no manifest entry (and alternative text) for '$($file.Name)'. Add one before regenerating."
        }
    }

    foreach ($name in $existingByName.Keys) {
        if (-not (Test-Path -LiteralPath (Join-Path $imageDirectory $name) -PathType Leaf)) {
            throw "$($manifestFile.Name): manifest references '$name', which is not present in $imageDirectory."
        }
    }

    $placements = $manifest.placements

    if ($null -eq $placements) {
        throw "$($manifestFile.Name): 'placements' is required."
    }

    foreach ($slot in @('hero', 'about')) {
        $reference = $placements.$slot

        if (-not [string]::IsNullOrWhiteSpace($reference) -and
            -not $existingByName.ContainsKey([string]$reference)) {
            throw "$($manifestFile.Name): '$slot' placement references '$reference', which is not in the manifest."
        }
    }

    $gallery = @()

    if ($null -ne $placements.gallery) {
        $gallery = @($placements.gallery | ForEach-Object { [string]$_ })
    }

    $seenGallery = @{}

    foreach ($reference in $gallery) {
        if (-not $existingByName.ContainsKey($reference)) {
            throw "$($manifestFile.Name): 'gallery' placement references '$reference', which is not in the manifest."
        }

        if ($seenGallery.ContainsKey($reference)) {
            throw "$($manifestFile.Name): 'gallery' references '$reference' more than once."
        }

        $seenGallery[$reference] = $true
    }

    $unassigned = @()

    if ($null -ne $manifest.unassigned) {
        $unassigned = @($manifest.unassigned | ForEach-Object { [string]$_ })
    }

    $assigned = @{}

    foreach ($slot in @('hero', 'about')) {
        $reference = $placements.$slot

        if (-not [string]::IsNullOrWhiteSpace($reference)) {
            $assigned[[string]$reference] = $slot
        }
    }

    foreach ($reference in $gallery) {
        $assigned[$reference] = 'gallery'
    }

    foreach ($name in $unassigned) {
        if (-not $existingByName.ContainsKey($name)) {
            throw "$($manifestFile.Name): 'unassigned' references '$name', which is not in the manifest."
        }

        if ($assigned.ContainsKey($name)) {
            throw "$($manifestFile.Name): '$name' is both placed and listed as unassigned."
        }
    }

    foreach ($file in $files) {
        if (-not $assigned.ContainsKey($file.Name) -and $unassigned -notcontains $file.Name) {
            throw "$($manifestFile.Name): '$($file.Name)' needs an explicit placement or an 'unassigned' entry."
        }
    }

    $assetIds = @{}

    $assets = foreach ($file in $files) {
        $assetId = Convert-ToAssetId $assetIdPrefix $file.Name

        if ($assetIds.ContainsKey($assetId)) {
            throw "$($manifestFile.Name): asset ID collision '$assetId' between '$($assetIds[$assetId])' and '$($file.Name)'."
        }

        $assetIds[$assetId] = $file.Name

        $image = [System.Drawing.Image]::FromFile($file.FullName)

        try {
            $width = $image.Width
            $height = $image.Height
        }
        finally {
            $image.Dispose()
        }

        $sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $previous = $existingByName[$file.Name]

        if ($null -eq $previous.bytes -or
            [long]$previous.bytes -ne $file.Length -or
            [int]$previous.width -ne $width -or
            [int]$previous.height -ne $height -or
            [string]$previous.sha256 -ne $sha256) {
            Write-Host "  refreshed metadata: $($file.Name)"
        }

        [ordered]@{
            fileName = $file.Name
            alt      = ([string]$previous.alt).Trim()
            width    = $width
            height   = $height
            bytes    = $file.Length
            sha256   = $sha256
        }
    }

    $orderedPlacements = [ordered]@{}

    foreach ($slot in @('hero', 'about')) {
        $reference = $placements.$slot

        if (-not [string]::IsNullOrWhiteSpace($reference)) {
            $orderedPlacements[$slot] = [string]$reference
        }
    }

    if ($gallery.Count -gt 0) {
        $orderedPlacements['gallery'] = $gallery
    }

    $document = [ordered]@{
        schemaVersion = 1
        templateKey   = $templateKey
    }

    if ($manifest.PSObject.Properties.Name -contains 'assetIdPrefix' -and
        -not [string]::IsNullOrWhiteSpace($manifest.assetIdPrefix)) {
        $document['assetIdPrefix'] = [string]$manifest.assetIdPrefix
    }

    $document['directory'] = [string]$manifest.directory
    $document['assets'] = @($assets)
    $document['placements'] = $orderedPlacements

    if ($unassigned.Count -gt 0) {
        $document['unassigned'] = $unassigned
    }

    $json = Format-JsonValue $document 0

    # Keep the checked-in artifacts byte-stable: LF endings, a single trailing
    # newline and no BOM, so re-running the generator only changes the inventory.
    $json = ($json -replace "`r`n", "`n").TrimEnd("`n") + "`n"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    [System.IO.File]::WriteAllText($manifestFile.FullName, $json, $utf8NoBom)

    Write-Host "Wrote $($assets.Count) assets for '$templateKey' to $($manifestFile.FullName)"
    $written++
}

Write-Host "Processed $written manifest(s)."
