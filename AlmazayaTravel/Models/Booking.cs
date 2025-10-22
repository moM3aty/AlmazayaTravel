using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlmazayaTravel.Models
{
    public class Booking
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Client name is required.")]
        [StringLength(100)]
        [Display(Name = "Client Name")]
        public string ClientName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(100)]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Number of adults is required.")]
        [Range(1, 100, ErrorMessage = "Number of adults must be at least 1.")]
        [Display(Name = "Number of Adults")]
        public int Adults { get; set; }

        [Range(0, 100, ErrorMessage = "Number of children cannot be negative.")]
        [Display(Name = "Number of Children")]
        public int Children { get; set; } = 0;

        [Required]
        [Display(Name = "Trip Package")]
        public int TripPackageId { get; set; }

        [ForeignKey("TripPackageId")]
        [Display(Name = "Trip Package")]
        public virtual TripPackage? TripPackage { get; set; }

        [Required]
        [Display(Name = "Booking Date")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime BookingDate { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = "Pending";

        [StringLength(100)]
        [Display(Name = "Transaction ID")]
        public string? PaymentTransactionId { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Amount Paid")]
        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = false)]
        public decimal? AmountPaid { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
