using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData;
using Microsoft.WindowsAzure.Mobile.Service;
using Documents.MobileServices.Todolist.DataObjects;

namespace Documents.MobileServices.Todolist.Controllers
{
    public class TodoItemController : TableController<DocumentTodoItem>
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);            
            DomainManager = new DocumentDBDomainManager<DocumentTodoItem>(Request, Services);
        }

        
        public IQueryable<DocumentTodoItem> GetAllTodoItems()
        {
            return Query();
        }

        public SingleResult<DocumentTodoItem> GetTodoItem(string id)
        {
            return Lookup(id);
        }

        public Task<DocumentTodoItem> PatchTodoItem(string id, Delta<DocumentTodoItem> patch)
        {
            return UpdateAsync(id, patch);
        }

        public async Task<IHttpActionResult> PostTodoItem(DocumentTodoItem item)
        {
            DocumentTodoItem current = await InsertAsync(item);
            return CreatedAtRoute("Tables", new { id = current.Id }, current);
        }

        public Task DeleteTodoItem(string id)
        {
            return DeleteAsync(id);
        }
    }
}