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
		public static IEnumerable<IdentityResource> GetIdentityResources()
		{
			return new List<IdentityResource>
			{
				new IdentityResources.OpenId(),
				new IdentityResources.Profile(),
				new IdentityResources.Email()
			};
		}

		public static IEnumerable<ApiResource> GetApis()
		{
			return new List<ApiResource>
			{
				//new ApiResource("api", "OpenGameMonitor API"),
				new ApiResource
				{
					Name = "api",
					Description = "OpenGameMonitor API",
					Scopes =
					{
						new Scope("api"),
						new Scope("OpenGameMonitorWebAPI")
					},
					UserClaims = {
						IdentityModel.JwtClaimTypes.Name,
						IdentityModel.JwtClaimTypes.Email,
						IdentityModel.JwtClaimTypes.Role,
						System.Security.Claims.ClaimTypes.Role,
						System.Security.Claims.ClaimTypes.NameIdentifier
					}
				}
			};
		}

		public static IEnumerable<Client> GetClients()
		{
			return new List<Client>
			{
				// machine to machine client
				new Client
				{
					ClientId = "client",
					ClientSecrets = { new Secret("secret".Sha256()) },

					AllowedGrantTypes = GrantTypes.ClientCredentials,
					// scopes that client has access to
					AllowedScopes = { "api" }
				},
				// interactive ASP.NET Core MVC client
				/*
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
						"api"
					},

					AllowOfflineAccess = true
				},
				*/
				// Angular Client
				new Client
				{
					ClientId = "OpenGameMonitorPanel",
					ClientName = "Angular Client",
					//AllowedGrantTypes = GrantTypes.Implicit,
					AllowedGrantTypes = GrantTypes.Code,
					AllowOfflineAccess = true,
					RequireClientSecret = false,
					//RequirePkce = true,
					RequireConsent = false,

					AllowAccessTokensViaBrowser = true,

					ClientUri =              "http://localhost:4200",
					RedirectUris =           { "http://localhost:4200/auth-callback", "https://localhost:5001/authentication/login-callback" },
					PostLogoutRedirectUris = { "http://localhost:4200/", "https://localhost:5001/" },
					AllowedCorsOrigins =     { "http://localhost:4200", "https://localhost:5001" },

					AllowedScopes =
					{
						"api",
						"OpenGameMonitorWebAPI",
						IdentityServerConstants.StandardScopes.OpenId,
						IdentityServerConstants.StandardScopes.Profile,
						IdentityServerConstants.StandardScopes.Email
					}
				}
			};
		}
	}
}
