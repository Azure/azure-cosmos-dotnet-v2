using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Todo.NET.Models;
using repo = todo.net.DocumentDBRepository;

namespace Todo.NET.Controllers
{
    public class ItemController : Controller
    {
        public ActionResult Index()
        {
            var items = repo.GetIncompleteItems();
            return View(items);
        }
        
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Create(Item item)
        {
            if (ModelState.IsValid)
            {
                await repo.CreateDocument(item);
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

            Item item = repo.GetDocument(id);
            if (item == null)
            {
                return HttpNotFound();
            }

            return View(item);
        }

        [HttpPost]
        public async Task<ActionResult> Edit(Item item)
        {
            if (ModelState.IsValid)
            {
                await repo.UpdateDocument(item);
                return RedirectToAction("Index");
            }

            return View(item);
        }
    }
}