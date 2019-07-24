using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using VSOWorkBot.Extensions;
using VSOWorkBot.Models;

namespace VSOWorkBot.Controllers
{
	[Route("")]
	[AllowAnonymous]
	public class AuthenticationController : Controller
	{
		private readonly AuthHelper authHelper;

		private readonly string sessionKey = "UserData";

		public AuthenticationController(AuthHelper authHelper)
		{
			this.authHelper = authHelper;
		}

		[HttpGet]
		[Route("signin/{conversationId}/{userId}")]
		public IActionResult SignIn(string conversationId, string userId)
		{
			// Instruct the middleware corresponding to the requested external identity
			// provider to redirect the user agent to its own authorization endpoint.
			// Note: the authenticationScheme parameter must match the value configured in Startup.cs
			if (String.IsNullOrEmpty(conversationId) || String.IsNullOrEmpty(userId))
			{
				return BadRequest(new ArgumentException("ConversationId or UserId cannot be empty"));
			}
			HttpContext.Session.SetString(sessionKey, JsonConvert.SerializeObject(new UserSession { ConversationId = conversationId, UserId = userId }));
			return Redirect(authHelper.GetAuthorizeUrl());
		}

		[HttpGet]
		[Route("oauth-callback")]
		public async Task<IActionResult> GetAsync([FromQuery(Name = "code")] string code)
		{
			var sessionData = HttpContext.Session.GetString(sessionKey);
			if (String.IsNullOrEmpty(sessionData) || String.IsNullOrEmpty(code))
			{
				return BadRequest(new ArgumentException("Invalid session. Please re-authenticate"));
			}
			var vsoToken = await authHelper.GetTokenAsync(code);
			if (String.IsNullOrEmpty(vsoToken.AccessToken))
			{
				return BadRequest(new ArgumentException("Something went. Please re-authenticate"));
			}
			var userSession = JsonConvert.DeserializeObject<UserSession>(HttpContext.Session.GetString(sessionKey));
			await authHelper.SaveTokenAsync(userSession.ConversationId, userSession.UserId, vsoToken.AccessToken);
			return File("loggedin.html", "text/html");
		}
	}
}