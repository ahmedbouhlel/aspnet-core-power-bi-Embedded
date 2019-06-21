using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.PowerBI.Api.V2;
using Microsoft.PowerBI.Api.V2.Models;
using Microsoft.Rest;
using PowerBIEmbedded_AppOwnsData.Context;
using PowerBIEmbedded_AppOwnsData.Models;
using PowerBIEmbedded_AppOwnsData.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace PowerBIEmbedded_AppOwnsData.Controllers
{
    public class HomeController : Controller
    {
        private static readonly string Username = ConfigurationManager.AppSettings["pbiUsername"];
        private static readonly string Password = ConfigurationManager.AppSettings["pbiPassword"];
        private static readonly string AuthorityUrl = ConfigurationManager.AppSettings["authorityUrl"];
        private static readonly string ResourceUrl = ConfigurationManager.AppSettings["resourceUrl"];
        private static readonly string ApplicationId = ConfigurationManager.AppSettings["ApplicationId"];
        private static readonly string ApiUrl = ConfigurationManager.AppSettings["apiUrl"];
        private static readonly string WorkspaceId = ConfigurationManager.AppSettings["workspaceId"];
        private static readonly string DatasetKey = ConfigurationManager.AppSettings["datasetKey"];

        //private static readonly string ReportId = ConfigurationManager.AppSettings["reportId"];

        DBConnector dbConnector = new DBConnector();

        public async Task<ActionResult> EmbedReport(string username, string roles, string token)
        {
            var result = new EmbedConfig();
            try
            {
                if (String.IsNullOrEmpty(token))
                {
                    result.ErrorMessage = "you are not authorized no token was found";
                    return View("Error",result);
                }

                System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwtToken;

                if (CustomValidation.ValidateToken(token, out jwtToken))
                {
                    var claims = jwtToken.Claims.Cast<System.Security.Claims.Claim>().ToList();
                    var campaignId = claims.FirstOrDefault(x => x.Type == "CampaignId");
                    var languageId = claims.FirstOrDefault(x => x.Type == "LanguageId");
                    // if the data is not inside the token
                    if (campaignId == null || languageId == null) Environment.Exit(0);
                    Console.WriteLine($"CampaignId: {campaignId.Value}");
                    Console.WriteLine($"LanguageId: {languageId.Value}");

                    var ReportInfo = dbConnector.GetReportInfo(int.Parse(campaignId.Value), languageId.Value);

                    if (ReportInfo == null)
                    {
                        result.ErrorMessage = "No Report was created for this campaign";
                        return View("Error", result);
                    }

                    result = new EmbedConfig { Username = username, Roles = roles };
                    var error = GetWebConfigErrors();
                    if (error != null)
                    {
                        result.ErrorMessage = error;
                        return View(result);
                    }
                    // Create a user password cradentials.
                    var credential = new UserPasswordCredential(Username, Password);
                    // Authenticate using created credentials
                    var authenticationContext = new AuthenticationContext(AuthorityUrl);
                    var authenticationResult = await authenticationContext.AcquireTokenAsync(ResourceUrl, ApplicationId, credential);
                    if (authenticationResult == null)
                    {
                        result.ErrorMessage = "Authentication Failed.";
                        return View(result);
                    }
                    var tokenCredentials = new TokenCredentials(authenticationResult.AccessToken, "Bearer");

                    // Create a Power BI Client object. It will be used to call Power BI APIs.
                    using (var client = new PowerBIClient(new Uri(ApiUrl), tokenCredentials))
                    {
                        //Refresh Dataset befaure calling reports (limitation 8 refersh for Pro account)
                       // object result_ds = client.Datasets.RefreshDataset(WorkspaceId, DatasetKey);

                        // Get a list of reports.
                        var reports = await client.Reports.GetReportsInGroupAsync(WorkspaceId);

                        // No reports retrieved for the given workspace.
                        if (reports.Value.Count() == 0)
                        {
                            result.ErrorMessage = "No reports were found in the workspace";
                            return View(result);
                        }
                        Report report;
                        if (string.IsNullOrWhiteSpace(ReportInfo.ReportId))
                        {
                            // Get the first report in the workspace.
                            report = reports.Value.FirstOrDefault();
                        }
                        else
                        {
                            report = reports.Value.FirstOrDefault(r => r.Id == ReportInfo.ReportId);
                        }
                        if (report == null)
                        {
                            result.ErrorMessage = "No report with the given ID was found in the workspace. Make sure ReportId is valid.";
                            return View(result);
                        }

                        var datasets = await client.Datasets.GetDatasetByIdInGroupAsync(WorkspaceId, report.DatasetId);
                        result.IsEffectiveIdentityRequired = datasets.IsEffectiveIdentityRequired;
                        result.IsEffectiveIdentityRolesRequired = datasets.IsEffectiveIdentityRolesRequired;
                        GenerateTokenRequest generateTokenRequestParameters;
                        // This is how you create embed token with effective identities
                        if (!string.IsNullOrWhiteSpace(username))
                        {
                            var rls = new EffectiveIdentity(username, new List<string> { report.DatasetId });
                            if (!string.IsNullOrWhiteSpace(roles))
                            {
                                var rolesList = new List<string>();
                                rolesList.AddRange(roles.Split(','));
                                rls.Roles = rolesList;
                            }
                            // Generate Embed Token with effective identities.
                            generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view", identities: new List<EffectiveIdentity> { rls });
                        }
                        else
                        {
                            // Generate Embed Token for reports without effective identities.
                            generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view");
                        }
                        var tokenResponse = await client.Reports.GenerateTokenInGroupAsync(WorkspaceId, report.Id, generateTokenRequestParameters);
                        if (tokenResponse == null)
                        {
                            result.ErrorMessage = "Failed to generate embed token.";
                            return View(result);
                        }
                        // Generate Embed Configuration.
                        result.EmbedToken = tokenResponse;
                        result.EmbedUrl = report.EmbedUrl;
                        result.Id = report.Id;
                        result.CampaignId = int.Parse(campaignId.Value);

                        return View(result);
                    }
                }
            }
            catch (HttpOperationException exc)
            {
                result.ErrorMessage = string.Format("Status: {0} ({1})\r\nResponse: {2}\r\nRequestId: {3}", exc.Response.StatusCode, (int)exc.Response.StatusCode, exc.Response.Content, exc.Response.Headers["RequestId"].FirstOrDefault());
            }
            catch (Exception exc)
            {
                result.ErrorMessage = exc.ToString();
            }
            return View("Error");
        }

        /// <summary>
        /// Check if web.config embed parameters have valid values.
        /// </summary>
        /// <returns>Null if web.config parameters are valid, otherwise returns specific error string.</returns>
        private string GetWebConfigErrors()
        {
            // Application Id must have a value.
            if (string.IsNullOrWhiteSpace(ApplicationId))
            {
                return "ApplicationId is empty. please register your application as Native app in https://dev.powerbi.com/apps and fill client Id in web.config.";
            }

            // Application Id must be a Guid object.
            Guid result;
            if (!Guid.TryParse(ApplicationId, out result))
            {
                return "ApplicationId must be a Guid object. please register your application as Native app in https://dev.powerbi.com/apps and fill application Id in web.config.";
            }

            // Workspace Id must have a value.
            if (string.IsNullOrWhiteSpace(WorkspaceId))
            {
                return "WorkspaceId is empty. Please select a group you own and fill its Id in web.config";
            }

            // Workspace Id must be a Guid object.
            if (!Guid.TryParse(WorkspaceId, out result))
            {
                return "WorkspaceId must be a Guid object. Please select a workspace you own and fill its Id in web.config";
            }

            // Username must have a value.
            if (string.IsNullOrWhiteSpace(Username))
            {
                return "Username is empty. Please fill Power BI username in web.config";
            }

            // Password must have a value.
            if (string.IsNullOrWhiteSpace(Password))
            {
                return "Password is empty. Please fill password of Power BI username in web.config";
            }

            return null;
        }
    }
}
