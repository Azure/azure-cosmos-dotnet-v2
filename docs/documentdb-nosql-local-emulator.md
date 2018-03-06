# Use the Azure Cosmos DB Emulator for Development and Testing
The Azure Cosmos DB Emulator provides a local environment that emulates the Azure Cosmos DB service for development purposes. Using the Cosmos DB Emulator, you can develop and test your application locally, without creating an Azure subscription or incurring any costs. When you're satisfied with how your application is working in the Cosmos DB Emulator, you can switch to using an Azure Cosmos DB account in the cloud.

## Cosmos DB Emulator system requirements
The Cosmos DB Emulator has the following hardware and software requirements:

* Software requirements
  * Windows Server 2012 R2, Windows Server 2016, or Windows 10
*	Minimum Hardware requirements
  *	2 GB RAM
  *	10 GB available hard disk space

## Installing the Cosmos DB Emulator
You can download the Cosmos DB Emulator from the [Microsoft Download Center](https://aka.ms/documentdb-emulator). To install, configure, and run the Cosmos DB Emulator, you must have administrative privileges on the computer.

> [!NOTE]
> To install, configure, and run the Cosmos DB Emulator, you must have administrative privileges on the computer.

## Checking for Cosmos DB Emulator updates
The Cosmos DB Emulator includes a built-in Azure Cosmos DB Data Explorer to browse data stored within Cosmos DB, create new collections, and let you know when a new update is available for download. 

> [!NOTE]
> Data created in one version of the Cosmos DB Emulator is not guaranteed to be accessible when using a different version. If you need to persist your data for the long term, it is recommended that you store that data in an Azure Cosmos DB account, rather than in the Cosmos DB Emulator. 

## How the Cosmos DB Emulator works
The Cosmos DB Emulator provides a high-fidelity emulation of the Cosmos DB service. It supports identical functionality as Azure Cosmos DB, including support for creating and querying JSON documents, provisioning and scaling collections, and executing stored procedures and triggers. You can develop and test applications using the Cosmos DB Emulator, and deploy them to Azure at global scale by just making a single configuration change to the connection endpoint for Cosmos DB.

While we created a high-fidelity local emulation of the actual Cosmos DB service, the implementation of the Cosmos DB Emulator is different than that of the service. For example, the Cosmos DB Emulator uses standard OS components such as the local file system for persistence, and HTTPS protocol stack for connectivity. This means that some functionality that relies on Azure infrastructure like global replication, single-digit millisecond latency for reads/writes, and tunable consistency levels are not available via the Cosmos DB Emulator.

## Authenticating requests against the Cosmos DB Emulator
Just as with Azure Document in the cloud, every request that you make against the Cosmos DB Emulator must be authenticated. The Cosmos DB Emulator supports a single fixed account and a well-known authentication key for master key authentication. This account and key are the only credentials permitted for use with the Cosmos DB Emulator. They are:

    Account name: localhost:<port>
    Account key: C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==

> [!NOTE]
> The master key supported by the Cosmos DB Emulator is intended for use only with the emulator. You cannot use your production Cosmos DB account and key with the Cosmos DB Emulator. 

Additionally, just as the Azure Cosmos DB service, the Cosmos DB Emulator supports only secure communication via SSL.

## Start and initialize the Cosmos DB Emulator
To start the Azure Cosmos DB Emulator, select the Start button or press the Windows key. Begin typing **Cosmos DB Emulator**, and select the emulator from the list of applications. When the emulator is running, you'll see an icon in the Windows taskbar notification area.

The Cosmos DB Emulator is installed by default to the `C:\Program Files\Azure DocumentDB Emulator` directory. You can also start and stop the emulator from the command line. Please see below for options for running the emulator from the command line.

## Developing with the Cosmos DB Emulator
Once you have the Cosmos DB Emulator running on your desktop, you can use any supported [Cosmos DB SDK](https://docs.microsoft.com/azure/documentdb/documentdb-sdk-dotnet) or the [Cosmos DB REST API](https://msdn.microsoft.com/library/azure/dn781481.aspx) to interact with the Emulator. The Cosmos DB Emulator also includes a built-in Data Explorer that lets you create collections, view and edit documents without writing any code. 

    // Connect to the Cosmos DB Emulator running locally
    DocumentClient client = new DocumentClient(
        new Uri("https://localhost:8081"), 
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

You can use existing tools like [DocumentDB Studio](https://github.com/mingaliu/DocumentDBStudio) to connect to the Cosmos DB Emulator. You can also migrate data between the Cosmos DB Emulator and the Azure Cosmos DB service using the [DocumentDB Data Migration Tool](https://github.com/azure/azure-documentdb-datamigrationtool).

## Cosmos DB Emulator command line tool reference
From the installation location, you can use the command line to start and stop the emulator, configure options, and perform other operations.

### Command Line Syntax

    DocumentDB.LocalEmulator.exe [/shutdown] [/datapath] [/port] [/mongoport] [/directports] [/key] [/?]

To view the list of options, type `DocumentDB.LocalEmulator.exe /?` at the command prompt.

<table>
<tr>
  <td><strong>Option</strong></td>
  <td><strong>Description</strong></td>
  <td><strong>Command</strong></td>
  <td><strong>Arguments</strong></td>
</tr>
<tr>
  <td>[No arguments]</td>
  <td>Starts up the Cosmos DB Emulator with default settings</td>
  <td>DocumentDB.LocalEmulator.exe</td>
  <td></td>
</tr>
<tr>
  <td>Shutdown</td>
  <td>Shuts down the Cosmos DB Emulator</td>
  <td>DocumentDB.LocalEmulator.exe /Shutdown</td>
  <td></td>
</tr>
<tr>
  <td>Help</td>
  <td>Displays the list of command line arguments</td>
  <td>DocumentDB.LocalEmulator.exe /?</td>
  <td></td>
</tr>
<tr>
  <td>Datapath</td>
  <td>Specifies the path in which to store data files</td>
  <td>DocumentDB.LocalEmulator.exe /datapath=&lt;datapath&gt;</td>
  <td>&lt;datapath&gt;: An accessible path</td>
</tr>
<tr>
  <td>Port</td>
  <td>Specifies the port number to use for the emulator.  Default is 8081</td>
  <td>DocumentDB.LocalEmulator.exe /port=&lt;port&gt;</td>
  <td>&lt;port&gt;: Single port number</td>
</tr>
<tr>
  <td>MongoPort</td>
  <td>Specifies the port number to use for MongoDB compatibility API. Default is 10250</td>
  <td>DocumentDB.LocalEmulator.exe /mongoport=&lt;mongoport&gt;</td>
  <td>&lt;mongoport&gt;: Single port number</td>
</tr>
<tr>
  <td>DirectPorts</td>
  <td>Specifies the ports to use for direct connectivity. Defaults are 10251,10252,10253,10254</td>
  <td>DocumentDB.LocalEmulator.exe /directports:&lt;directports&gt;</td>
  <td>&lt;directports&gt;: Comma delimited list of 4 ports</td>
</tr>
<tr>
  <td>Key</td>
  <td>Authorization key for the emulator. Key must be the base-64 encoding of a 64-byte vector</td>
  <td>DocumentDB.LocalEmulator.exe /key:&lt;key&gt;</td>
  <td>&lt;key&gt;: Key must be the base-64 encoding of a 64-byte vector</td>
</tr>
<tr>
  <td>EnableThrottling</td>
  <td>Specifies that request throttling behavior is enabled</td>
  <td>DocumentDB.LocalEmulator.exe /enablethrottling</td>
  <td></td>
</tr>
<tr>
  <td>DisableThrottling</td>
  <td>Specifies that request throttling behavior is disabled</td>
  <td>DocumentDB.LocalEmulator.exe /disablethrottling</td>
  <td></td>
</tr>
</table>

## Differences between the Cosmos DB Emulator and Azure Cosmos DB 
Because the Cosmos DB Emulator provides an emulated environment running on a local developer workstation, there are some differences in functionality between the emulator and an Azure Cosmos DB account in the cloud:

* The Cosmos DB Emulator supports only a single fixed account and a well-known master key.  Key regeneration is not possible in the Cosmos DB Emulator.
* The Cosmos DB Emulator is not a scalable service and will not support a large number of collections.
* The Cosmos DB Emulator does not simulate different [DocumentDB consistency levels](https://docs.microsoft.com/azure/documentdb/documentdb-consistency-levels).
* The Cosmos DB Emulator does not simulate [multi-region replication](https://docs.microsoft.com/azure/documentdb/documentdb-distribute-data-globally).
* The Cosmos DB Emulator does not support service quota overrides which may be available in the Azure Cosmos DB service (e.g. document size limits, increased partitioned collection storage).
* While the Cosmos DB Emulator will return request charges similar to the Azure Cosmos DB service, the emulator cannot be used to estimate provisioned throughput requirements for applications leveraging the Azure Cosmos DB service. To accurately estimate production throughput needs, use the [Cosmos DB capacity planner](https://www.documentdb.com/capacityplanner).
* While the Cosmos DB Emulator persists data, the emulator cannot be used to estimate data and index storage requirements for applications leveraging the Azure Cosmos DB service. To accurately estimate production storage needs, use the [Cosmos DB capacity planner](https://www.documentdb.com/capacityplanner).


## Next steps
* To learn more about Cosmos DB, see [Introduction to Azure Cosmos DB](https://docs.microsoft.com/azure/documentdb/documentdb-introduction)
* To start developing against the Cosmos DB Emulator, download one of the [supported Cosmos DB SDKs](https://docs.microsoft.com/azure/documentdb/documentdb-sdk-dotnet)
