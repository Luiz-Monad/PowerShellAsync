param ( [string] $commitHash, [string] $csprojPath )
$xml = [xml](Get-Content $csprojPath)
$propertyGroup = $xml.Project.PropertyGroup | Select-Object -First 1
if (-not $propertyGroup.SelectSingleNode('RepositoryCommit')) {
    $commitElement = $xml.CreateElement('RepositoryCommit')
    $propertyGroup.AppendChild($commitElement)
} else {
    $commitElement = $propertyGroup.SelectSingleNode('RepositoryCommit')
}
$commitElement.RemoveAll()
$textNode = $xml.CreateTextNode($commitHash)
$commitElement.AppendChild($textNode)
$xml.Save($csprojPath)
