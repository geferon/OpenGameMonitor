using IdentityServer4.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace OpenGameMonitorWeb.Utils
{
	public static class CredentialsHelper
	{
		private const string KeyType = "KeyType";
		private const string KeyTypeKeyFile = "KeyFile";
		private const string KeyTypeKeyStore = "KeyStore";
		private const string KeyTypeTemporary = "Temporary";
		private const string KeyFilePath = "KeyFilePath";
		private const string KeyFilePassword = "KeyFilePassword";
		private const string KeyStoreIssuer = "KeyStoreIssuer";

		public static X509Certificate2 GetCredentialFromConfig(IConfigurationSection options, ILogger? logger)
		{
			string keyType = options.GetValue<string>(KeyType);
			logger?.LogDebug($"CredentialsHelper keyType is {keyType}");

			switch (keyType)
			{
				case KeyTypeTemporary:
					logger?.LogDebug($"CredentialsHelper adding Temporary Signing Credential");
					//return GetCertificateTemporary();
					// TODO
					break;

				case KeyTypeKeyFile:
					return GetCertificateFromFile(options, logger);

				case KeyTypeKeyStore:
					return GetCertificateFromStore(options, logger);
			}

			throw new Exception($"keyType {keyType} not found");
		}

		public static SecurityKey GetSecurityKeyFromConfig(IConfigurationSection options, ILogger? logger)
		{
			string keyType = options.GetValue<string>(KeyType);

			switch (keyType)
			{
				case KeyTypeTemporary:
					return GenerateRSADev();

				case KeyTypeKeyFile:
				case KeyTypeKeyStore:
					return new X509SecurityKey(GetCredentialFromConfig(options, logger));
			}

			throw new Exception($"keyType {keyType} not found");
		}

		internal class TemporaryRsaKey
		{
			public string KeyId { get; set; }
			public RSAParameters Parameters { get; set; }
		}

		internal class RsaKeyContractResolver : DefaultContractResolver
		{
			protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
			{
				var property = base.CreateProperty(member, memberSerialization);

				property.Ignored = false;

				return property;
			}
		}

		private static RsaSecurityKey GenerateRSADev()
		{
			var filename = Path.Combine(Directory.GetCurrentDirectory(), "tempkey.rsa");

			if (File.Exists(filename))
			{
				var keyFile = File.ReadAllText(filename);
				var tempKey = JsonConvert.DeserializeObject<TemporaryRsaKey>(keyFile, new JsonSerializerSettings { ContractResolver = new RsaKeyContractResolver() });

				return CryptoHelper.CreateRsaSecurityKey(tempKey.Parameters, tempKey.KeyId);
			}
			else
			{
				var key = CryptoHelper.CreateRsaSecurityKey();

				RSAParameters parameters;

				if (key.Rsa != null)
				{
					parameters = key.Rsa.ExportParameters(includePrivateParameters: true);
				}
				else
				{
					parameters = key.Parameters;
				}

				var tempKey = new TemporaryRsaKey
				{
					Parameters = parameters,
					KeyId = key.KeyId
				};

				File.WriteAllText(filename, JsonConvert.SerializeObject(tempKey, new JsonSerializerSettings { ContractResolver = new RsaKeyContractResolver() }));

				return key;
			}
		}

		// Not working for some reason
		private static X509Certificate2 GenerateX509Dev()
		{
			using (RSA parent = RSA.Create(4096))
			using (RSA rsa = RSA.Create(2048))
			{
				CertificateRequest parentReq = new CertificateRequest(
					"CN=Experimental Issuing Authority",
					parent,
					HashAlgorithmName.SHA256,
					RSASignaturePadding.Pkcs1);

				parentReq.CertificateExtensions.Add(
					new X509BasicConstraintsExtension(true, false, 0, true));

				parentReq.CertificateExtensions.Add(
					new X509SubjectKeyIdentifierExtension(parentReq.PublicKey, false));

				using (X509Certificate2 parentCert = parentReq.CreateSelfSigned(
					DateTimeOffset.UtcNow.AddDays(-45),
					DateTimeOffset.UtcNow.AddDays(365)))
				{
					CertificateRequest req = new CertificateRequest(
						"CN=Valid-Looking Timestamp Authority",
						rsa,
						HashAlgorithmName.SHA256,
						RSASignaturePadding.Pkcs1);

					req.CertificateExtensions.Add(
						new X509BasicConstraintsExtension(false, false, 0, false));

					req.CertificateExtensions.Add(
						new X509KeyUsageExtension(
							X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
							false));

					req.CertificateExtensions.Add(
						new X509EnhancedKeyUsageExtension(
							new OidCollection
							{
					new Oid("1.3.6.1.5.5.7.3.8")
							},
							true));

					req.CertificateExtensions.Add(
						new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

					using (X509Certificate2 cert = req.Create(
						parentCert,
						DateTimeOffset.UtcNow.AddDays(-1),
						DateTimeOffset.UtcNow.AddDays(90),
						new byte[] { 1, 2, 3, 4 }))
					{
						return cert;
					}
				}
			}
		}

		// Copied from IdentityServer4, let's just share the credentials and call it a day
		private static X509Certificate2 GetCertificateTemporary()
		{
			var filename = Path.Combine(Directory.GetCurrentDirectory(), "tempkey.x509");

			if (File.Exists(filename))
			{
				X509Certificate2 tempCert = new X509Certificate2(filename);
				return tempCert;
			}
			else
			{
				X509Certificate2 tempCert = GenerateX509Dev();
				File.WriteAllBytes(filename, tempCert.Export(X509ContentType.Cert));
				return tempCert;
			}
		}

		private static X509Certificate2 GetCertificateFromStore(IConfigurationSection options, ILogger logger)
		{
			var keyIssuer = options.GetValue<string>(KeyStoreIssuer);
			logger?.LogDebug($"CredentialsHelper adding key from store by {keyIssuer}");

			X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
			store.Open(OpenFlags.ReadOnly);

			var certificates = store.Certificates.Find(X509FindType.FindByIssuerName, keyIssuer, true);

			if (certificates.Count == 0)
			{
				logger?.LogError("A matching key couldn't be found in the store");
				throw new Exception("A matching key couldn't be found in the store");
			}

			return certificates[0];
		}

		private static X509Certificate2 GetCertificateFromFile(IConfigurationSection options, ILogger logger)
		{
			var keyFilePath = options.GetValue<string>(KeyFilePath);
			var keyFilePassword = options.GetValue<string>(KeyFilePassword);

			if (!File.Exists(keyFilePath))
			{
				logger?.LogError($"CredentialsHelper cannot find key file {keyFilePath}");
				throw new Exception($"Cannot find key file {keyFilePath}");
			}

			logger?.LogDebug($"CredentialsHelper adding key from file {keyFilePath}");
			return new X509Certificate2(keyFilePath, keyFilePassword);
		}
	}
}
