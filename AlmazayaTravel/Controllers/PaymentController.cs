using AlmazayaTravel.Data;
using AlmazayaTravel.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AlmazayaTravel.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;

        private readonly string _merchantId;
        private readonly string _terminalId;
        private readonly string _tranportalId;
        private readonly string _tranportalPassword;
        private readonly string _terminalResourceKey;
        private readonly string _secureHashKey;
        private readonly string _paymentUrl;
        private readonly string _returnUrlBase;

        public PaymentController(ApplicationDbContext context, IConfiguration configuration, ILogger<PaymentController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;

            _merchantId = _configuration["PaymentGateway:MerchantId"] ?? string.Empty;
            _terminalId = _configuration["PaymentGateway:TerminalId"] ?? string.Empty;
            _tranportalId = _configuration["PaymentGateway:TranportalId"] ?? string.Empty;
            _tranportalPassword = _configuration["PaymentGateway:TranportalPassword"] ?? string.Empty;
            _terminalResourceKey = _configuration["PaymentGateway:TerminalResourceKey"] ?? string.Empty;
            _secureHashKey = _configuration["PaymentGateway:SecureHashKey"] ?? string.Empty;
            _paymentUrl = _configuration["PaymentGateway:PaymentUrl"] ?? "https://securepayments.alrajhibank.com.sa/pg/payment/hosted.htm";

            if (string.IsNullOrEmpty(_secureHashKey) || _secureHashKey == "YOUR_SECURE_HASH_KEY_FROM_BANK")
            {
                _logger.LogCritical("CRITICAL SECURITY WARNING: SecureHashKey is not configured!");
            }
            _returnUrlBase = _configuration["AppSettings:BaseUrl"] ?? string.Empty;
            if (string.IsNullOrEmpty(_returnUrlBase))
            {
                _logger.LogWarning("AppSettings:BaseUrl is not configured.");
            }
        }

        public async Task<IActionResult> Initiate(int bookingId, decimal amount)
        {
            var booking = await _context.Bookings
                                      .Include(b => b.TripPackage)
                                      .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) { return RedirectToAction("Index", "Home"); }
            if (booking.PaymentStatus?.ToLower() == "completed") { return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id }); }

            string trackId = $"ALM-{booking.Id}-{DateTime.UtcNow.Ticks}";
            string currencyCode = "SAR";
            string actionCode = "1";
            string amountFormatted = amount.ToString("0.00", CultureInfo.InvariantCulture);

            string responseUrl = Url.Action("PaymentSuccess", "Payment", null, Request.Scheme) ?? string.Empty;
            string errorUrl = Url.Action("PaymentFailure", "Payment", null, Request.Scheme) ?? string.Empty;
            if (!Uri.IsWellFormedUriString(responseUrl, UriKind.Absolute)) responseUrl = $"{Request.Scheme}://{Request.Host}{responseUrl}";
            if (!Uri.IsWellFormedUriString(errorUrl, UriKind.Absolute)) errorUrl = $"{Request.Scheme}://{Request.Host}{errorUrl}";

            string hashString = $"{_tranportalId}|{_tranportalPassword}|{_terminalResourceKey}|{trackId}|{amountFormatted}|{currencyCode}|{actionCode}|{responseUrl}|{errorUrl}";
            string requestHash = CalculateSha256Hash(hashString + _secureHashKey);
            if (string.IsNullOrEmpty(requestHash)) { return RedirectToAction("Index", "Home"); }

            var paymentParams = new Dictionary<string, string> {
                 { "id", _terminalId }, { "password", _tranportalPassword }, { "action", actionCode },
                 { "amt", amountFormatted }, { "currencycode", currencyCode }, { "trackid", trackId },
                 { "responseURL", responseUrl }, { "errorURL", errorUrl },
                 { "udf1", booking.Id.ToString() }, { "udf2", booking.ClientName },
                 { "udf5", "Almazaya Booking" }, { "hash", requestHash }
             };

            booking.PaymentTransactionId = trackId;
            _context.Update(booking);
            await _context.SaveChangesAsync();

            ViewBag.PaymentUrl = _paymentUrl;
            ViewBag.PaymentParams = paymentParams;
            return View("RedirectToGateway");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentSuccess(IFormCollection form)
        {
            string? paymentId = form["paymentid"]; string? result = form["result"]; string? trackId = form["trackid"];
            string? authCode = form["auth"]; string? transId = form["tranid"]; string? responseHash = form["hash"];
            string? amountStr = form["amt"]; string? udf1 = form["udf1"]; string? errorCode = form["Error"]; string? errorMessage = form["ErrorText"];

            _logger.LogInformation("Payment Success Callback Received. TrackID: {TrackId}, Result: {Result}", trackId, result);

            if (string.IsNullOrEmpty(trackId) || string.IsNullOrEmpty(responseHash) || string.IsNullOrEmpty(udf1)) { return RedirectToAction("PaymentResult", "Home", new { success = false, message = "Incomplete response." }); }

            string responseHashString = $"{_terminalResourceKey}|{paymentId}|{result}|{trackId}|{amountStr}|{udf1}";
            string calculatedHash = CalculateSha256Hash(responseHashString + _secureHashKey);
            if (string.IsNullOrEmpty(calculatedHash)) { return RedirectToAction("PaymentResult", "Home", new { success = false, message = "Verification error." }); }

            if (!string.Equals(calculatedHash, responseHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Payment Success Callback - HASH MISMATCH! TrackID: {TrackId}", trackId);
                var bookingForMismatch = await _context.Bookings.FirstOrDefaultAsync(b => b.PaymentTransactionId == trackId);
                if (bookingForMismatch != null) { await _context.SaveChangesAsync(); }
                return RedirectToAction("PaymentResult", "Home", new { success = false, message = "Payment verification failed.", bookingId = bookingForMismatch?.Id });
            }

            _logger.LogInformation("Payment Success Callback - Hash Verified. TrackID: {TrackId}", trackId);
            int bookingId = 0; int.TryParse(udf1, out bookingId);
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.PaymentTransactionId == trackId);
            if (booking == null) { return RedirectToAction("PaymentResult", "Home", new { success = false, message = "Booking not found." }); }
            if (booking.PaymentStatus?.ToLower() == "completed") { return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id, message = "Already confirmed." }); }

            decimal amountPaid = 0; decimal.TryParse(amountStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amountPaid);

            if (result != null && (result.Equals("CAPTURED", StringComparison.OrdinalIgnoreCase) || result.Equals("APPROVED", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Processing successful payment for Booking ID: {BookingId}", booking.Id);
                booking.PaymentStatus = "Completed"; booking.AmountPaid = amountPaid;
                if (!string.IsNullOrEmpty(transId) && transId != trackId) { booking.PaymentTransactionId = transId; }
                _context.Update(booking); await _context.SaveChangesAsync();

                return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id });
            }
            else
            {
                _logger.LogWarning("Payment Success Callback - Result indicates failure. Result: {Result}, TrackID: {TrackId}", result, trackId);
                booking.PaymentStatus = "Failed"; _context.Update(booking); await _context.SaveChangesAsync();
                return RedirectToAction("PaymentResult", "Home", new { success = false, bookingId = booking.Id, message = $"Payment failed. Gateway message: {errorMessage ?? result}" });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentFailure(IFormCollection form)
        {
            string? trackId = form["trackid"]; string? errorCode = form["Error"]; string? errorMessage = form["ErrorText"];
            string? result = form["result"]; string? udf1 = form["udf1"];

            _logger.LogWarning("Payment Failure Callback Received. TrackID: {TrackId}, Result: {Result}", trackId, result);

            int bookingId = 0; int.TryParse(udf1, out bookingId);
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId || b.PaymentTransactionId == trackId);

            if (booking != null && booking.PaymentStatus?.ToLower() != "completed")
            {
                booking.PaymentStatus = "Failed"; _context.Update(booking); await _context.SaveChangesAsync();
                _logger.LogWarning("Marked Booking ID {BookingId} as Failed.", booking.Id);
            }
            else if (booking == null) { _logger.LogWarning("Booking not found for failed callback. TrackID: {TrackId}", trackId); }

            return RedirectToAction("PaymentResult", "Home", new { success = false, bookingId = booking?.Id, message = $"Payment failed or cancelled. Gateway message: {errorMessage ?? result}" });
        }

        private string CalculateSha256Hash(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (string.IsNullOrEmpty(_secureHashKey) || _secureHashKey == "YOUR_SECURE_HASH_KEY_FROM_BANK")
            {
                _logger.LogError("Attempted hash calculation without SecureHashKey."); return string.Empty;
            }
            try
            {
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < bytes.Length; i++) { builder.Append(bytes[i].ToString("x2")); }
                    return builder.ToString();
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error calculating SHA256 hash."); return string.Empty; }
        }
    }
}
