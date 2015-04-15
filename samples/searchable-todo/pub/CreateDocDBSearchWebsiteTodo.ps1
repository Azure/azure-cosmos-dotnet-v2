Param([string]$WebSiteName,[string]$ResourceGroupName,[string]$docDBAccountName,[string]$searchAccountName,[string]$searchSku,[string]$location)

Set-StrictMode -Version 3
#Switch Azure Powershell Mode
Switch-AzureMode AzureResourceManager
Add-AzureAccount
# Create or update the resource group using our template file and template parameters
New-AzureResourceGroup -Name $resourceGroupName -Location $location -DeploymentName "Microsoft.DocDBSearchTodo" -Force -Verbose -TemplateFile .\DocDBSearchWebsiteTodo.json -siteName $WebSiteName -siteLocation $location -databaseAccountName $docDBAccountName -searchAccountName $searchAccountName -locationFromTemplate $location -TemplateParameterFile .\DocDBSearchWebsiteTodo.param.dev.json -hostingPlanName $WebSiteName -searchSku $searchSku
