using AlmazayaTravel.Data;
using AlmazayaTravel.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging; // Added for ILogger
using Microsoft.AspNetCore.Authorization; // Added for [AllowAnonymous]

namespace AlmazayaTravel.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger; // Added ILogger field

        private readonly string _merchantId;
        private readonly string _terminalId;
        private readonly string _tranportalId;
        private readonly string _tranportalPassword;
        private readonly string _terminalResourceKey;
        private readonly string _secureHashKey;
        private readonly string _paymentUrl;
        private readonly string _returnUrlBase;

        // Modified constructor to inject ILogger
        public PaymentController(ApplicationDbContext context, IConfiguration configuration, ILogger<PaymentController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger; // Assign injected logger

            _merchantId = _configuration["PaymentGateway:MerchantId"] ?? string.Empty;
            _terminalId = _configuration["PaymentGateway:TerminalId"] ?? string.Empty;
            _tranportalId = _configuration["PaymentGateway:TranportalId"] ?? string.Empty;
            _tranportalPassword = _configuration["PaymentGateway:TranportalPassword"] ?? string.Empty;
            _terminalResourceKey = _configuration["PaymentGateway:TerminalResourceKey"] ?? string.Empty;
            _secureHashKey = _configuration["PaymentGateway:SecureHashKey"] ?? string.Empty;
            _paymentUrl = _configuration["PaymentGateway:PaymentUrl"] ?? "https://securepayments.alrajhibank.com.sa/pg/payment/hosted.htm";

            if (string.IsNullOrEmpty(_secureHashKey) || _secureHashKey == "YOUR_SECURE_HASH_KEY_FROM_BANK")
            {
                _logger.LogCritical("CRITICAL SECURITY WARNING: SecureHashKey is not configured in appsettings.json!"); // Use _logger
            }

            // Note: Cannot access Request object directly in constructor.
            // _returnUrlBase needs to be constructed within action methods if needed dynamically.
            // Or construct it based on configured base URL if available.
            _returnUrlBase = _configuration["AppSettings:BaseUrl"] ?? string.Empty; // Example: Read base URL from config
            if (string.IsNullOrEmpty(_returnUrlBase))
            {
                _logger.LogWarning("AppSettings:BaseUrl is not configured. Callback URLs might be incorrect if scheme/host changes.");
            }
        }

        // GET: /Payment/Initiate
        public async Task<IActionResult> Initiate(int bookingId, decimal amount)
        {
            var booking = await _context.Bookings
                                    .Include(b => b.TripPackage)
                                    .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("Index", "Home");
            }

            if (booking.PaymentStatus?.ToLower() == "completed")
            {
                TempData["InfoMessage"] = "This booking has already been paid.";
                return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id });
            }

            string trackId = $"ALM-{booking.Id}-{DateTime.UtcNow.Ticks}";
            string currencyCode = "SAR";
            string actionCode = "1";
            string amountFormatted = amount.ToString("0.00", CultureInfo.InvariantCulture);

            // Construct callback URLs dynamically within the action method
            string responseUrl = Url.Action("PaymentSuccess", "Payment", null, Request.Scheme) ?? string.Empty;
            string errorUrl = Url.Action("PaymentFailure", "Payment", null, Request.Scheme) ?? string.Empty;

            // Ensure URLs are absolute if needed by the gateway
            if (!Uri.IsWellFormedUriString(responseUrl, UriKind.Absolute))
                responseUrl = $"{Request.Scheme}://{Request.Host}{responseUrl}";
            if (!Uri.IsWellFormedUriString(errorUrl, UriKind.Absolute))
                errorUrl = $"{Request.Scheme}://{Request.Host}{errorUrl}";


            // --- Hash Calculation (REPLACE WITH BANK SPECIFIC LOGIC) ---
            // Example: String format might differ, key might prepend/be used differently.
            string hashString = $"{_tranportalId}|{_tranportalPassword}|{_terminalResourceKey}|{trackId}|{amountFormatted}|{currencyCode}|{actionCode}|{responseUrl}|{errorUrl}";
            string requestHash = CalculateSha256Hash(hashString + _secureHashKey); // Append key (Verify method!)

            if (string.IsNullOrEmpty(requestHash)) // Check if hashing failed (e.g., missing key)
            {
                _logger.LogError("Failed to generate request hash for TrackID {TrackId}. SecureHashKey might be missing.", trackId);
                TempData["ErrorMessage"] = "Payment processing error. Please contact support.";
                return RedirectToAction("Index", "Home");
            }
            // --- End Hash Calculation ---


            var paymentParams = new Dictionary<string, string>
            {
                 { "id", _terminalId },
                 { "password", _tranportalPassword },
                 { "action", actionCode },
                 { "amt", amountFormatted },
                 { "currencycode", currencyCode },
                 { "trackid", trackId },
                 { "responseURL", responseUrl },
                 { "errorURL", errorUrl },
                 { "udf1", booking.Id.ToString() },
                 { "udf2", booking.ClientName },
                 { "udf5", "Almazaya Booking" },
                 { "hash", requestHash } // Verify parameter name
                 // { "langid", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToUpperInvariant() } // Example: Pass language
            };

            booking.PaymentTransactionId = trackId;
            _context.Update(booking);
            await _context.SaveChangesAsync();

            ViewBag.PaymentUrl = _paymentUrl;
            ViewBag.PaymentParams = paymentParams;

            return View("RedirectToGateway");
        }

        [HttpPost]
        //[ValidateAntiForgeryToken] // Comment out if bank callback doesn't support it
        [AllowAnonymous] // Use attribute from Microsoft.AspNetCore.Authorization
        public async Task<IActionResult> PaymentSuccess(IFormCollection form)
        {
            string? paymentId = form["paymentid"];
            string? result = form["result"];
            string? trackId = form["trackid"];
            string? authCode = form["auth"];
            string? transId = form["tranid"];
            string? responseHash = form["hash"]; // Verify name
            string? amountStr = form["amt"];
            string? udf1 = form["udf1"]; // Booking ID
            string? errorCode = form["Error"];
            string? errorMessage = form["ErrorText"];

            _logger.LogInformation("Payment Success Callback Received. TrackID: {TrackId}, Result: {Result}, PaymentID: {PaymentId}", trackId, result, paymentId);

            if (string.IsNullOrEmpty(trackId) || string.IsNullOrEmpty(responseHash) || string.IsNullOrEmpty(udf1))
            {
                _logger.LogError("Payment Success Callback - Missing critical parameters.");
                TempData["ErrorMessage"] = "Payment response incomplete.";
                return RedirectToAction("PaymentResult", "Home", new { success = false, message = "Incomplete payment response received." });
            }

            // --- Hash Verification (REPLACE WITH BANK SPECIFIC LOGIC) ---
            // Example: String format might differ, key might prepend/be used differently.
            string responseHashString = $"{_terminalResourceKey}|{paymentId}|{result}|{trackId}|{amountStr}|{udf1}"; // Example String - REPLACE!
            string calculatedHash = CalculateSha256Hash(responseHashString + _secureHashKey); // Append key (Verify method!)

            if (string.IsNullOrEmpty(calculatedHash)) // Check if hashing failed (e.g., missing key)
            {
                _logger.LogError("Failed to calculate response hash for verification. TrackID {TrackId}. SecureHashKey might be missing.", trackId);
                TempData["ErrorMessage"] = "Payment verification error. Please contact support.";
                // Don't update booking status here as we can't be sure
                return RedirectToAction("PaymentResult", "Home", new { success = false, message = "Payment verification error." });
            }
            // --- End Hash Verification ---


            if (!string.Equals(calculatedHash, responseHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Payment Success Callback - HASH MISMATCH! Calculated: {Calculated}, Received: {Received}. TrackID: {TrackId}", calculatedHash, responseHash, trackId);
                var bookingForMismatch = await _context.Bookings.FirstOrDefaultAsync(b => b.PaymentTransactionId == trackId);
                if (bookingForMismatch != null)
                {
                    bookingForMismatch.PaymentStatus = "Verification Failed";
                    _context.Update(bookingForMismatch);
                    await _context.SaveChangesAsync();
                }
                TempData["ErrorMessage"] = "Payment verification failed. Please contact support.";
                return RedirectToAction("PaymentResult", "Home", new { success = false, message = "Payment verification failed.", bookingId = bookingForMismatch?.Id });
            }

            _logger.LogInformation("Payment Success Callback - Hash Verified. TrackID: {TrackId}", trackId);

            int bookingId = 0;
            int.TryParse(udf1, out bookingId);
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.PaymentTransactionId == trackId);

            if (booking == null)
            {
                _logger.LogError("Payment Success Callback - Booking not found for TrackID: {TrackId} and BookingID: {BookingId}", trackId, udf1);
                TempData["ErrorMessage"] = "Associated booking not found for this payment.";
                return RedirectToAction("PaymentResult", "Home", new { success = false, message = "Associated booking not found." });
            }

            if (booking.PaymentStatus?.ToLower() == "completed")
            {
                _logger.LogInformation("Payment Success Callback - Booking ID {BookingId} already marked as completed.", booking.Id);
                return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id, message = "Payment was already confirmed." });
            }

            decimal amountPaid = 0;
            decimal.TryParse(amountStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amountPaid);

            // Adapt success condition based on actual codes from Al Rajhi
            if (result != null && (result.Equals("CAPTURED", StringComparison.OrdinalIgnoreCase) || result.Equals("APPROVED", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Payment Success Callback - Processing successful payment for Booking ID: {BookingId}", booking.Id);
                booking.PaymentStatus = "Completed";
                booking.AmountPaid = amountPaid;
                if (!string.IsNullOrEmpty(transId) && transId != trackId)
                {
                    booking.PaymentTransactionId = transId;
                }
                _context.Update(booking);
                await _context.SaveChangesAsync();
                // TODO: Send confirmation email etc.
                TempData["SuccessMessage"] = "Payment completed successfully!";
                return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id });
            }
            else
            {
                _logger.LogWarning("Payment Success Callback - Result indicates failure/issue. Result: {Result}, Error: {Error}, ErrorText: {ErrorText}. TrackID: {TrackId}", result, errorCode, errorMessage, trackId);
                booking.PaymentStatus = "Failed";
                _context.Update(booking);
                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] = $"Payment failed or was not approved. Gateway message: {errorMessage ?? result}";
                return RedirectToAction("PaymentResult", "Home", new { success = false, bookingId = booking.Id, message = $"Payment failed. Gateway message: {errorMessage ?? result}" });
            }
        }

        [HttpPost] // Assuming POST
        [AllowAnonymous] // Use attribute from Microsoft.AspNetCore.Authorization
        public async Task<IActionResult> PaymentFailure(IFormCollection form)
        {
            string? trackId = form["trackid"];
            string? errorCode = form["Error"];
            string? errorMessage = form["ErrorText"];
            string? result = form["result"];
            string? udf1 = form["udf1"];

            _logger.LogWarning("Payment Failure Callback Received. TrackID: {TrackId}, Result: {Result}, Error: {Error}, ErrorText: {ErrorText}", trackId, result, errorCode, errorMessage);

            int bookingId = 0;
            int.TryParse(udf1, out bookingId);
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId || b.PaymentTransactionId == trackId);

            if (booking != null && booking.PaymentStatus?.ToLower() != "completed")
            {
                booking.PaymentStatus = "Failed";
                _context.Update(booking);
                await _context.SaveChangesAsync();
                _logger.LogWarning("Marked Booking ID {BookingId} as Failed due to payment failure callback.", booking.Id);
            }
            else if (booking == null)
            {
                _logger.LogWarning("Could not find associated booking for failed payment callback. TrackID: {TrackId}, BookingID: {BookingId}", trackId, udf1);
            }

            TempData["ErrorMessage"] = $"Payment failed or was cancelled. Gateway message: {errorMessage ?? result}";
            return RedirectToAction("PaymentResult", "Home", new { success = false, bookingId = booking?.Id, message = $"Payment failed or cancelled. Gateway message: {errorMessage ?? result}" });
        }

        private string CalculateSha256Hash(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            if (string.IsNullOrEmpty(_secureHashKey) || _secureHashKey == "YOUR_SECURE_HASH_KEY_FROM_BANK")
            {
                _logger.LogError("Attempted to calculate hash without a valid SecureHashKey.");
                return string.Empty; // Return empty to indicate failure
            }

            try
            {
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        builder.Append(bytes[i].ToString("x2"));
                    }
                    return builder.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating SHA256 hash.");
                return string.Empty; // Return empty on error
            }
        }
    }
}

