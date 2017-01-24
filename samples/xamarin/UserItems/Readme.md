This sample illustrates how to use DocumentDB built-in authorization engine to implement per-user data scenario for a Xamarin mobile app.
The sample is a simple ToDo list app with support for logging in users using Facebook Auth and managing user's to do list.   

## Overview
This sample consists of:
- Xamarin app that allows users to login and manage their todo lists. The app stores todo lists in a DocumentDB partitioned collection UserItems.
- Resource Token Broker API for brokering resource tokens to the logged in users of the app

The authentication and data flow is illustrated in the diagram below.
- DocumentDB collection is created with partition key '/userid'. Specifying partition key for collection allows DocumentDB to scale infinitely as the number of users and lists grows.
- Xamarin app allows users to login with Facebook credentials.
- Xamarin app uses Facebook access token to authenticate with ResourceTokenBroker API, a simple Web API that authenticates logged in Facebook user, and requests from DocumentDB short lived resource tokens for the user, with access limited to documents within that user's partition. 
Once the app receives back the resource token, it accesses users documents in UserItems collection.


![Diagram](tokenbroker.png)
## How to Run this sample?

- Step 1. In Azure portal create a DocumentDB account and a new collection UserItems specifying partition key /userid. Navigate to Keys tab.
- Step 2. In Azure portal create an App Service website to host Resource Token Broker API.
- Step 3. Open web.config file in ResourceTokenBroker project and fill in the values for DocumentDB account URL, DocumentDB secret, collection name, database name, as well as hostURL which is the base https url of the created website.
- Step 4. Publish ResourceTokenBroker solution to your created website.
- Step 5. Open Xamarin project, and navigate to TodoItemManager.cs. Fill in the values for accountURL, collectionId, databaseId, as well as resourceTokenBrokerURL as the base https url for the resource token broker website.
- Step 6. Follow [this tutorial](https://docs.microsoft.com/en-us/azure/app-service-mobile/app-service-mobile-how-to-configure-facebook-authentication) to setup Facebook authentication and configure ResourceTokenBroker website.
- Step 7. Run the Xamarin app.
