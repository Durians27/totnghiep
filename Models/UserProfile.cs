using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VelvySkinWeb.Models
{
    public class UserProfile
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }

        [NotMapped]
        public string Email { get; set; } 

        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string DefaultAddress { get; set; }


        public string SkinType { get; set; }
        public string SkinConcern { get; set; }
        public string Allergies { get; set; }
        public string LockReason { get; set; }
    }
}