using IdentityServer4;
using IdentityServer4.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb
{
	public static class Config
	{
		public static IEnumerable<IdentityResource> Ids =>
			new List<IdentityResource>
			{
				new IdentityResources.OpenId(),
				new IdentityResources.Email(),
				new IdentityResources.Profile()
			};

		public static IEnumerable<ApiResource> Apis =>
			new List<ApiResource>
			{
				new ApiResource("api1", "OpenGameMonitor API")
			};

		public static IEnumerable<Client> Clients =>
			new List<Client>
			{
				// machine to machine client
				new Client
				{
					ClientId = "client",
					ClientSecrets = { new Secret("secret".Sha256()) },

					AllowedGrantTypes = GrantTypes.ClientCredentials,
					// scopes that client has access to
					AllowedScopes = { "api1" }
				},
				// interactive ASP.NET Core MVC client
				new Client
				{
					ClientId = "mvc",
					ClientSecrets = { new Secret("secret".Sha256()) },

					AllowedGrantTypes = GrantTypes.Code,
					RequireConsent = false,
					RequirePkce = true,
				
					// where to redirect to after login
					RedirectUris = { "http://localhost:5002/signin-oidc" },

					// where to redirect to after logout
					PostLogoutRedirectUris = { "http://localhost:5002/signout-callback-oidc" },

					AllowedScopes = new List<string>
					{
						IdentityServerConstants.StandardScopes.OpenId,
						IdentityServerConstants.StandardScopes.Profile,
						"api1"
					},

					AllowOfflineAccess = true
				},
				// Angular Client
				new Client
				{
					ClientId = "OpenGameMonitorPanel",
					ClientName = "Angular Client",
					AllowedGrantTypes = GrantTypes.Implicit,
					RequirePkce = true,
					RequireClientSecret = false,
					RequireConsent = false,

					AllowAccessTokensViaBrowser = true,

					RedirectUris =           { "http://localhost:4200/auth-callback" },
					PostLogoutRedirectUris = { "http://localhost:4200/" },
					AllowedCorsOrigins =     { "http://localhost:4200" },

					AllowedScopes =
					{
						IdentityServerConstants.StandardScopes.OpenId,
						IdentityServerConstants.StandardScopes.Profile,
						IdentityServerConstants.StandardScopes.Email,
						"api1"
					}
				}
			};
	}
}
