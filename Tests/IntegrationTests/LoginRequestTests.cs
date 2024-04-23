using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Server;
using Server.Utilities;
using UtilitiesLibrary.Models;

namespace Tests.IntegrationTests.Server
{
    [Collection(nameof(LoginRequestTests))]
    public class LoginRequestTests : IDisposable
    {
        private HttpClient _client;
        private WebApplicationFactory<Program> _factory;

        private readonly byte[] _sharedSecretKey;
        private readonly string _accessToken;

        public LoginRequestTests()
        {
            _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("TEST_INTEGRATION");
            });
            _client = _factory.CreateClient();
            _sharedSecretKey = CompleteTestHandshake.GetSharedSecret(_client);

            byte[] unlockSharedSecret = CompleteTestHandshake.GetSharedSecret(_client, 1);
            _accessToken = CompleteTestAuth.GetAccessToken(_client, unlockSharedSecret);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        public void Dispose()
        {
            // Ensure that the server's database file is deleted after each test run.
            var service = _factory.Services.GetService(typeof(SqlContext));
            if (service is SqlContext context)
                context.Database.EnsureDeleted();
        }

        private async Task<HttpResponseMessage> RegisterDomainAsync(string domain, string username = "loginrequesttestsusername")
        {
            byte[] plainPassword = PasswordUtil.ByteArrayFromPlain("loginrequesttestspassword");
            byte[] encryptedSharedKeyPassword = await PasswordUtil.EncryptMessage(_sharedSecretKey, plainPassword);

            var registerApiEndpoint = "/api/register";
            var registerRequest = new DomainRegisterRequest { Domain = domain, Username = username, Password = Convert.ToBase64String(encryptedSharedKeyPassword) };
            var registerRequestContent = new StringContent(JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");
            return await _client.PostAsync(registerApiEndpoint, registerRequestContent);
        }

        private async Task<HttpResponseMessage> LoginDomainAsync(DomainLoginRequest request)
        {
            var apiEndpoint = "/api/login";
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(apiEndpoint, requestContent);
            return response;
        }

        private async Task<HttpResponseMessage> EditDomainAsync(LoginDetailsEditRequest request)
        {
            var apiEndpoint = "/api/logindetails";
            HttpContent requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var response = await _client.PutAsync(apiEndpoint, requestContent);
            return response;
        }

        private async Task<HttpResponseMessage> DeleteDomainAsync(int loginDetailsId)
        {
            var apiEndpoint = $"/api/logindetails?id={loginDetailsId}";
            var response = await _client.DeleteAsync(apiEndpoint);
            return response;
        }

        [Fact]
        public async Task TestNoDomainLoginRequestReturns401()
        {
            var response = await LoginDomainAsync(null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        
        [Fact]
        public async Task TestEmptyDomainLoginRequestReturns404()
        {
            var response = await LoginDomainAsync(new());

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TestUnknownDomainLoginRequestReturns404()
        {
            DomainLoginRequest request = new DomainLoginRequest { Domain = "unknowndomain.404" };

            var response = await LoginDomainAsync(request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsOk()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };

            var response = await LoginDomainAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsResponseObj()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };

            var response = await LoginDomainAsync(request);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainLoginResponse? responseObj = JsonSerializer.Deserialize<DomainLoginResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainLoginResponse>(responseObj);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsCorrectPassword()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };

            var response = await LoginDomainAsync(request);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainLoginResponse? responseObj = JsonSerializer.Deserialize<DomainLoginResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainLoginResponse>(responseObj);
            Assert.NotNull(responseObj.Password);

            byte[] decryptedPasword = await PasswordUtil.DecryptMessage(_sharedSecretKey, Convert.FromBase64String(responseObj.Password));
            string decryptedPasswordString = PasswordUtil.PlainFromContainer(decryptedPasword);

            Assert.Equal("loginrequesttestspassword", decryptedPasswordString);
        }

        [Fact]
        public async Task TestKnownDomainLoginRequestReturnsIncorrectPassword()
        {
            // Create login details for the known domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Attempt to retrieve login details with the known domain
            DomainLoginRequest request = new DomainLoginRequest { Domain = "knowndomain.ok" };

            var response = await LoginDomainAsync(request);

            string responseString = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseString);

            DomainLoginResponse? responseObj = JsonSerializer.Deserialize<DomainLoginResponse>(responseString);

            Assert.NotNull(responseObj);
            Assert.IsType<DomainLoginResponse>(responseObj);
            Assert.NotNull(responseObj.Password);

            byte[] decryptedPasword = await PasswordUtil.DecryptMessage(_sharedSecretKey, Convert.FromBase64String(responseObj.Password));
            string decryptedPasswordString = PasswordUtil.PlainFromContainer(decryptedPasword);

            Assert.NotEqual("loginrequesttestspassword123", decryptedPasswordString);
        }

        [Fact]
        public async Task TestEditLoginDetailsReturnsNoContent()
        {
            // Create login details for the domain
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Edit the login details
            byte[] encryptedNewPassword = await PasswordUtil.EncryptMessage(_sharedSecretKey, PasswordUtil.ByteArrayFromPlain("loginrequesttestspasswordedited"));
            LoginDetailsEditRequest editRequest = new LoginDetailsEditRequest
            {
                SourceId = 0,
                LoginDetailsId = 1,
                Username = "loginrequesttestsusernameedited",
                Password = Convert.ToBase64String(encryptedNewPassword)
            };
            var editResponse = await EditDomainAsync(editRequest);

            Assert.Equal(HttpStatusCode.NoContent, editResponse.StatusCode);

            // Verify that the login details have been edited
            var loginResponseEdited = await LoginDomainAsync(new DomainLoginRequest { Domain = "knowndomain.ok", Username = "loginrequesttestsusernameedited" });
            string loginResponseEditedString = await loginResponseEdited.Content.ReadAsStringAsync();
            DomainLoginResponse? loginResponseEditedObj = JsonSerializer.Deserialize<DomainLoginResponse>(loginResponseEditedString);
            Assert.NotNull(loginResponseEditedObj);
            Assert.IsType<DomainLoginResponse>(loginResponseEditedObj);

            byte[] decryptedPasword = await PasswordUtil.DecryptMessage(_sharedSecretKey, Convert.FromBase64String(loginResponseEditedObj.Password));
            string decryptedPasswordString = PasswordUtil.PlainFromContainer(decryptedPasword);

            Assert.Equal("loginrequesttestsusernameedited", loginResponseEditedObj.Username);
            Assert.Equal("loginrequesttestspasswordedited", decryptedPasswordString);
        }

        [Fact]
        public async Task TestEditLoginDetailsExistingUsernameReturnsConflict()
        {
            // Create the first login details
            var registerResponse = await RegisterDomainAsync("knowndomain.ok", "one");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Create the second login details
            var registerResponse2 = await RegisterDomainAsync("knowndomain.ok", "two");
            Assert.Equal(HttpStatusCode.OK, registerResponse2.StatusCode);

            // Edit the login details
            LoginDetailsEditRequest editRequest = new LoginDetailsEditRequest
            {
                SourceId = 0,
                LoginDetailsId = 1,
                Username = "two" // Already exists for this domain
            };
            var editResponse = await EditDomainAsync(editRequest);
            Assert.Equal(HttpStatusCode.Conflict, editResponse.StatusCode);
        }

        [Fact]
        public async Task TestEditNonExistingLoginDetailsReturnsNotFound()
        {
            LoginDetailsEditRequest editRequest = new LoginDetailsEditRequest
            {
                SourceId = 0,
                LoginDetailsId = 1,
                Username = "loginrequesttestsusernameedited",
                Password = "loginrequesttestspasswordedited"
            };
            var editResponse = await EditDomainAsync(editRequest);

            Assert.Equal(HttpStatusCode.NotFound, editResponse.StatusCode);
        }

        [Fact]
        public async Task TestDeleteLoginDetailsReturnsNoContent()
        {
            // Create login details
            var registerResponse = await RegisterDomainAsync("knowndomain.ok");
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Delete the login details
            var deleteResponse = await DeleteDomainAsync(1);
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // Verify that the login details have been deleted
            var loginResponseDeleted = await LoginDomainAsync(new DomainLoginRequest { SourceId = 0, Username = "loginrequesttestsusername", Domain = "knowndomain.ok" });
            Assert.Equal(HttpStatusCode.NotFound, loginResponseDeleted.StatusCode);
        }

        [Fact]
        public async Task TestDeleteNonExistingLoginDetailsReturnsNotFound()
        {
            var deleteResponse = await DeleteDomainAsync(1);
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }
    }
}