using AlmazayaTravel.Data;
using AlmazayaTravel.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AlmazayaTravel.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Fetching active packages for Index page.");
            var activePackages = await _context.TripPackages
                                               .Where(p => p.IsActive)
                                               .OrderBy(p => p.DestinationCountry)
                                               .ToListAsync();
            return View(activePackages);
        }

        public async Task<IActionResult> PackageDetails(int? id)
        {
            _logger.LogInformation("Fetching details for Package ID: {PackageId}", id);
            if (id == null)
            {
                _logger.LogWarning("PackageDetails requested with null ID.");
                return NotFound();
            }
            var tripPackage = await _context.TripPackages
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (tripPackage == null)
            {
                _logger.LogWarning("Package ID {PackageId} not found or inactive.", id);
                return NotFound();
            }
            return View(tripPackage);
        }

        public async Task<IActionResult> Book(int? id)
        {
            _logger.LogInformation("Displaying booking form for Package ID: {PackageId}", id);
            if (id == null)
            {
                _logger.LogWarning("Book action requested with null ID.");
                return NotFound();
            }
            var tripPackage = await _context.TripPackages.FindAsync(id);
            if (tripPackage == null || !tripPackage.IsActive)
            {
                _logger.LogWarning("Package ID {PackageId} not found or inactive for booking.", id);
                TempData["ErrorMessage"] = "The selected package is no longer available.";
                return RedirectToAction(nameof(Index));
            }
            var bookingModel = new Booking
            {
                TripPackageId = tripPackage.Id,
                TripPackage = tripPackage
            };
            return View(bookingModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book([Bind("TripPackageId,ClientName,PhoneNumber,Email,Adults,Children")] Booking booking)
        {
            _logger.LogInformation("Processing booking form submission for Package ID: {PackageId}", booking.TripPackageId);
            ModelState.Remove("TripPackage");
            ModelState.Remove("PaymentStatus");
            ModelState.Remove("PaymentTransactionId");
            ModelState.Remove("AmountPaid");
            ModelState.Remove("BookingDate");
            ModelState.Remove("RowVersion");
            ModelState.Remove("Id");

            var tripPackage = await _context.TripPackages.AsNoTracking().FirstOrDefaultAsync(tp => tp.Id == booking.TripPackageId);

            if (tripPackage == null || !tripPackage.IsActive)
            {
                _logger.LogWarning("Selected Package ID {PackageId} is not available during booking POST.", booking.TripPackageId);
                ModelState.AddModelError("", "Selected package is not available.");
                booking.TripPackage = tripPackage;
                return View(booking);
            }

            if (ModelState.IsValid)
            {
                booking.BookingDate = DateTime.UtcNow;
                booking.PaymentStatus = "Pending";

                decimal pricePerUnit = tripPackage.PriceAfterDiscount ?? tripPackage.PriceBeforeDiscount;
                decimal totalAmount = pricePerUnit * booking.Adults;
                booking.AmountPaid = null;

                _context.Add(booking);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Booking ID {BookingId} created for Package ID {PackageId}. Redirecting to payment.", booking.Id, booking.TripPackageId);
                return RedirectToAction("Initiate", "Payment", new { bookingId = booking.Id, amount = totalAmount });
            }
            else
            {
                _logger.LogWarning("Booking form submission failed validation for Package ID {PackageId}.", booking.TripPackageId);
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        _logger.LogDebug("Validation Error ({Key}): {ErrorMessage}", state.Key, error.ErrorMessage);
                    }
                }
                booking.TripPackage = tripPackage;
                return View(booking);
            }
        }

        public async Task<IActionResult> BookingConfirmation()
        {
            if (TempData["BookingId"] == null)
            {
                return RedirectToAction(nameof(Index));
            }
            int bookingId = (int)TempData["BookingId"];
            var booking = await _context.Bookings
                                      .Include(b => b.TripPackage)
                                      .FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null)
            {
                _logger.LogWarning("BookingConfirmation requested for non-existent Booking ID: {BookingId}", bookingId);
                return NotFound();
            }
            TempData.Keep("BookingId");
            return View(booking);
        }

        public async Task<IActionResult> PaymentResult(bool success = false, string message = "", int? bookingId = null)
        {
            _logger.LogInformation("Displaying PaymentResult page. Success: {Success}, BookingID: {BookingId}, Message: {Message}", success, bookingId, message);
            ViewBag.Success = success;
            ViewBag.Message = message;
            Booking? booking = null;
            if (bookingId.HasValue)
            {
                booking = await _context.Bookings.Include(b => b.TripPackage).FirstOrDefaultAsync(b => b.Id == bookingId.Value);
                if (booking == null) { _logger.LogWarning("Booking ID {BookingId} not found when displaying PaymentResult.", bookingId.Value); }
            }
            return View(booking);
        }

        public IActionResult CancellationPolicy()
        {
            _logger.LogInformation("Displaying Cancellation Policy page.");
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var errorViewModel = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
            _logger.LogError("An error occurred. Request ID: {RequestId}", errorViewModel.RequestId);
            return View(errorViewModel);
        }
    }
}
