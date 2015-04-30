using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using searchabletodo.Data;
using searchabletodo.Models;
using System.Threading.Tasks;
using System.Net;

namespace searchabletodo.Controllers
{
    public class ItemController : Controller
    {
        public ActionResult Index()
        {
            var items = DocumentDBRepository<Item>.Find(i => !i.Completed);
            return View(items);
        }
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Id,Title,Description,Tags,DueDate,Completed")] Item item)
        {
            if (ModelState.IsValid)
            {
                if (item.Tags == null || item.Tags.Count == 0)
                {
                    item.Tags = new List<string> { "One-time" };
                }

                item.Id = DateTime.UtcNow.Ticks.ToString();
                await DocumentDBRepository<Item>.CreateAsync(item);
                return RedirectToAction("Index");
            }

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

        public ActionResult Details(string id)
        {
            Item item = DocumentDBRepository<Item>.Get(x => x.Id == id);
            return View(item);
        }
    }
}