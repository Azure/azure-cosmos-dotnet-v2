using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using searchabletodo.Data;
using searchabletodo.Models;
using System.Net;

namespace searchabletodo.Controllers
{
    public class SearchController : Controller
    {
        // GET: Search
        public async Task<ActionResult> Index(string q)
        {
            var results = await ItemSearchRepository.SearchAsync(q);

            return View(results);
        }

        public ActionResult Details(string id)
        {
            Item item = DocumentDBRepository<Item>.Get(x => x.Id == id);
            return View(item);
        }

        public ActionResult Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Item item = DocumentDBRepository<Item>.Get(x => x.Id == id);
            if (item == null)
            {
                return HttpNotFound();
            }

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Id,Title,Description,Tags,DueDate,Completed")] Item item)
        {
            if (ModelState.IsValid)
            {
                if (item.Tags == null || item.Tags.Count == 0)
                {
                    item.Tags = new List<string> { "One-time" };
                }
                await DocumentDBRepository<Item>.UpdateAsync(item.Id, item);
                return RedirectToAction("Index");
            }

            return View(item);
        }

        public async Task Reindex()
        {
            await ItemSearchRepository.RunIndexerAsync();
        }

        public async Task<ActionResult> AutoComplete(string prefix)
        {
            return new JsonResult
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = await ItemSearchRepository.SuggestAsync(prefix)
            };
        }
    }
}

//q=lunch