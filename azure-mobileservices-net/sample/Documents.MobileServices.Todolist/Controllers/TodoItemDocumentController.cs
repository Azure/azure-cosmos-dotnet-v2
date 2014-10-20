
using Documents.MobileServices.Todolist.DataObjects;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.MobileServices;
using Microsoft.WindowsAzure.Mobile.Service;
using Microsoft.WindowsAzure.Mobile.Service.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData;

namespace Documents.MobileServices.Controllers
{
    public class TodoItemDocumentController : DocumentController<DocumentTodoItem>
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            DomainManager = new DocumentEntityDomainManager<DocumentTodoItem>("AMSDocumentDB", "todolist", Request, Services);
        }

        public IQueryable<DocumentTodoItem> GetAllTodoItems()
        {
            return Query();
        }

        public SingleResult<DocumentTodoItem> GetTodoItem(string id)
        {
            return Lookup(id);
        }

        public Task<DocumentTodoItem> ReplaceTodoItem(string id, DocumentTodoItem item)
        {
            return ReplaceAsync(id, item);
        }

        public async Task<IHttpActionResult> PostTodoItem(DocumentTodoItem item)
        {
            var doc = await InsertAsync(item);

            return CreatedAtRoute("DefaultApis", new { id = doc.Id }, doc);
        }

        public Task DeleteTodoItem(string id)
        {
            return DeleteAsync(id);
        }
    }
}