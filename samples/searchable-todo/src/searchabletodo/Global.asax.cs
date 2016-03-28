using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using searchabletodo.Data;
using searchabletodo.Models;
using Microsoft.Azure.Documents;

namespace searchabletodo
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            
            // have to init DB & Collection here, else when Search tries to init it will fail because DocumentDB resources aren't there
            Database db = DocumentDBRepository<Item>.GetOrCreateDatabase(DocumentDBRepository<Item>.DatabaseId);
            DocumentCollection col = DocumentDBRepository<Item>.GetOrCreateCollection(db.SelfLink, DocumentDBRepository<Item>.CollectionId);

            // uncomment to force reset and reload
            // ItemSearchRepository.DeleteAll().Wait();
            ItemSearchRepository.SetupSearchAsync().Wait();
            ItemSearchRepository.RunIndexerAsync().Wait();
        }
    }
}
