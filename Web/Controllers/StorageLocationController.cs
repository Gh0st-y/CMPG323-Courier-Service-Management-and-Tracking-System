using System;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Web.Controllers
{
    /// <summary>T24: storage locations lookup and admin create.</summary>
    // TODO: add [Authorize] once login (FR-01) lands.
    public class StorageLocationsController : Controller
    {
        private readonly IStorageLocationRepository _locations;

        public StorageLocationsController()
            : this(new StorageLocationRepository(new SqlConnectionFactory()))
        {
        }

        public StorageLocationsController(IStorageLocationRepository locations)
        {
            _locations = locations;
        }

        [HttpGet]
        [Route("api/storage-locations")]
        public ActionResult GetActive()
        {
            try
            {
                var items = _locations.GetAll(activeOnly: true)
                    .Select(l => new
                    {
                        storageLocationId = l.StorageLocationId,
                        code = l.Code,
                        description = l.Description
                    })
                    .ToList();

                return Json(items, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return ErrorJson(500, "ServerError", "Something went wrong while loading storage locations. Please try again.");
            }
        }

        public class CreateStorageLocationRequest
        {
            public string Code { get; set; }
            public string Description { get; set; }
        }

        [HttpPost]
        [Route("api/storage-locations")]
        // TODO: restrict to SystemAdmin once login/roles (FR-01, SR-02) land.
        public ActionResult Create(CreateStorageLocationRequest body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Code))
                return ErrorJson(400, "ValidationError", "Code is required.");

            if (body.Code.Trim().Length > 50)
                return ErrorJson(400, "ValidationError", "Code must be 50 characters or fewer.");

            var location = new StorageLocation
            {
                Code = body.Code.Trim(),
                Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
                IsActive = true
            };

            try
            {
                var newId = _locations.Insert(location);
                var created = _locations.GetById(newId);

                Response.StatusCode = 201;
                return Json(new
                {
                    storageLocationId = created.StorageLocationId,
                    code = created.Code,
                    description = created.Description
                }, JsonRequestBehavior.AllowGet);
            }
            catch (System.Data.SqlClient.SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                // Unique constraint on Code (schema.sql: Code NVARCHAR(50) NOT NULL UNIQUE)
                return ErrorJson(409, "DuplicateCode", "A storage location with that code already exists.");
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return ErrorJson(500, "ServerError", "Something went wrong while adding the location. Please try again.");
            }
        }

        private ActionResult ErrorJson(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } }, JsonRequestBehavior.AllowGet);
        }
    }
}