This example uses an Azure Resource Management (ARM) template to - 

- Provision an Azure DocumentDB Account
- Provision an Azure Search Account
- Provision an Azure Web Application
- Take an application, packaged as a web deploy package and deploy it to the newly created web application
- Use application settings in the web application to store configuration values

##<a id="Prerequisites"></a>Prerequisites ##
While this sample does not assume prior experience with Azure Resource Manager templates, JSON, or Azure PowerShell, should you wish to modify the referenced templates or deployment options, then knowledge of each of these areas will be required.

Before following these instructions, ensure that you have the following:

- An Azure subscription. Azure is a subscription-based platform.  For more information about obtaining a subscription, see [Purchase Options](http://azure.microsoft.com/pricing/purchase-options/), [Member Offers](http://azure.microsoft.com/pricing/member-offers/), or [Free Trial](http://azure.microsoft.com/pricing/free-trial/).
- An Azure Storage Account. For instructions, see [About Azure Storage Accounts](../storage-whatis-account/).
- A workstation with Azure PowerShell (minimum release date 3/31/2015). For instructions, see [Install and configure Azure PowerShell](http://azure.microsoft.com/documentation/articles/install-configure-powershell/).
- Note that depending on the security settings of your computer, you may need to unblock the CreateDocDBSearchWebsiteTodo.ps1 files by right-clicking the file, clicking **Properties**, and clicking **Unblock**.

##<a id="Deploy"></a>Deploy the template ##
1. Open Microsoft Azure PowerShell and navigate to the folder where the CreateDocDBSearchWebsiteTodo.ps1 and associated JSON files reside.


2. We're going to run the CreateDocDBSearchWebsiteTodo.ps1 PowerShell script.  The script takes the following parameters:
	- WebsiteName (mandatory): Specifies the Website name and is used to construct the URL that you will use to access the Website (e.g. if you specify "mydemodocdbwebsite", then the URL by which you will access the Website will be mydemodocdbwebsite.azurewebsites.net).

	- ResourceGroupName (mandatory): Specifies the name of the Azure Resource Group to deploy. If the specified Resource Group doesn't exist, it will be created.

	- docDBAccountName (mandatory): Specifies the name of the DocumentDB account to create.

	- searchAccountName (mandatory): Specifies the name of the Azure Search account to create.

	- location (mandatory): Specifies the Azure location in which to create the DocumentDB and Website resources.  Valid values are East Asia, Southeast Asia, East US, West US, North Europe, West Europe (note that the location value provided is case sensitive).
 
	- searchSku (optional): Specifies the SKU of the Azure Search account to create.  Valid values are free and standard (note that the value is case sensitive).  The default value is free.



3. Here is an example command to run the script:

    	PS C:\DocumentDBTemplates\CreateDocDBWebsiteTodo> .\CreateDocDBSearchWebsiteTodo.ps1 -WebSiteName "mydemosite" -ResourceGroupName "mydemorg" -docDBAccountName "mydemodocdb" -searchAccountName "mydemosearch" -searchSku "standard" -location "West US"

4. And here is an example of the resulting output:

		VERBOSE: 6:42:01 AM - Created resource group 'mydemorg' in location 'westus'
		VERBOSE: 6:42:02 AM - Template is valid.
		VERBOSE: 6:42:02 AM - Create template deployment 'Microsoft.DocDBSearchTodo'.
		VERBOSE: 6:42:08 AM - Resource Microsoft.Web/serverFarms 'mydemosite' provisioning status is succeeded
		VERBOSE: 6:42:12 AM - Resource Microsoft.DocumentDb/databaseAccounts 'mydemodocdb' provisioning status is running
		VERBOSE: 6:42:15 AM - Resource Microsoft.Web/Sites 'mydemosite' provisioning status is succeeded
		VERBOSE: 6:42:28 AM - Resource Microsoft.Search/searchServices 'mydemosearch' provisioning status is running
		VERBOSE: 6:48:52 AM - Resource Microsoft.DocumentDb/databaseAccounts 'mydemodocdb' provisioning status is succeeded
		VERBOSE: 6:48:52 AM - Resource Microsoft.DocumentDb/databaseAccounts 'mydemodocdb' provisioning status is succeeded
		VERBOSE: 7:03:45 AM - Resource Microsoft.Search/searchServices 'mydemosearch' provisioning status is succeeded
		VERBOSE: 7:03:47 AM - Resource Microsoft.Search/searchServices 'mydemosearch' provisioning status is succeeded
		VERBOSE: 7:03:52 AM - Resource Microsoft.Web/Sites/config 'mydemosite/web' provisioning status is succeeded
		VERBOSE: 7:03:52 AM - Resource Microsoft.Web/Sites/Extensions 'mydemosite/MSDeploy' provisioning status is running
		VERBOSE: 7:04:03 AM - Resource Microsoft.Web/Sites/Extensions 'mydemosite/MSDeploy' provisioning status is succeeded

		ResourceGroupName : mydemorg
		Location          : westus
		Resources         : {mydemodocdb, mydemosearch, mydemosite, mydemosite}
		ResourcesTable    :
                    Name           Type                                   Location
                    =============  =====================================  ========
                    mydemodocdb    Microsoft.DocumentDb/databaseAccounts  westus
                    mydemosearch  Microsoft.Search/searchServices        westus
                    mydemosite    Microsoft.Web/serverFarms              westus
                    mydemosite    Microsoft.Web/sites                    westus

		ProvisioningState : Succeeded

6. In order to use the application, simply navigate to the Website URL (in the example above, the URL would be http://mydemosite.azurewebsites.net).

##<a id="Deploy"></a>Import sample data ##
This sample comes with some sample data if you would like to see your database with some todo items to test the search indexer without first having create a stack of your own items. 

1. Navigate to the [sample data](../data/items.zip).
2. Extract the archive to a local directory on your computer.
3. Download the [DocumentDB Data Migration Tool](http://www.microsoft.com/en-us/download/details.aspx?id=46436).
4. Using the migration tool (dtui.exe for the GUI tool, or dt.exe for the command line version), select Import from "JSON Files", and add the folder where you extracted the sample data.
5. From the Azure Management Portal, copy the DocumentDB Account connection string for the account that was created and append ";Database=todo" (or whatever database you have chosen to use).
6. Set the Collection name to "items" (or whatever collection you have configured your application to use).
7. Import the data.

For more detailed instructions on using the DocumentDB Data Migration Tool please refer to [Import data to DocumentDB](http://azure.microsoft.com/en-us/documentation/articles/documentdb-import-data/) 