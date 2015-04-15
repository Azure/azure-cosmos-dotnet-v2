
##<a id="Deploy"></a>Import sample data ##
This sample comes with some sample data if you would like to see your database with some todo items to test the search indexer without first having create a stack of your own items. 

2. Extract the archive to a local directory on your computer.
3. Download the [DocumentDB Data Migration Tool](http://www.microsoft.com/en-us/download/details.aspx?id=46436).
4. Using the migration tool (dtui.exe for the GUI tool, or dt.exe for the command line version), select Import from "JSON Files", and add the folder where you extracted the sample data.
5. From the Azure Management Portal, copy the DocumentDB Account connection string for the account that was created and append ";Database=todo" (or whatever database you have chosen to use).
6. Set the Collection name to "items" (or whatever collection you have configured your application to use).
7. Import the data.

For more detailed instructions on using the DocumentDB Data Migration Tool please refer to [Import data to DocumentDB](http://azure.microsoft.com/en-us/documentation/articles/documentdb-import-data/) 