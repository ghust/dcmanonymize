$githubUrl = $env:GITHUB_URL
$githubApiKey = $env:GITHUB_API_KEY
$nugetUrl = $env:NUGET_URL
$nugetApiKey = $env:NUGET_API_KEY

if(-not $nugetUrl) {
    $nugetUrl = "https://api.nuget.org/v3/index.json";
}
if(-not $githubUrl) {
    $githubUrl = "https://nuget.pkg.github.com/amoerie/index.json";
}

$projectName = "DcmAnonymize"
$projectPath = Resolve-Path (Join-Path $PSScriptRoot "./$projectName/")
$csProjPath = Resolve-Path (Join-Path $projectPath "$projectName.csproj")

[xml]$csproj = Get-Content $csprojPath

$version = $csproj.Project.PropertyGroup.Version

Write-Host "Packing version $version"

dotnet pack $csprojPath --configuration Release

$nupkgFile = Resolve-Path (Join-Path "$projectPath/bin/Release" "$projectName.$version.nupkg")

Write-Host "Publishing NuGet package file"

if($nugetApiKey)
{
    nuget push $nupkgFile -skipduplicate -source $nugetUrl -apikey $nugetApiKey
} 
else 
{
    # API key is presumed to be preconfigured
    nuget push $nupkgFile -skipduplicate -source $nugetUrl
}

if($githubApiKey)
{
    nuget push $nupkgFile -skipduplicate -source $githubUrl -apikey $githubApiKey
}
else
{
    # API key is presumed to be preconfigured
    nuget push $nupkgFile -skipduplicate -source $githubUrl
}