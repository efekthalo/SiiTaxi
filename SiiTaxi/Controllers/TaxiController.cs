﻿using SiiTaxi.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Web.Mvc;
using SiiTaxi.Providers;
using System.Linq;
using SiiTaxi.Email;

namespace SiiTaxi.Controllers
{
    public class TaxiController : Controller
    {
        private readonly SiiTaxiEntities _context = new SiiTaxiEntities();

        [HttpPost]
        public ActionResult New(string ownerName, string ownerPhone, string time,
            string ownerEmail, string przejazdFrom, string przejazdTo,
            List<string> adds, int approver)
        {
            IQueryable<Approvers> approvers = _context.Approvers;
            var isBigTaxi = Request.Form["IsBigTaxi"] == "on";
            var order = Request.Form["order"] == "on";
            TempData["formData"] = Request.Form;

            var encodedResponse = Request.Form["g-Recaptcha-Response"];
            if (!Validators.IsCaptchaValid(encodedResponse))
            {
                TempData["errorMessage"] = Messages.NotValidCaptcha;
                return View(approvers);
            }
            if (!Validators.IsEmailValid(ownerEmail, true))
            {
                TempData["errorMessage"] = Messages.NotValidCompanyEmail;
                return View(approvers);
            }
            if (!Validators.IsPhoneValid(ownerPhone))
            {
                TempData["errorMessage"] = Messages.NotValidPhone;
                return View(approvers);
            }

            DateTime parsedTime;
            DateTime.TryParseExact(time, "dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedTime);

            if (parsedTime < DateTime.Now)
            {
                TempData["errorMessage"] = Messages.NotValidDate;
                return View(approvers);
            }

            try
            {
                var owner = _context.People.FirstOrDefault(x => x.Email == ownerEmail);
                if (owner != null)
                {
                    TryUpdateModel(owner);
                }
                else
                {
                    owner = _context.People.Add(new People() { Name = ownerName, Phone = ownerPhone, Email = ownerEmail });
                }
                _context.SaveChanges();

                var taxi = new Taxi
                {
                    From = przejazdFrom,
                    To = przejazdTo,
                    Time = parsedTime,
                    Owner = owner.PeopleId,
                    Approver = approver,
                    IsBigTaxi = isBigTaxi,
                    Order = !order,
                    ConfirmCode = Guid.NewGuid().ToString()
                };

                taxi = _context.Taxi.Add(taxi);
                _context.SaveChanges();

                if (adds != null)
                {
                    foreach (var add in adds.Distinct())
                    {
                        if (add == ownerEmail)
                        {
                            continue;
                        }

                        var other = _context.People.FirstOrDefault(x => x.Email == add);
                        if (other != null)
                        {
                            TryUpdateModel(other);
                        }
                        else
                        {
                            other = _context.People.Add(new People() { Name = "", Email = add });
                        }

                        var taxiPeople = new TaxiPeople
                        {
                            TaxiId = taxi.TaxiId,
                            PeopleId = other.PeopleId,
                            ConfirmCode = Guid.NewGuid().ToString(),
                            IsConfirmed = true
                        };

                        _context.TaxiPeople.Add(taxiPeople);
                    }

                    _context.SaveChanges();
                }

                SendConfirmEmail(taxi);
            }
            catch (Exception e)
            {
                TempData["errorMessage"] = Messages.DatabaseError;
                return View(approvers);
            }

            TempData["successMessage"] = Messages.AddNewTaxiSuccess;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpGet]
        public ActionResult New()
        {
            IQueryable<Approvers> approvers = _context.Approvers;
            return View(approvers);
        }

        public ActionResult Join(int id)
        {
            var maxInTaxi = 3;
            TempData["formData"] = Request.Form;

            var taxi = _context.Taxi.Find(id);
            if (taxi != null)
            {
                if (taxi.Time < DateTime.Now)
                {
                    TempData["errorMessage"] = Messages.TaxiExpired;
                    return RedirectToAction("Index", "Taxi");
                }

                if (taxi.IsBigTaxi)
                    maxInTaxi = 6;

                TempData["resources"] = taxi.TaxiPeople.Count(x => !x.ResourceOnly) >= maxInTaxi? true : false;
                return View(taxi);
            }

            TempData["errorMessage"] = Messages.TaxiNotFound;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpPost]
        public ActionResult Join(int id, string name, string phone, string email)
        {
            var resourceOnly = Request.Form["resourceOnly"] == "on";
            var maxInTaxi = 3;

            var taxi = _context.Taxi.Find(id);
            if (taxi == null)
            {
                TempData["errorMessage"] = Messages.TaxiNotFound;
                return RedirectToAction("Index", "Taxi");
            }

            if (taxi.IsBigTaxi)
                maxInTaxi = 6;

            if (taxi.Time < DateTime.Now)
            {
                TempData["errorMessage"] = Messages.TaxiExpired;
                return RedirectToAction("Index", "Taxi");
            }

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
            if (taxi.TaxiPeople.Count(x => !x.ResourceOnly) >= maxInTaxi && !resourceOnly)
            {
                TempData["errorMessage"] = Messages.TaxiFull;
                return RedirectToAction("Index", "Taxi");
            }
            if (email == taxi.People.Email || taxi.TaxiPeople.Any(x => x.People.Email == email))
            {
                TempData["errorMessage"] = Messages.JoinedAlready;
                return RedirectToAction("Index", "Taxi");
            }

            try
            {
                var other = _context.People.FirstOrDefault(x => x.Email == email);
                if (other != null)
                {
                    TryUpdateModel(other);
                }
                else
                {
                    other = _context.People.Add(new People() { Name = "", Phone = phone, Email = email });
                }
                _context.SaveChanges();

                var taxiPeople = new TaxiPeople { TaxiId = id, PeopleId = other.PeopleId, ConfirmCode = Guid.NewGuid().ToString(), ResourceOnly = resourceOnly };
                taxiPeople = _context.TaxiPeople.Add(taxiPeople);
                _context.SaveChanges();

                SendJoinEmail(taxiPeople, false);
            }
            catch
            {
                TempData["errorMessage"] = Messages.DatabaseError;
                return View(taxi);
            }
            if (resourceOnly)
            {
                TempData["successMessage"] = Messages.IncludeResourcesSuccess;
                return RedirectToAction("Index", "Taxi");
            }
            TempData["successMessage"] = Messages.IncludeTaxiSuccess;
            return RedirectToAction("Index", "Taxi");
        }

        public ActionResult Index(DateTime? date = null)
        {
            var localDate = (date == null || date < DateTime.Now ? DateTime.Now : (DateTime)date);
            IQueryable<Taxi> taxis = _context.Taxi.Where(x => (x.Time.Year == localDate.Year) && (x.Time.Month == localDate.Month) && (x.Time.Day == localDate.Day));
            return View(taxis);
        }

        [HttpGet]
        [ActionName("Confirm")]
        public ActionResult ConfirmGet(int id, string code)
        {
            var taxi = _context.Taxi.Find(id);
            if (taxi != null && taxi.ConfirmCode == code)
            {
                if (taxi.Time < DateTime.Now)
                {
                    TempData["errorMessage"] = Messages.TaxiExpired;
                    return RedirectToAction("Index", "Taxi");
                }
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
        [ActionName("Confirm")]
        public ActionResult ConfirmPost(int id, string code)
        {
            var taxi = _context.Taxi.Find(id);
            if (taxi != null)
            {
                try
                {
                    if (taxi.ConfirmCode == code)
                    {
                        taxi.IsConfirmed = true;
                        _context.SaveChanges();
                        SendNotification(taxi);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                catch
                {
                    TempData["errorMessage"] = Messages.ConfirmFailed;
                    return View(taxi);
                }

                TempData["successMessage"] = Messages.ConfirmSucceed;
                return RedirectToAction("Index", "Taxi");
            }

            TempData["successMessage"] = Messages.TaxiNotFound;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpGet]
        [ActionName("ConfirmJoin")]
        public ActionResult ConfirmJoinGet(int id, string code)
        {
            var taxiPeople = _context.TaxiPeople.Find(id);
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
        [ActionName("ConfirmJoin")]
        public ActionResult ConfirmJoinPost(int id, string code)
        {
            var taxiPeople = _context.TaxiPeople.Find(id);
            if (taxiPeople != null)
            {
                try
                {
                    if (taxiPeople.ConfirmCode == code)
                    {
                        taxiPeople.IsConfirmed = true;
                        _context.SaveChanges();
                        SendJoinEmail(taxiPeople, true);
                    }
                    else
                    {
                        TempData["errorMessage"] = Messages.IncorrectCode;
                        return View(taxiPeople);
                    }
                }
                catch
                {
                    TempData["errorMessage"] = Messages.ConfirmFailed;
                    return View(taxiPeople);
                }

                TempData["successMessage"] = Messages.ConfirmSucceed;
                return RedirectToAction("Index", "Taxi");
            }

            TempData["successMessage"] = Messages.TaxiPeopleNotFound;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpPost]
        [ActionName("BugReport")]
        public ActionResult BugReport(string name, string description)
        {
            var encodedResponse = Request.Form["g-Recaptcha-Response"];
            if (!Validators.IsCaptchaValid(encodedResponse))
            {
                TempData["errorMessage"] = Messages.NotValidCaptcha;
                return RedirectToAction("Index", "Taxi");
            }

            if (name != null && description != null)
            {
                var body = $"<p>Zgłaszający: {name}</p><p>Opis błędu: {description}</p>";
                var client = new Emailer(ConfigurationManager.AppSettings["adminEmail"], ConfigurationManager.AppSettings["adminEmail"], body, "Zgłoszenie błędu TakSii");
                client.SendEmail();

                TempData["successMessage"] = Messages.BugReported;
                return RedirectToAction("Index", "Taxi");
            }

            TempData["successMessage"] = Messages.BugFailed;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpGet]
        [ActionName("Remove")]
        public ActionResult RemoveGet(int id, string code)
        {
            var taxi = _context.Taxi.Find(id);
            if (taxi != null)
            {
                TempData["code"] = code;
                return View(taxi);
            }

            TempData["errorMessage"] = Messages.TaxiNotFound;
            return RedirectToAction("Index", "Taxi");
        }

        [HttpPost]
        [ActionName("Remove")]
        public ActionResult RemovePost(int id, string code)
        {
            var taxi = _context.Taxi.Find(id);
            if (taxi != null)
            {
                try
                {
                    var joiner = taxi.TaxiPeople.FirstOrDefault(x => x.ConfirmCode == code);

                    if (taxi.ConfirmCode == code)
                    {
                        if (taxi.IsOrdered)
                        {
                            TempData["errorMessage"] = Messages.TaxiOrdered;
                            return RedirectToAction("Index", "Taxi");
                        }

                        SendRemoveToJoiners(taxi);
                        _context.Taxi.Remove(taxi);
                    }
                    else if (joiner != null)
                    {
                        SendRemoveToOwner(taxi, joiner);
                        _context.TaxiPeople.Remove(joiner);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    _context.SaveChanges();

                }
                catch
                {
                    TempData["errorMessage"] = Messages.RemoveFailed;
                    return View(taxi);
                }

                TempData["successMessage"] = Messages.RemoveSucceed;
                return RedirectToAction("Index", "Taxi");
            }

            TempData["errorMessage"] = Messages.TaxiNotFound;
            return RedirectToAction("Index", "Taxi");
        }

        public bool SendConfirmEmail(Taxi taxi)
        {
            var template = new ConfirmTemplate
            {
                Taxi = taxi
            };

            var body = template.TransformText();
            var owner = taxi.People.Email;
            var approver = _context.Approvers.Find(taxi.Approver)?.People.Email;
            if (owner != null && approver != null)
            {
                var client = new Emailer(ConfigurationManager.AppSettings["adminEmail"], owner, body, "Potwierdzenie taksówki - TakSii", approver);
                client.SendEmail();
                return true;
            }

            return false;
        }

        public bool SendJoinEmail(TaxiPeople entity, bool confirmed)
        {
            if (entity.TaxiId != null)
            {
                string body;
                string subject;

                if (!confirmed)
                {
                    var template = new ConfirmJoinTemplate
                    {
                        TaxiPeople = entity
                    };

                    subject = "Potwierdzenie dołączenia - TakSii";
                    body = template.TransformText();
                }
                else
                {
                    if (entity.ResourceOnly) {
                        var template = new ResourceOnlyTemplate
                        {
                            Taxi = entity.Taxi
                        };
                        subject = "Kontakt do właściciela - TakSii";
                        body = template.TransformText();
                    }
                    else {
                        var template = new OwnerContactTemplate
                        {
                            Taxi = entity.Taxi
                        };
                        subject = "Kontakt do właściciela - TakSii";
                        body = template.TransformText();
                    }
                }

                var joiner = entity.People.Email;
                var owner = entity.Taxi.People.Email;
                if (joiner != null && owner != null)
                {
                    var client = new Emailer(ConfigurationManager.AppSettings["adminEmail"], joiner, body, subject, owner);
                    client.SendEmail();
                    return true;
                }
            }

            return false;
        }

        public void SendRemoveToJoiners(Taxi taxi)
        {
            foreach (var taxiPeople in taxi.TaxiPeople)
            {
                var codeTemplate = new SendRemoveToJoinersTemplate
                {
                    Taxi = taxi
                };
                var body = codeTemplate.TransformText();

                var client = new Emailer(ConfigurationManager.AppSettings["adminEmail"], taxiPeople.People.Email, body, "Taksówka została odwołana - TakSii");
                client.SendEmail();
            }
        }

        private void SendRemoveToOwner(Taxi taxi, TaxiPeople joiner)
        {
            var codeTemplate = new SendRemoveToOwnerTemplate
            {
                Taxi = taxi,
                Joiner = joiner
            };
            var body = codeTemplate.TransformText();

            var client = new Emailer(ConfigurationManager.AppSettings["adminEmail"], taxi.People.Email, body, "Wypisano " + joiner.People.Email + " z taksówki - TakSii");
            client.SendEmail();
        }

        private void SendNotification(Taxi taxi)
        {
            var codeTemplate = new SendNotificationTemplate
            {
                Taxi = taxi,
            };
            var body = codeTemplate.TransformText();

            var client = new Emailer(ConfigurationManager.AppSettings["adminEmail"], "aguja@pl.sii.eu", body, "Nowa potwierdzona taksówka - TakSii");
            client.SendEmail();
        }
    }
}
