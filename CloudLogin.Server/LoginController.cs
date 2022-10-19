﻿using System.Web;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.Twitter;
using AngryMonkey.Cloud.Login.DataContract;
using AuthenticationProperties = Microsoft.AspNetCore.Authentication.AuthenticationProperties;
using Microsoft.Azure.Cosmos.Linq;
using System.Configuration;

namespace AngryMonkey.Cloud.Login.Controllers
{
	[Route("CloudLogin")]
	[ApiController]
	public class LoginController : BaseController
	{
		[HttpGet("Login/{identity}")]
		public async Task<ActionResult?> Login(string identity, string input, string redirectUri, bool keepMeSignedIn)
		{
			AuthenticationProperties properties = new()
			{
				RedirectUri = $"/cloudlogin/result?redirectUri={HttpUtility.UrlEncode(redirectUri)}",
				ExpiresUtc = keepMeSignedIn ? DateTimeOffset.UtcNow.AddMonths(3) : null,
				IsPersistent = keepMeSignedIn,
			};

			properties.SetParameter("login_hint", input);

			return identity.Trim().ToLower() switch
			{
				"microsoft" => Challenge(properties, MicrosoftAccountDefaults.AuthenticationScheme),
				"google" => Challenge(properties, GoogleDefaults.AuthenticationScheme),
				"facebook" => Challenge(properties, FacebookDefaults.AuthenticationScheme),
				"twitter" => Challenge(properties, TwitterDefaults.AuthenticationScheme),
				_ => null,
			};
		}

		[HttpGet("Login/CustomLogin")]
		public async Task<ActionResult<string>?> CustomLogin(string userInfo, bool keepMeSignedIn, string redirectUri)
		{
			Dictionary<string, string> userDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(userInfo);
			
			AuthenticationProperties properties = new()
			{
				ExpiresUtc = keepMeSignedIn ? DateTimeOffset.UtcNow.AddMonths(3) : null,
				IsPersistent = keepMeSignedIn
			};

			string firstName = userDictionary["FirstName"];
			string lastName = userDictionary["LastName"];
			string displayName = userDictionary["DisplayName"];

			if (Configuration.Cosmos == null)
			{
				firstName ??= "Guest";
				lastName ??= "User";
			}

			displayName ??= $"{firstName} {lastName}";

			//create claimsIdentity
			var claimsIdentity = new ClaimsIdentity(new[] {

				new Claim(ClaimTypes.NameIdentifier, userDictionary["UserId"]),
				new Claim(ClaimTypes.GivenName, firstName),
				new Claim(ClaimTypes.Surname, lastName),
				new Claim(ClaimTypes.Name, displayName)

			}, ".");

			if (userDictionary["Type"].ToLower() == "phonenumber")
				claimsIdentity.AddClaim(new Claim(ClaimTypes.MobilePhone, userDictionary["Input"]));
			if (userDictionary["Type"].ToLower() == "emailaddress")
				claimsIdentity.AddClaim(new Claim(ClaimTypes.Email, userDictionary["Input"]));


			var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

			await HttpContext.SignInAsync(claimsPrincipal, properties);


			return Redirect($"/cloudlogin/result?redirecturi={HttpUtility.UrlEncode(redirectUri)}");
		}

		[HttpGet("Result")]
		public async Task<ActionResult<string>> LoginResult(string redirectUri)
		{
			CloudUser user = JsonConvert.DeserializeObject<CloudUser>(HttpContext.Request.Cookies["CloudUser"]);

			AuthenticationProperties properties = new();
			string firstName = user.FirstName;
			string lastName = user.LastName;
			string displayName = user.DisplayName;

			if (Configuration.Cosmos == null)
			{
				firstName ??= "Guest";
				lastName ??= "User";
			}

			displayName ??= $"{firstName} {lastName}";
			var claimsIdentity = new ClaimsIdentity(new[] {

				new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
				new Claim(ClaimTypes.GivenName, firstName),
				new Claim(ClaimTypes.Surname, lastName),
				new Claim(ClaimTypes.Name, displayName),
				new Claim(ClaimTypes.Hash, "Cloud Login")

			}, ".");
			if (user.EmailAddresses.FirstOrDefault() == null)
			{
				claimsIdentity.AddClaim(new Claim(ClaimTypes.MobilePhone, user.PhoneNumbers.Where(key => key.IsPrimary == true).FirstOrDefault().Input));
			}
			else
				claimsIdentity.AddClaim(new Claim(ClaimTypes.Email, user.EmailAddresses.Where(key => key.IsPrimary == true).FirstOrDefault().Input));

			var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

			await HttpContext.SignInAsync(claimsPrincipal, properties);

			return Redirect(redirectUri);
		}

		[HttpGet("Logout")]
		public async Task<ActionResult> Logout()
		{
			await HttpContext.SignOutAsync();

			return Redirect("/");
		}
	}
}
