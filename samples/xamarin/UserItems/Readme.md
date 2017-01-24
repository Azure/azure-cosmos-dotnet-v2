This contains a TodoList sample with support for logging in users and creating per-user lists.  

!Overview

!How to Run this sample?

Step 1. In Azure portal create a DocumentDB account and a new collection UserItems specifying partition key /userid. Navigate to Keys tab.
Step 2. In Azure portal create an App Service website to host Resource Token Broker API.
Step 3. Open web.config file in ResourceTokenBroker project and fill in the values for DocumentDB account URL, DocumentDB secret, collection name, database name, as well as hostURL which is the base https url of the created website.
Step 4. Publish ResourceTokenBroker solution to your created website.
Step 5. Open Xamarin project, and navigate to TodoItemManager.cs. Fill in the values for accountURL, collectionId, databaseId, as well as resourceTokenBrokerURL as the base https url for the resource token broker website.
Step 6. Follow [this tutorial](https://docs.microsoft.com/en-us/azure/app-service-mobile/app-service-mobile-how-to-configure-facebook-authentication) to setup Facebook authentication and configure ResourceTokenBroker website.
Step 7. Run the Xamarin app.
