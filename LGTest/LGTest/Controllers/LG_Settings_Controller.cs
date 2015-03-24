using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using LGTest.Models;

namespace LGTest.Controllers
{
    public class LG_Settings_Controller : Controller
    {
        private WonderlandEntities db = new WonderlandEntities();

        //
        // GET: /LG_Settings_/

        public ActionResult Index()
        {
            return View(db.WL_LIST_GUARD_SETTINGS.ToList());
        }

        //
        // GET: /LG_Settings_/Details/5

        public ActionResult Details(int id = 0)
        {
            WL_LIST_GUARD_SETTINGS wl_list_guard_settings = db.WL_LIST_GUARD_SETTINGS.Find(id);
            if (wl_list_guard_settings == null)
            {
                return HttpNotFound();
            }
            return View(wl_list_guard_settings);
        }

        //
        // GET: /LG_Settings_/Create

        public ActionResult Create()
        {
            return View();
        }

        //
        // POST: /LG_Settings_/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(WL_LIST_GUARD_SETTINGS wl_list_guard_settings)
        {
            if (ModelState.IsValid)
            {
                db.WL_LIST_GUARD_SETTINGS.Add(wl_list_guard_settings);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(wl_list_guard_settings);
        }

        //
        // GET: /LG_Settings_/Edit/5

        public ActionResult Edit(int id = 0)
        {
            WL_LIST_GUARD_SETTINGS wl_list_guard_settings = db.WL_LIST_GUARD_SETTINGS.Find(id);
            if (wl_list_guard_settings == null)
            {
                return HttpNotFound();
            }
            return View(wl_list_guard_settings);
        }

        //
        // POST: /LG_Settings_/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(WL_LIST_GUARD_SETTINGS wl_list_guard_settings)
        {
            if (ModelState.IsValid)
            {
                db.Entry(wl_list_guard_settings).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(wl_list_guard_settings);
        }

        //
        // GET: /LG_Settings_/Delete/5

        public ActionResult Delete(int id = 0)
        {
            WL_LIST_GUARD_SETTINGS wl_list_guard_settings = db.WL_LIST_GUARD_SETTINGS.Find(id);
            if (wl_list_guard_settings == null)
            {
                return HttpNotFound();
            }
            return View(wl_list_guard_settings);
        }

        //
        // POST: /LG_Settings_/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            WL_LIST_GUARD_SETTINGS wl_list_guard_settings = db.WL_LIST_GUARD_SETTINGS.Find(id);
            db.WL_LIST_GUARD_SETTINGS.Remove(wl_list_guard_settings);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }
    }
}