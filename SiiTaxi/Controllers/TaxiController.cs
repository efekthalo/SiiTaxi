﻿using SiiTaxi.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Mvc;
using SiiTaxi.Providers;
using System.Linq;

namespace SiiTaxi.Controllers
{
    public class TaxiController : Controller
    {
        [HttpPost]
        public ActionResult New(string ownerName, string ownerPhone, string time, 
            string ownerEmail, string przejazdFrom, string przejazdTo, 
            List<string> adds, int approver, bool isBigTaxi, 
            bool order, TaxiViewModel taxiModel, PeopleViewModel peopleModel, TaxiPeopleViewModel taxiPeopleModel)
        {
            TempData["formData"] = Request.Form;
            var encodedResponse = Request.Form["g-Recaptcha-Response"];
            if (!Validators.IsCaptchaValid(encodedResponse))
            {
                TempData["errorMessage"] = Messages.NotValidCaptcha;
                return View(new PeopleViewModel());
            }

            if(!Validators.IsEmailValid(ownerEmail, true))
            {
                TempData["errorMessage"] = Messages.NotValidCompanyEmail;
                return View(new PeopleViewModel());
            }
            if (!Validators.IsPhoneValid(ownerPhone))
            {
                TempData["errorMessage"] = Messages.NotValidPhone;
                return View(new PeopleViewModel());
            }

            DateTime parsedTime;
            DateTime.TryParseExact(time, "dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedTime);

            if(parsedTime < DateTime.Now)
            {
                TempData["errorMessage"] = Messages.NotValidDate;
                return View(new PeopleViewModel());
            }

            var owner = new People
            {
                Name = ownerName,
                Email = ownerEmail,
                Phone = ownerPhone
            };

            var taxi = new Taxi
            {
                From = przejazdFrom,
                To = przejazdTo,
                Time = parsedTime,
                Owner = peopleModel.UpdateEntityBy("Email", owner).PeopleId,
                Approver = approver,
                IsBigTaxi = isBigTaxi,
                Order = order
            };

            try
            {
                taxiModel.UpdateEntity(null, taxi);

                if (adds != null)
                {
                    foreach (var add in adds)
                    {
                        var other = new People { Name = "", Email = add };
                        var taxiPeople = new TaxiPeople { TaxiId = taxi.TaxiId, PeopleId = peopleModel.UpdateEntityBy("Email", other).PeopleId };
                        taxiPeopleModel.UpdateEntity(null, taxiPeople);
                    }
                }

                taxiModel.SendConfirmEmail(taxi.TaxiId);
            }
            catch
            {
                TempData["errorMessage"] = Messages.DatabaseError;
                return View(new PeopleViewModel());
            }

            TempData["successMessage"] = Messages.AddNewTaxiSuccess;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpGet]
        public ActionResult New()
        {
            return View(new PeopleViewModel());
        }

        public ActionResult Join(int id, TaxiViewModel taxiModel)
        {
            var maxInTaxi = 3;
            TempData["formData"] = Request.Form;
            var taxi = taxiModel.GetEntityBy<Taxi>("TaxiId", id);
            if (taxi != null)
            {
                if (taxi.IsBigTaxi)
                    maxInTaxi = 6;

                if (taxi.TaxiPeople.Count <= maxInTaxi)
                {
                    return View(taxi);
                }
                else
                {
                    TempData["errorMessage"] = Messages.TaxiFull;
                    return RedirectToAction("Index", "Taxi");
                }
            }
            TempData["errorMessage"] = Messages.TaxiNotFound;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpPost]
        public ActionResult Join(int id, string name, string phone, string email, TaxiViewModel taxiModel, PeopleViewModel peopleModel, TaxiPeopleViewModel taxiPeopleModel)
        {
            var maxInTaxi = 3;
            var taxi = taxiModel.GetEntityBy<Taxi>("TaxiId",id);
            if (taxi.IsBigTaxi)
                maxInTaxi = 6;

            TempData["formData"] = Request.Form;
            var encodedResponse = Request.Form["g-Recaptcha-Response"];
            if (!Validators.IsCaptchaValid(encodedResponse))
            {
                TempData["errorMessage"] = Messages.NotValidCaptcha;
                return View(taxi);
            }
            if (!Validators.IsEmailValid(email, true))
            {
                TempData["errorMessage"] = Messages.NotValidCompanyEmail;
                return View(taxi);
            }
            if (!Validators.IsPhoneValid(phone))
            {
                TempData["errorMessage"] = Messages.NotValidPhone;
                return View(taxi);
            }
            if (taxi.TaxiPeople.Count >= maxInTaxi)
            {
                TempData["errorMessage"] = Messages.TaxiFull;
                return RedirectToAction("Index", "Taxi");
            }
            if (email == taxi.People.Email || taxi.TaxiPeople.Any(x => x.People.Name == name))
            {
                TempData["errorMessage"] = Messages.JoinedAlready;
                return RedirectToAction("Index", "Taxi");
            }

            try
            {
                var other = new People { Name = name, Email = email, Phone = phone };
                var taxiPeople = new TaxiPeople { TaxiId = id, PeopleId = peopleModel.UpdateEntityBy("Email", other).PeopleId };
                taxiPeople = taxiPeopleModel.UpdateEntity(null, taxiPeople);

                taxiModel.SendJoinEmail(taxiPeople.Id);
            }
            catch
            {
                TempData["errorMessage"] = Messages.DatabaseError;
                return View(taxi);
            }

            TempData["successMessage"] = Messages.IncludeTaxiSuccess;
            return RedirectToAction("Index", "Taxi");
        }

        public ActionResult Index(DateTime? date = null)
        {
            return View(new TaxiViewModel(date ?? DateTime.Now));
        }

        [HttpGet]
        public ActionResult Confirm(int id, string code)
        {
            var taxi = new TaxiViewModel().GetEntityBy<Taxi>("TaxiId", id);
            if(taxi != null && taxi.ConfirmCode == code)
            {
                if (taxi.IsConfirmed)
                {
                    TempData["errorMessage"] = Messages.TaxiConfirmed;
                    return RedirectToAction("Index", "Taxi");
                }
                TempData["code"] = code;
                return View(taxi);
            }
            TempData["errorMessage"] = Messages.TaxiNotFound;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpPost]
        public ActionResult Confirm(int id, string code, TaxiViewModel taxiModel)
        {
            try
            {
                taxiModel.ConfirmTaxi(id, code);
            }
            catch
            {
                TempData["errorMessage"] = Messages.ConfirmFailed;
                return View(taxiModel.GetEntityBy<Taxi>("TaxiId", id));
            }
            TempData["successMessage"] = Messages.ConfirmSucceed;
            return RedirectToAction("Index", "Taxi") ;
        }

        [HttpGet]
        public ActionResult ConfirmJoin(int id, string code)
        {
            var taxiPeople = new TaxiViewModel().GetEntityBy<TaxiPeople>("Id", id);
            if (taxiPeople != null && taxiPeople.ConfirmCode == code)
            {
                if (taxiPeople.IsConfirmed)
                {
                    TempData["errorMessage"] = Messages.TaxiConfirmed;
                    return RedirectToAction("Index", "Taxi");
                }
                TempData["code"] = code;
                return View(taxiPeople);
            }
            TempData["errorMessage"] = Messages.TaxiNotFound;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpPost]
        public ActionResult ConfirmJoin(int id, string code, TaxiViewModel taxiModel)
        {
            try
            {
                taxiModel.ConfirmJoin(id, code);
            }
            catch
            {
                TempData["errorMessage"] = Messages.ConfirmFailed;
                return View(taxiModel.GetEntityBy<TaxiPeople>("Id", id));
            }
            TempData["successMessage"] = Messages.ConfirmSucceed;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpPost]
        public ActionResult BugReport(string name, string description)
        {
            var encodedResponse = Request.Form["g-Recaptcha-Response"];
            if (!Validators.IsCaptchaValid(encodedResponse))
            {
                TempData["errorMessage"] = Messages.NotValidCaptcha;
                return RedirectToAction("Index", "Taxi");
            }

            if(name != null && description != null)
            {
                var body = string.Format("<p>Zgłaszający: {0}</p><p>Opis błędu: {1}</p>", name, description);
                var client = new Email.Emailer("taksii.test@gmail.com", "taksii.test@gmail.com", body, "Zgłoszenie błędu TakSii");
                client.SendEmail();

                TempData["successMessage"] = Messages.BugReported;
                return RedirectToAction("Index", "Taxi");
            }

            TempData["successMessage"] = Messages.BugFailed;
            return RedirectToAction("Index", "Taxi");
        }
    }
}
