﻿using System.ComponentModel.DataAnnotations;

namespace Online_Marketplace.Shared.DTOs
{
    public class SellerForRegistrationDto
    {
        [Required(ErrorMessage = "Firstname is required")]
        public string FirstName { get; init; }

        [Required(ErrorMessage = "Lastname is required")]
        public string LastName { get; init; }

        [Required(ErrorMessage = "BusinessName is required")]
        public string BusinessName { get; init; }

        [Required(ErrorMessage = "UserName is required")]
        public string UserName { get; init; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; init; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; init; }

        [Required(ErrorMessage = "Phone Number is required")]
        public string PhoneNumber { get; init; }
    }
}
