using Newtonsoft.Json;
using OAuthLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Brainchild.Net
{
    public delegate void CreateCompletedHandler(TwitterStream created);

    public class TwitterStream:IEnumerable<string>
    {
        private const string API_TWITTER_STREAMING = "https://stream.twitter.com/1/statuses/sample.json";
        private const string API_TWITTER_AUTHORIZE = "https://twitter.com/oauth/authorize";
        private const string API_TWITTER_ACCESS_TOKEN = "http://twitter.com/oauth/access_token";
        private const string API_TWITTER_REQUEST_TOKEN = "http://twitter.com/oauth/request_token";
        private const string TWITTER_BASE_URI = "http://twitter.com/";
        private WebResponse webReponse = null;
        private AccessToken accessToken = null;
        private Consumer consumer = null;

        protected TwitterStream(string consumerKey, string consumerSecret, AccessToken accessToken)
        {
            this.consumer = new Consumer(consumerKey, consumerSecret);
            this.accessToken = accessToken;
        }

        protected TwitterStream(Consumer consumer, AccessToken accessToken)
        {
            this.consumer = consumer;
            this.accessToken = accessToken;
        }

        protected HttpWebResponse AccessProtectedResource(string urlString, string method, string authorizationRealm, Parameter[] queryParameters, Parameter[] additionalParameters)
        {
            var webReponse = this.consumer.AccessProtectedResource(this.accessToken, urlString, method, authorizationRealm, queryParameters, null);
            return webReponse;
        }

        public static TwitterStream Create(string consumerKey, string consumerSecret, string tokenValue, string tokenSecret)
        {
            var accessToken = new AccessToken(tokenValue, tokenSecret);
            var twitterStream = new TwitterStream(consumerKey, consumerSecret, accessToken);
            Parameter[] parameters = { };
            twitterStream.webReponse = twitterStream.AccessProtectedResource(API_TWITTER_STREAMING, "GET", API_TWITTER_AUTHORIZE, parameters, null);
            return twitterStream;
        }

        public static void CreateAsync(string consumerKey, string consumerSecret, string tokenValue, string tokenSecret, CreateCompletedHandler onComplete)
        {
            var accessToken = new AccessToken(tokenValue, tokenSecret);
            var twitterStream = new TwitterStream(consumerKey, consumerSecret, accessToken);
            Parameter[] parameters = { };
            HttpWebRequest request = null;
 
            var asyncResult = twitterStream.consumer.BeginAccessProtectedResource(twitterStream.accessToken, API_TWITTER_STREAMING, "GET", API_TWITTER_AUTHORIZE, parameters, null, out request, ar=>
            {
                var asyncResult2 = request.BeginGetResponse(ar2 =>
                {
                    WebResponse result = null;
                    result = request.EndGetResponse(ar2);
                    twitterStream.webReponse = result;
                    onComplete(twitterStream);
                }, null);
            }, null);
        }

        public IEnumerator<string> GetEnumerator()
        {
            var responseStream = this.webReponse.GetResponseStream();
            return new TwitterStreamEnumerator(responseStream);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            var responseStream = this.webReponse.GetResponseStream();
            return new TwitterStreamEnumerator(responseStream);
        }
    }
}
