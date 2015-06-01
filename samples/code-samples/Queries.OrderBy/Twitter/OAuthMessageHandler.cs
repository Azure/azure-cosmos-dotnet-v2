namespace DocumentDB.Samples.Twitter
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary> 
    /// Basic DelegatingHandler that creates an OAuth authorization header based on the OAuthBase 
    /// class downloaded from http://oauth.net 
    /// </summary> 
    public class OAuthMessageHandler : DelegatingHandler
    {
        private string consumerKey;
        private string consumerSecret;
        private string token;
        private string tokenSecret;

        private OAuthBase openAuthBase = new OAuthBase();

        public OAuthMessageHandler(
            HttpMessageHandler innerHandler,
            string consumerKey,
            string consumerSecret,
            string token,
            string tokenSecret)
            : base(innerHandler)
        {
            this.consumerKey = consumerKey;
            this.consumerSecret = consumerSecret;
            this.token = token;
            this.tokenSecret = tokenSecret;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string normalizedUri;
            string normalizedParameters;
            string authHeader;

            string signature = this.openAuthBase.GenerateSignature(
                request.RequestUri,
                this.consumerKey,
                this.consumerSecret,
                this.token,
                this.tokenSecret,
                request.Method.Method,
                this.openAuthBase.GenerateTimeStamp(),
                this.openAuthBase.GenerateNonce(),
                out normalizedUri,
                out normalizedParameters,
                out authHeader);

            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);
            return base.SendAsync(request, cancellationToken);
        }
    }
}