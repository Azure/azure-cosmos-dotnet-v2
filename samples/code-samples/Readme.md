##<a id="Introduction"></a>Introduction ##
These samples demonstrate how to use the .NET Client SDK to interact with the [Azure DocumentDB](http://azure.microsoft.com/services/documentdb)  service

##<a id="Building"></a>Building the sample ##

Open in solution Visual Studio, hit build the solution and enjoy ...

The solution has been configured to restore missing NuGet packages, so it should "just work", but in case it does not, you will need to manually install the DocumentDB NuGet package. To do this, open the NuGet package console on the solution and select the "Restore" button top left.

If the button is not there, then just manually search for the package (DocumentDB) and install it.

Before you can run any of the samples you do need an active Azure DocumentDB account. 
So head over to [Azure](http://portal.azure.com) and sign-up for your account.

##<a id="Description"></a>Description ##

Azure DocumentDB is a fully managed, scalable, queryable, schema free JSON document database service built for modern applications and delivered to you by Microsoft.

These samples demonstrate how to use the Client SDKs to interact with the service.

- **CollectionManagement** - CRUD operations on DocumentCollection resources

- **DatabaseManagent** - CRUD operations on Database resources

- **DocumentManagement** - CRUD operations on Document resources

- **IndexManagement** - shows samples on how to custimize the Indexing Policy for a Collection should you need to.

- **Partitioning** - Samples for common partitioning scenarios using the .NET SDK 

- **Queries** - Samples on how to query for Documents in DocumentDB showing LINQ and SQL

- **ServerSideScripts** - Samples on how to create and execute Stored Procedures, Triggers and User Defined Functions.

- **UserManagement** - CRUD operations on User and Permission resources

- **Spatial** - How to work with GeoJSON and DocumentDB geospatial capabilities

After walking through these samples you should have a good idea of how to get going and how to make user of the various Azure DocumentDB APIs. 

There are step-by-step tutorials and more documentation on the [DocumentDB domentation](http://azure.microsoft.com/en-us/documentation/services/documentdb/) page so head on over, sign-up, and learn about this cool new NoSQL document database.

 
##<a id="More"></a>More information ##

For more information please refer to the [Azure DocumentDB](http://azure.microsoft.com/services/documentdb) service page.
