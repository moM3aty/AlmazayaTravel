using AlmazayaTravel.Data;
using AlmazayaTravel.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.Encoders;

namespace AlmazayaTravel.Controllers
{
    public class BankApiResponse
    {
        public string? result { get; set; }
        public string? status { get; set; }
        public string? error { get; set; }
        public string? errorText { get; set; }
    }

    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly string _merchantId;
        private readonly string _terminalId;
        private readonly string _tranportalId;
        private readonly string _tranportalPassword;
        private readonly string _aesKey;
        private readonly string _iv;
        private readonly string _secureHashKey;
        private readonly string _paymentUrl;
        private readonly string _returnUrlBase;

        public PaymentController(ApplicationDbContext context, IConfiguration configuration, ILogger<PaymentController> logger, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            _merchantId = _configuration["PaymentGateway:MerchantId"] ?? string.Empty;
            _terminalId = _configuration["PaymentGateway:TerminalId"] ?? string.Empty;
            _tranportalId = _configuration["PaymentGateway:TranportalId"] ?? string.Empty;
            _tranportalPassword = _configuration["PaymentGateway:TranportalPassword"] ?? string.Empty;
            _aesKey = _configuration["PaymentGateway:TerminalResourceKey"] ?? string.Empty;
            _iv = _configuration["PaymentGateway:IV"] ?? string.Empty;
            _secureHashKey = _configuration["PaymentGateway:SecureHashKey"] ?? string.Empty;
            _paymentUrl = _configuration["PaymentGateway:PaymentUrl"] ?? "https://securepayments.alrajhibank.com.sa/pg/payment/hosted.htm";

            if (string.IsNullOrEmpty(_tranportalId)) _logger.LogCritical("CRITICAL CONFIGURATION WARNING: TranportalId is not configured!");
            if (string.IsNullOrEmpty(_tranportalPassword)) _logger.LogCritical("CRITICAL CONFIGURATION WARNING: TranportalPassword is not configured!");
            if (string.IsNullOrEmpty(_aesKey) || Encoding.UTF8.GetBytes(_aesKey).Length != 32) _logger.LogCritical("CRITICAL CONFIGURATION WARNING: AES Key (TerminalResourceKey) byte length is not 32 (256 bits)!");
            if (string.IsNullOrEmpty(_iv) || Encoding.UTF8.GetBytes(_iv).Length != 16) _logger.LogCritical("CRITICAL CONFIGURATION WARNING: IV byte length is not 16 (128 bits)!");

            _returnUrlBase = _configuration["AppSettings:BaseUrl"] ?? string.Empty;
            if (string.IsNullOrEmpty(_returnUrlBase))
            {
                _logger.LogWarning("AppSettings:BaseUrl is not configured. Falling back to request host for callback URLs.");
            }
        }

        public async Task<IActionResult> Initiate(int bookingId, decimal amount)
        {
            var booking = await _context.Bookings
                                        .Include(b => b.TripPackage)
                                        .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
            {
                _logger.LogWarning("Initiate Payment: Booking ID {BookingId} not found.", bookingId);
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("Index", "Home");
            }
            if (booking.PaymentStatus?.ToLower() == "completed")
            {
                _logger.LogInformation("Initiate Payment: Booking ID {BookingId} already completed.", bookingId);
                return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id, message = "Payment already completed." });
            }

            // *** Use amount passed from HomeController (which should be TotalAmountDue) ***
            string trackId = $"ALM-{booking.Id}-{DateTime.UtcNow.Ticks}";
            string currencyCode = "682";
            string actionCode = "1";
            string amountFormatted = amount.ToString("0.00", CultureInfo.InvariantCulture);

            string responseUrl = Url.Action("PaymentSuccess", "Payment", null, Request.Scheme) ?? string.Empty;
            string errorUrl = Url.Action("PaymentFailure", "Payment", null, Request.Scheme) ?? string.Empty;
            if (string.IsNullOrEmpty(_returnUrlBase))
            {
                if (!Uri.IsWellFormedUriString(responseUrl, UriKind.Absolute)) responseUrl = $"{Request.Scheme}://{Request.Host}{responseUrl}";
                if (!Uri.IsWellFormedUriString(errorUrl, UriKind.Absolute)) errorUrl = $"{Request.Scheme}://{Request.Host}{errorUrl}";
            }
            else
            {
                responseUrl = $"{_returnUrlBase.TrimEnd('/')}{Url.Action("PaymentSuccess", "Payment")}";
                errorUrl = $"{_returnUrlBase.TrimEnd('/')}{Url.Action("PaymentFailure", "Payment")}";
            }

            _logger.LogInformation("--- Al Rajhi Payment Request ---");

            var plainTrandataDict = new Dictionary<string, object>
            {
                { "id", _tranportalId },
                { "password", _tranportalPassword },
                { "action", actionCode },
                { "currencyCode", currencyCode },
                { "errorURL", errorUrl },
                { "responseURL", responseUrl },
                { "trackId", trackId },
                { "amt", amountFormatted }
            };
            var plainTrandataList = new List<Dictionary<string, object>> { plainTrandataDict };
            string plainTrandataJson = JsonSerializer.Serialize(plainTrandataList);
            _logger.LogInformation("Plain Trandata JSON for Booking ID {BookingId} (Dict, Array Wrapper, CORE Fields ONLY): {PlainJson}", bookingId, plainTrandataJson);

            string encryptedTrandataHex = EncryptAesBouncyCastle(plainTrandataJson, _aesKey, _iv);
            if (string.IsNullOrEmpty(encryptedTrandataHex))
            {
                _logger.LogError("Initiate Payment: AES Encryption failed (BouncyCastle) for Booking ID {BookingId}.", bookingId);
                TempData["ErrorMessage"] = "Payment processing error (Encryption BC). Please try again.";
                return RedirectToAction("PaymentResult", "Home", new { success = false, bookingId = bookingId, message = TempData["ErrorMessage"] });
            }
            _logger.LogInformation("Encrypted Trandata (HEX, BouncyCastle) for Booking ID {BookingId}: {EncryptedData}", bookingId, encryptedTrandataHex);

            var requestPayloadObject = new[] { new {
                 id = _tranportalId,
                 trandata = encryptedTrandataHex,
                 errorURL = errorUrl,
                 responseURL = responseUrl
            }};
            string requestPayloadJson = JsonSerializer.Serialize(requestPayloadObject);
            _logger.LogInformation("Final JSON Payload for Bank API for Booking ID {BookingId}: {PayloadJson}", bookingId, requestPayloadJson);

            try
            {
                var client = _httpClientFactory.CreateClient();
                var content = new StringContent(requestPayloadJson, Encoding.UTF8, "application/json");

                _logger.LogInformation("Sending POST request to {PaymentUrl} for Booking ID {BookingId}", _paymentUrl, bookingId);
                HttpResponseMessage bankResponse = await client.PostAsync(_paymentUrl, content);

                string responseBody = await bankResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Received response from Bank API for Booking ID {BookingId}. Status Code: {StatusCode}, Body: {ResponseBody}", bookingId, bankResponse.StatusCode, responseBody);

                if (bankResponse.IsSuccessStatusCode)
                {
                    List<BankApiResponse>? apiResponseArray = null;
                    try
                    {
                        apiResponseArray = JsonSerializer.Deserialize<List<BankApiResponse>>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Initiate Payment: JSON Deserialization failed for Bank API response. Body: {ResponseBody}", responseBody);
                    }

                    if (apiResponseArray != null && apiResponseArray.Count > 0)
                    {
                        var apiResponse = apiResponseArray[0];
                        if (apiResponse?.status == "1" && !string.IsNullOrEmpty(apiResponse.result))
                        {
                            string[] resultParts = apiResponse.result.Split(':');
                            if (resultParts.Length >= 2)
                            {
                                string paymentId = resultParts[0];
                                string baseUrl = string.Join(":", resultParts.Skip(1)).Trim();
                                if (!string.IsNullOrEmpty(paymentId) && Uri.IsWellFormedUriString(baseUrl, UriKind.Absolute))
                                {
                                    string redirectUrl = $"{baseUrl}?PaymentID={paymentId}";

                                    booking.PaymentTransactionId = trackId; // Store the trackId used for this attempt
                                    // *** Ensure TotalAmountDue is saved before redirecting if not already saved ***
                                    // (It should be saved by HomeController, but double-check)
                                    _context.Update(booking);
                                    await _context.SaveChangesAsync();

                                    _logger.LogInformation("Initiate Payment: Successfully received PaymentID {PaymentId} for Booking ID {BookingId}. Redirecting user to: {RedirectUrl}", paymentId, bookingId, redirectUrl);
                                    return Redirect(redirectUrl);
                                }
                                else { _logger.LogError("Initiate Payment: Invalid PaymentID or BaseURL received in API response result for Booking ID {BookingId}. Result: {ApiResult}", bookingId, apiResponse.result); }
                            }
                            else { _logger.LogError("Initiate Payment: Unexpected format in API response 'result' field for Booking ID {BookingId}. Result: {ApiResult}", bookingId, apiResponse.result); }
                        }
                        else
                        {
                            _logger.LogError("Initiate Payment: Bank API returned non-success status or empty result for Booking ID {BookingId}. Status: {ApiStatus}, ErrorCode: {ApiError}, ErrorText: {ApiErrorText}, Result: {ApiResult}",
                                bookingId, apiResponse?.status, apiResponse?.error, apiResponse?.errorText, apiResponse?.result);
                        }
                    }
                    else { _logger.LogError("Initiate Payment: Failed to deserialize or empty array in Bank API response for Booking ID {BookingId}. Body: {ResponseBody}", bookingId, responseBody); }
                }
                else { _logger.LogError("Initiate Payment: Bank API call failed for Booking ID {BookingId}. Status Code: {StatusCode}, Reason: {ReasonPhrase}", bookingId, bankResponse.StatusCode, bankResponse.ReasonPhrase); }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Initiate Payment: HTTP Request Exception occurred during API call for Booking ID {BookingId}. Check network/URL.", bookingId);
            }
            catch (TaskCanceledException taskEx)
            {
                if (taskEx.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(taskEx, "Initiate Payment: API call timed out for Booking ID {BookingId}.", bookingId);
                }
                else
                {
                    _logger.LogError(taskEx, "Initiate Payment: API call cancelled for Booking ID {BookingId}.", bookingId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initiate Payment: General Exception occurred during API call or processing for Booking ID {BookingId}.", bookingId);
            }

            TempData["ErrorMessage"] = "Failed to initiate payment. Please try again or contact support.";
            return RedirectToAction("PaymentResult", "Home", new { success = false, bookingId = bookingId, message = TempData["ErrorMessage"] });
        }


        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentSuccess(IFormCollection form)
        {
            string? paymentId = form["paymentid"];
            string? result = form["result"];
            string? trackId = form["trackid"];
            // string? responseHash = form["hash"]; // Hash is still missing
            string? amountStr = form["amt"];    // Amount is still empty
            // string? udf1 = form["udf1"];       // UDF1 is still missing
            string? transId = form["tranid"];
            string? errorCode = form["Error"];
            string? errorMessage = form["ErrorText"];
            string? postDate = form["postdate"];
            string? authCode = form["auth"];
            string? refNum = form["ref"];
            string? avr = form["avr"];

            _logger.LogInformation("Payment Success Callback Received. TrackID: {TrackId}, Result: {Result}, PaymentID: {PaymentId}, TranID: {TranId}", trackId, result, paymentId, transId);
            LogFormData(form, "PaymentSuccess");

            if (string.IsNullOrEmpty(trackId))
            {
                _logger.LogWarning("Payment Success Callback - Missing required parameter: trackId.");
                return Content("Error: Incomplete payment response received (Missing TrackID).");
            }

            _logger.LogWarning("HASH VERIFICATION BYPASSED FOR TESTING - DO NOT USE IN PRODUCTION");
            bool hashVerified = true;

            if (!hashVerified)
            {
                _logger.LogError("Payment Success Callback - HASH MISMATCH! TrackID: {TrackId}.", trackId);
                return Content("Error: Payment verification failed.");
            }

            _logger.LogInformation("Payment Success Callback - Hash Verified (!! Method Assumed/Bypassed !!). TrackID: {TrackId}", trackId);

            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.PaymentTransactionId == trackId);

            if (booking == null)
            {
                _logger.LogWarning("Payment Success Callback - Booking not found for TrackID {TrackId}. PaymentID was {PaymentId}.", trackId, paymentId);
                return Content("Error: Associated booking not found.");
            }
            int bookingId = booking.Id;

            if (booking.PaymentStatus?.ToLower() == "completed")
            {
                _logger.LogInformation("Payment Success Callback - Booking ID {BookingId} already marked as completed. Ignoring duplicate callback.", booking.Id);
                return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id, message = "Payment was already confirmed." });
            }

            bool isSuccess = false;
            if (!string.IsNullOrEmpty(result) && (result.Equals("CAPTURED", StringComparison.OrdinalIgnoreCase) || result.Equals("APPROVED", StringComparison.OrdinalIgnoreCase)))
            {
                isSuccess = true;
            }
            else if (string.IsNullOrEmpty(result) && string.IsNullOrEmpty(errorCode) && string.IsNullOrEmpty(errorMessage))
            {
                _logger.LogWarning("Payment Success Callback - 'result', 'Error', and 'ErrorText' fields are empty for TrackID {TrackId}. Assuming success based on callback URL. VERIFY THIS LOGIC!", trackId);
                isSuccess = true;
            }
            else
            {
                _logger.LogWarning("Payment Success Callback - Bank result code indicates failure or is ambiguous. Result: {Result}, TrackID: {TrackId}, ErrorCode: {ErrorCode}, ErrorMsg: {ErrorMsg}", result, trackId, errorCode, errorMessage);
                isSuccess = false;
            }

            if (isSuccess)
            {
                // *** UPDATED: Use TotalAmountDue from booking record ***
                decimal? amountPaid = booking.TotalAmountDue;
                if (amountPaid == 0) // Log if the stored amount was zero for some reason
                {
                    _logger.LogWarning("Payment Success Callback - TotalAmountDue retrieved from booking for TrackID {TrackId} was zero.", trackId);
                }
                // *** END UPDATED ***

                _logger.LogInformation("Processing successful payment update for Booking ID: {BookingId}. Bank TranID: {TranId}. Amount set to {AmountPaid}", booking.Id, transId, amountPaid);
                booking.PaymentStatus = "Completed";
                booking.AmountPaid = amountPaid;
                _context.Update(booking);
                await _context.SaveChangesAsync();
                return RedirectToAction("PaymentResult", "Home", new { success = true, bookingId = booking.Id });
            }
            else
            {
                booking.PaymentStatus = "Failed";
                _context.Update(booking);
                await _context.SaveChangesAsync();
                return RedirectToAction("PaymentResult", "Home", new { success = false, bookingId = booking.Id, message = $"Payment failed. Gateway message: {errorMessage ?? result}" });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentFailure(IFormCollection form)
        {
            string? trackId = form["trackid"];
            string? result = form["result"];
            string? udf1 = form["udf1"];
            string? errorCode = form["Error"];
            string? errorMessage = form["ErrorText"];
            string? paymentId = form["paymentid"];

            _logger.LogWarning("Payment Failure Callback Received. TrackID: {TrackId}, Result: {Result}, PaymentID: {PaymentId}, ErrorCode: {ErrorCode}, ErrorMsg: {ErrorMsg}", trackId, result, paymentId, errorCode, errorMessage);
            LogFormData(form, "PaymentFailure");

            if (string.IsNullOrEmpty(trackId))
            {
                _logger.LogWarning("Payment Failure Callback - Missing required parameter: trackId.");
                return Content("Error: Incomplete payment failure response received (Missing TrackID).");
            }

            int bookingId = 0;
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.PaymentTransactionId == trackId);
            if (booking == null && !string.IsNullOrEmpty(udf1) && int.TryParse(udf1, out bookingId))
            {
                _logger.LogWarning("Payment Failure Callback - Could not find booking by TrackID {TrackId}. Attempting lookup by UDF1 BookingID {BookingId}.", trackId, bookingId);
                booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
            }


            if (booking != null && booking.PaymentStatus?.ToLower() != "completed")
            {
                booking.PaymentStatus = "Failed";
                _context.Update(booking);
                await _context.SaveChangesAsync();
                _logger.LogWarning("Marked Booking ID {BookingId} as Failed due to failure callback.", booking.Id);
            }
            else if (booking == null) { _logger.LogWarning("Payment Failure Callback - Booking not found for TrackID {TrackId} or UDF1 {BookingId}.", trackId, udf1); }
            else { _logger.LogInformation("Payment Failure Callback received for already completed Booking ID {BookingId}. No status change made.", booking.Id); }

            return RedirectToAction("PaymentResult", "Home", new { success = false, bookingId = booking?.Id, message = $"Payment failed or cancelled. Gateway message: {errorMessage ?? result}" });
        }


        private string EncryptAesBouncyCastle(string plainText, string key, string iv)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            try
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);
                byte[] ivBytes = Encoding.UTF8.GetBytes(iv);
                byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);

                if (keyBytes.Length != 32) { _logger.LogError("AES Encryption Error (BC): Key byte length is not 32."); return string.Empty; }
                if (ivBytes.Length != 16) { _logger.LogError("AES Encryption Error (BC): IV byte length is not 16."); return string.Empty; }

                PaddedBufferedBlockCipher cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()), new Pkcs7Padding());
                cipher.Init(true, new ParametersWithIV(new KeyParameter(keyBytes), ivBytes));
                byte[] outputBytes = cipher.DoFinal(inputBytes);
                return Hex.ToHexString(outputBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AES encryption (BouncyCastle).");
                return string.Empty;
            }
        }

        private string CalculateSha256Hash(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                _logger.LogError("SHA256 Hash calculation input was empty.");
                return string.Empty;
            }
            if (string.IsNullOrEmpty(_secureHashKey))
            {
                _logger.LogError("Attempted SHA256 hash calculation without a valid SecureHashKey configured (for callback?).");
                return string.Empty;
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating SHA256 hash for input starting with: {InputStart}", input.Length > 50 ? input.Substring(0, 50) : input);
                return string.Empty;
            }
        }

        private void LogFormData(IFormCollection form, string callbackType)
        {
            if (form == null) return;
            var formDataLog = new StringBuilder($"--- {callbackType} Form Data Received ---");
            foreach (var key in form.Keys.OrderBy(k => k))
            {
                string valueToLog = form[key];
                formDataLog.Append($"\n[{key}]: {valueToLog}");
            }
            _logger.LogInformation(formDataLog.ToString());
        }
    }
}

