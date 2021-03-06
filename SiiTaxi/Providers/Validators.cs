﻿using System.Text.RegularExpressions;

namespace SiiTaxi.Providers
{
    public static class Validators
    {
        public static bool IsEmailValid(string email, bool company = false)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email && (!company || email.EndsWith("@pl.sii.eu"));
            }
            catch
            {
                return false;
            }
        }

        public static bool IsCaptchaValid(string response)
        {
            return (ReCaptcha.Validate(response) == "True");
        }

        public static bool IsPhoneValid(string phone)
        {
            return Regex.Match(phone, @"[+]?\d{8,12}").Success || phone == string.Empty;
        }
    }
}
