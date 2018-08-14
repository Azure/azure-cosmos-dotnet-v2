using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Configuration;
using System.Threading.Tasks;

namespace mykeyvault
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }
        private static async Task MainAsync(string[] args)
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

            try
            {
                var keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

                var secret = await keyVaultClient.GetSecretAsync("https://rafatkeyvalut.vault.azure.net/secrets/testsecret/{YourId}")
                    .ConfigureAwait(false);

                Console.WriteLine( $"Secret: {secret.Value}");

            }
            catch (Exception exp)
            {
                Console.WriteLine( $"Something went wrong: {exp.Message}");
            }

            Console.WriteLine(azureServiceTokenProvider.PrincipalUsed != null ? $"Principal Used: {azureServiceTokenProvider.PrincipalUsed}" : string.Empty);
            Console.Read();
        }
    } //~class
} //~nameSpace
