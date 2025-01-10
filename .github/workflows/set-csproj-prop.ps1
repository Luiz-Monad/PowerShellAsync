param ( [string] $key, [string] $value, [string] $csProjPath )
$xml = [xml](Get-Content $csProjPath)
$propertyGroup = $xml.Project.PropertyGroup | Select-Object -First 1
if (-not $propertyGroup.SelectSingleNode($key)) {
    $commitElement = $xml.CreateElement($key)
    $propertyGroup.AppendChild($commitElement)
} else {
    $commitElement = $propertyGroup.SelectSingleNode($key)
}
$commitElement.RemoveAll()
$textNode = $xml.CreateTextNode($value)
$commitElement.AppendChild($textNode)
$xml.Save($csProjPath)
