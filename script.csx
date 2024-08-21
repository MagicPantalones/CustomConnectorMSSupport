public class Script : ScriptBase
{
    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        Context.Logger.LogInformation($"Original Request URL: {Context.Request.RequestUri}");
        Context.Logger.LogInformation($"Original Authorization: {Context.Request.Headers.Authorization.ToString()}");

        var dateFormat = "yyyy-MM-ddTHH:mm:ssZ";
        var request = this.Context.Request;
        var originalRequestSerialized = JsonConvert.SerializeObject(Context.Request);
        var query = new Uri(request.RequestUri.AbsoluteUri).Query;
        var queryParams = HttpUtility.ParseQueryString(query);
        var projectId = queryParams["projectId"];
        var servicePlanId = queryParams["servicePlanId"];

        queryParams.Remove("projectId");
        queryParams.Remove("servicePlanId");

        var uriBuilder = new UriBuilder(request.RequestUri);

        if (uriBuilder.Path.Contains("my_project_id"))
        {
            uriBuilder.Path = uriBuilder.Path.Replace("my_project_id", projectId);
        }

        if (uriBuilder.Path.Contains("my_service_plan_id"))
        {
            uriBuilder.Path = uriBuilder.Path.Replace("my_service_plan_id", servicePlanId);
        }

        if (request.Headers.Authorization.Scheme == "Basic")
        {
            var credentials = this.Context.Request.Headers.Authorization.ToString().Replace("Basic ", "");
            this.Context.Request.Headers.Remove("Authorization");
            string b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));

            this.Context.Request.Headers.Add("Authorization", "Basic " + b64);
        }
        else
        {
            var credentials = this.Context.Request.Headers.Authorization.ToString();
            this.Context.Request.Headers.Remove("Authorization");

            this.Context.Request.Headers.Add("Authorization", "Bearer " + credentials);
        }


        Context.Logger.LogInformation($"New Authorization: {Context.Request.Headers.Authorization.ToString()}");

        uriBuilder.Query = queryParams.ToString();
        this.Context.Request.RequestUri = uriBuilder.Uri;
        Context.Logger.LogInformation($"New Request URL: {Context.Request.RequestUri}");

        HttpResponseMessage response = await this.Context.SendAsync(this.Context.Request, this.CancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        if (Context.OperationId == "NewSMS")
        {
            /*if(queryParams["start_date"] == null) {
                queryParams.Add("start_date", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.SSSZ"));
            }*/

            if (response.IsSuccessStatusCode)
            {
                var responseContentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseContent = JObject.Parse(responseContentString);
                var startDate = DateTime.Parse(responseContent["inbounds"][0]["received_at"].ToString());

                responseContent.Add("start_date", startDate.ToString(dateFormat));
                responseContent.Add("debugurl", response.RequestMessage.RequestUri.ToString());
                responseContent.Add("originalRequest", originalRequestSerialized);
                response.Content = CreateJsonContent(responseContent.ToString());
                Context.Logger.LogInformation($"New Body: {responseContent}");
            }

        }


        // HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        // response.Content = CreateJsonContent("{\"message\": \"Hello World\"}");

        return response;
    }
}