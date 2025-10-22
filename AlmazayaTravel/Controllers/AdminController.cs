using Microsoft.AspNetCore.Mvc;
using AlmazayaTravel.Data;
using AlmazayaTravel.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace AlmazayaTravel.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, ILogger<AdminController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // --- Authentication Actions (Remain the same) ---
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (User.Identity?.IsAuthenticated ?? false) { return RedirectToAction(nameof(Index)); }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            // !!! Replace with secure authentication !!!
            if (username == "admin" && password == "password123")
            {
                var claims = new List<Claim> { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Administrator"), };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = true };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                _logger.LogInformation("User '{Username}' logged in successfully.", username);
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                else return RedirectToAction(nameof(Index));
            }
            _logger.LogWarning("Login failed for user '{Username}'.", username);
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Index", "Home");
        }

        // --- Trip Package Management (Updated Bind) ---

        // GET: Admin/ or Admin/Index
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Fetching trip packages for admin index.");
            var packages = await _context.TripPackages.OrderByDescending(p => p.Id).ToListAsync();
            return View("PackagesIndex", packages); // Assumes view is still PackagesIndex.cshtml
        }

        // GET: Admin/PackageCreate
        public IActionResult PackageCreate()
        {
            return View();
        }

        // POST: Admin/PackageCreate (Updated Bind)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PackageCreate(
            [Bind("Name,Description,DestinationCountry,NameAr,DescriptionAr,DestinationCountryAr,DurationDays,PriceBeforeDiscount,PriceAfterDiscount,IsActive,ImageFile")] // Added Ar fields
            TripPackage tripPackage)
        {
            _logger.LogInformation("Attempting to create trip package: {PackageName}", tripPackage.Name);
            ModelState.Remove("Id");
            ModelState.Remove("ImageUrl");
            ModelState.Remove("Bookings");
            ModelState.Remove("RowVersion");

            if (tripPackage.ImageFile != null)
            {
                string? uploadedFilePath = await UploadPackageImage(tripPackage.ImageFile);
                if (uploadedFilePath == null)
                {
                    ModelState.AddModelError("ImageFile", "Error uploading image.");
                    _logger.LogWarning("Image upload failed during package creation for {PackageName}.", tripPackage.Name);
                    return View(tripPackage);
                }
                tripPackage.ImageUrl = uploadedFilePath;
            }

            if (ModelState.IsValid)
            {
                _context.Add(tripPackage);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Trip Package created successfully!";
                _logger.LogInformation("Trip package '{PackageName}' created successfully with ID {PackageId}.", tripPackage.Name, tripPackage.Id);
                return RedirectToAction(nameof(Index));
            }
            _logger.LogWarning("Model state invalid during package creation for {PackageName}.", tripPackage.Name);
            return View(tripPackage);
        }

        // GET: Admin/PackageEdit/5
        public async Task<IActionResult> PackageEdit(int? id)
        {
            if (id == null) return NotFound();
            var tripPackage = await _context.TripPackages.FindAsync(id);
            if (tripPackage == null) return NotFound();
            return View(tripPackage);
        }

        // POST: Admin/PackageEdit/5 (Updated Bind)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PackageEdit(int id,
             [Bind("Id,Name,Description,DestinationCountry,NameAr,DescriptionAr,DestinationCountryAr,DurationDays,PriceBeforeDiscount,PriceAfterDiscount,IsActive,ImageUrl,ImageFile,RowVersion")] // Added Ar fields
             TripPackage tripPackage)
        {
            _logger.LogInformation("Attempting to edit trip package ID: {PackageId}", id);
            if (id != tripPackage.Id) return NotFound();

            ModelState.Remove("Bookings");

            var packageToUpdate = await _context.TripPackages.FindAsync(id);
            if (packageToUpdate == null) return NotFound();

            _context.Entry(packageToUpdate).Property("RowVersion").OriginalValue = tripPackage.RowVersion;

            if (tripPackage.ImageFile != null)
            {
                if (!string.IsNullOrEmpty(packageToUpdate.ImageUrl))
                {
                    DeletePackageImage(packageToUpdate.ImageUrl);
                }
                string? uploadedFilePath = await UploadPackageImage(tripPackage.ImageFile);
                if (uploadedFilePath == null)
                {
                    ModelState.AddModelError("ImageFile", "Error uploading new image.");
                    _logger.LogWarning("Image upload failed during package edit for ID {PackageId}.", id);
                    tripPackage.ImageUrl = packageToUpdate.ImageUrl;
                    return View(tripPackage);
                }
                packageToUpdate.ImageUrl = uploadedFilePath;
            }
            else
            {
                // Ensure ImageUrl is preserved if no new file uploaded but model binding might clear it
                packageToUpdate.ImageUrl = tripPackage.ImageUrl;
            }

            // Update TryUpdateModelAsync to include Ar fields
            if (await TryUpdateModelAsync<TripPackage>(packageToUpdate, "",
                p => p.Name, p => p.Description, p => p.DestinationCountry,
                p => p.NameAr, p => p.DescriptionAr, p => p.DestinationCountryAr, // Added Ar fields
                p => p.DurationDays, p => p.PriceBeforeDiscount, p => p.PriceAfterDiscount,
                p => p.IsActive, p => p.ImageUrl)) // ImageUrl updated separately above
            {
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Trip Package updated successfully!";
                    _logger.LogInformation("Trip package ID {PackageId} updated successfully.", id);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict editing trip package ID {PackageId}.", id);
                    var entry = ex.Entries.Single();
                    var clientValues = (TripPackage)entry.Entity;
                    var databaseEntry = entry.GetDatabaseValues();
                    if (databaseEntry == null) { ModelState.AddModelError(string.Empty, "Package deleted by another user."); }
                    else
                    {
                        var databaseValues = (TripPackage)databaseEntry.ToObject();
                        ModelState.AddModelError(string.Empty, "Record modified by another user...");
                        tripPackage.RowVersion = databaseValues.RowVersion;
                        ModelState.Remove("RowVersion");
                    }
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error saving trip package ID {PackageId}.", id);
                    ModelState.AddModelError("", "Unable to save changes...");
                }
            }
            else
            {
                _logger.LogWarning("Model state invalid during package edit for ID {PackageId}.", id);
                if (tripPackage.ImageFile == null)
                    // Re-assign ImageUrl in case TryUpdateModelAsync failed AFTER we potentially set it
                    packageToUpdate.ImageUrl = tripPackage.ImageUrl;
                // Return the view with the model containing validation errors
                return View(tripPackage); // Important: return the model passed in, which has errors
            }
            // If TryUpdateModelAsync failed, need to return view with the original entity + errors
            // Restore ImageUrl in case TryUpdateModel cleared it and no new image was uploaded
            if (tripPackage.ImageFile == null) tripPackage.ImageUrl = packageToUpdate.ImageUrl;
            return View(tripPackage); // Return the model that failed validation
        }


        // --- Delete Actions (Remain the same logic) ---
        // GET: Admin/PackageDelete/5
        public async Task<IActionResult> PackageDelete(int? id, bool? concurrencyError)
        {
            if (id == null) return NotFound();
            var tripPackage = await _context.TripPackages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (tripPackage == null)
            {
                if (concurrencyError.GetValueOrDefault()) { return RedirectToAction(nameof(Index)); }
                return NotFound();
            }
            if (concurrencyError.GetValueOrDefault()) { ViewData["ConcurrencyErrorMessage"] = "Record modified..."; }
            return View(tripPackage); // Requires PackageDelete.cshtml view
        }

        // POST: Admin/PackageDeleteConfirmed/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PackageDeleteConfirmed(int id)
        {
            _logger.LogInformation("Attempting to delete trip package ID: {PackageId}", id);
            var tripPackage = await _context.TripPackages.Include(p => p.Bookings).FirstOrDefaultAsync(p => p.Id == id);
            if (tripPackage == null)
            {
                TempData["ErrorMessage"] = "Package not found.";
                _logger.LogWarning("Trip package ID {PackageId} not found for deletion.", id);
                return RedirectToAction(nameof(Index));
            }
            if (tripPackage.Bookings != null && tripPackage.Bookings.Any())
            {
                TempData["ErrorMessage"] = "Cannot delete package with existing bookings.";
                _logger.LogWarning("Attempted delete package ID {PackageId} with bookings.", id);
                return RedirectToAction(nameof(Index));
            }
            try
            {
                if (!string.IsNullOrEmpty(tripPackage.ImageUrl)) { DeletePackageImage(tripPackage.ImageUrl); }
                _context.TripPackages.Remove(tripPackage);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Trip Package deleted successfully!";
                _logger.LogInformation("Trip package ID {PackageId} deleted successfully.", id);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict deleting package ID {PackageId}.", id);
                return RedirectToAction(nameof(PackageDelete), new { id = id, concurrencyError = true });
            }
            catch (DbUpdateException ex)
            {
                TempData["ErrorMessage"] = $"Error deleting package: {ex.Message}";
                _logger.LogError(ex, "Database error deleting package ID {PackageId}.", id);
                return RedirectToAction(nameof(Index));
            }
        }


        // --- View Customer Bookings Actions (Remain the same) ---
        public async Task<IActionResult> BookingsIndex()
        {
            _logger.LogInformation("Fetching customer bookings for admin.");
            var bookings = await _context.Bookings.Include(b => b.TripPackage).OrderByDescending(b => b.BookingDate).ToListAsync();
            return View(bookings);
        }
        public async Task<IActionResult> BookingDetails(int? id)
        {
            if (id == null) return NotFound();
            var booking = await _context.Bookings.Include(b => b.TripPackage).FirstOrDefaultAsync(m => m.Id == id);
            if (booking == null) return NotFound();
            return View(booking); // Requires BookingDetails.cshtml view
        }


        // --- Helper Methods (Remain the same) ---
        private async Task<string?> UploadPackageImage(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return null;
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "packages");
            if (!Directory.Exists(uploadsFolder)) { Directory.CreateDirectory(uploadsFolder); }
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await imageFile.CopyToAsync(fileStream); }
                _logger.LogInformation("Image file '{FileName}' uploaded to {FilePath}", imageFile.FileName, filePath);
                return "/images/packages/" + uniqueFileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image file '{FileName}'", imageFile.FileName);
                return null;
            }
        }
        private void DeletePackageImage(string relativeImagePath)
        {
            if (string.IsNullOrEmpty(relativeImagePath)) return;
            string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, relativeImagePath.TrimStart('/', '\\'));
            try
            {
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    _logger.LogInformation("Deleted image file: {FilePath}", fullPath);
                }
                else { _logger.LogWarning("Image file not found for deletion: {FilePath}", fullPath); }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error deleting image file {FilePath}", fullPath); }
        }
        private bool TripPackageExists(int id) => _context.TripPackages.Any(e => e.Id == id);
    }
}

