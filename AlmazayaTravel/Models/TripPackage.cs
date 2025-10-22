using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http; // Added for IFormFile
using System.Collections.Generic; // Added for ICollection

namespace AlmazayaTravel.Models
{
    public class TripPackage
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "English package name is required.")]
        [StringLength(100)]
        [Display(Name = "Package Name (English)")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "English description is required.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Description (English)")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "English destination country is required.")]
        [StringLength(50)]
        [Display(Name = "Destination Country (English)")]
        public string DestinationCountry { get; set; } = string.Empty;

        [Required(ErrorMessage = "Arabic package name is required.")]
        [StringLength(150)]
        [Display(Name = "اسم الباقة (عربي)")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Arabic description is required.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "الوصف (عربي)")]
        public string DescriptionAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Arabic destination country is required.")]
        [StringLength(70)]
        [Display(Name = "بلد الوجهة (عربي)")]
        public string DestinationCountryAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Duration in days is required.")]
        [Range(1, 90)]
        [Display(Name = "Duration (Days)")]
        public int DurationDays { get; set; }

        [Required(ErrorMessage = "Price before discount is required.")]
        [Range(0.01, double.MaxValue)]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Price (Before Discount)")]
        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        public decimal PriceBeforeDiscount { get; set; }

        [Range(0.00, double.MaxValue)]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Price (After Discount)")]
        [DisplayFormat(DataFormatString = "{0:N2}", ApplyFormatInEditMode = true)]
        public decimal? PriceAfterDiscount { get; set; }

        [StringLength(255)]
        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [NotMapped]
        [Display(Name = "Package Image File")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Is Active?")]
        public bool IsActive { get; set; } = true;

        public virtual ICollection<Booking>? Bookings { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
