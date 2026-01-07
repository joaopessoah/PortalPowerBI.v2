using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using System.Collections.Generic;

namespace PortalPowerBI.Services
{
    public class PbiService
    {
        private readonly IConfiguration _configuration;

        public PbiService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // --- ATUALIZADO: Aceita username e roleName para RLS ---
        public async Task<EmbedConfig> GetEmbedTokenAsync(string reportIdString, string groupIdString, string? username = null, string? roleName = null)
        {
            var azureAd = _configuration.GetSection("AzureAd");

            // Fallback se o groupId vier vazio
            if (string.IsNullOrEmpty(groupIdString))
                groupIdString = _configuration["PowerBi:WorkspaceId"] ?? "";

            if (string.IsNullOrEmpty(groupIdString)) throw new Exception("Group ID não informado.");

            var workspaceId = new Guid(groupIdString);
            var reportId = new Guid(reportIdString);

            var clientId = azureAd["ClientId"];
            var clientSecret = azureAd["ClientSecret"];
            var authorityUrl = azureAd["AuthorityUrl"];
            var tenantId = azureAd["TenantId"];
            var scope = azureAd.GetSection("Scope").Get<string[]>();

            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri(authorityUrl + tenantId))
                .Build();

            var authResult = await app.AcquireTokenForClient(scope).ExecuteAsync();
            var tokenCredentials = new TokenCredentials(authResult.AccessToken, "Bearer");

            using (var client = new PowerBIClient(new Uri("https://api.powerbi.com/"), tokenCredentials))
            {
                var report = await client.Reports.GetReportInGroupAsync(workspaceId, reportId);
                
                // Configuração base do token
                var tokenRequest = new GenerateTokenRequest(accessLevel: "View");

                // --- LÓGICA DE RLS (Row Level Security) ---
                // Se o controller enviou um usuário e uma role, aplicamos o filtro na identidade
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(roleName))
                {
                    tokenRequest.Identities = new List<EffectiveIdentity>
                    {
                        new EffectiveIdentity(
                            username: username,                // E-mail do usuário
                            roles: new List<string> { roleName }, // Nome da Role (ex: "master" ou "Usuario")
                            datasets: new List<string> { report.DatasetId } // Dataset alvo
                        )
                    };
                }

                var embedToken = await client.Reports.GenerateTokenInGroupAsync(workspaceId, reportId, tokenRequest);

                return new EmbedConfig
                {
                    EmbedToken = embedToken.Token,
                    EmbedUrl = report.EmbedUrl,
                    ReportId = report.Id.ToString(),
                    ReportName = report.Name
                };
            }
        }
        
        // --- Método de Permissão (Mantido para compatibilidade, caso precise usar novamente) ---
        public async Task SetDatasetPermissions(string groupId, string reportId)
        {
            var azureAd = _configuration.GetSection("AzureAd");
            var clientId = azureAd["ClientId"];
            var tenantId = azureAd["TenantId"];
            var clientSecret = azureAd["ClientSecret"];
            var authorityUrl = azureAd["AuthorityUrl"];
            var scope = azureAd.GetSection("Scope").Get<string[]>();
            
            if (string.IsNullOrEmpty(clientId)) return;

            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri(authorityUrl + tenantId))
                .Build();

            var authResult = await app.AcquireTokenForClient(scope).ExecuteAsync();
            var tokenCredentials = new TokenCredentials(authResult.AccessToken, "Bearer");

            using (var client = new PowerBIClient(new Uri("https://api.powerbi.com/"), tokenCredentials))
            {
                var report = await client.Reports.GetReportInGroupAsync(new Guid(groupId), new Guid(reportId));
                var datasetId = report.DatasetId;

                if (datasetId == null) return;

                var userEmail = $"app:{clientId}@{tenantId}";

                var userAccessRight = new PostDatasetUserAccess
                {
                    Identifier = userEmail,
                    DatasetUserAccessRight = "ReadAndReshare", 
                    PrincipalType = PrincipalType.App
                };

                try
                {
                    await client.Datasets.PostDatasetUserInGroupAsync(
                        new Guid(groupId), 
                        datasetId, 
                        userAccessRight 
                    );
                }
                catch { /* Ignora erros se já existir */ }
            }
        }
    }

    public class EmbedConfig
    {
        public string? EmbedToken { get; set; }
        public string? EmbedUrl { get; set; }
        public string? ReportId { get; set; }
        public string? ReportName { get; set; }
    }
}