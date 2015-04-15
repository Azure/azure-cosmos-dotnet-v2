This source code contains 1 ASP.NET MVC web application.

On start-up the application will initialize and create a DocumentDB Database & Collection based on configuration. It will also create an Azure Search Datasource pointing to the DocumentDB Database & Collection and then configure an indexer (crawler) to index the contents of the DocuemntDB account.

webdeploy-pkg.zip - is the searchabletodo app packaged up, by Visual Studio, as a Web Deploy Package. The ARM template uses this to publish the web application. 

**NB:** if you update the source code in /src/ you will need to repackage the application as a Web Deploy Package and update this zip file. The ARM template looks specifically for this zip file. 
