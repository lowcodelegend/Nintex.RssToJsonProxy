using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Web;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using System.Net;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Newtonsoft.Json.Serialization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Resolvers;

namespace Nintex.RssToJsonProxy
{
    public static class GetRssAsJson
    {
        [FunctionName("GetRssAsJson")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "url" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "url", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "url parameter of rss feed")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<SimplifiedItem>), Description = "OK response", Example = typeof(SimplifiedItemnModelExample))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string url = req.Query["url"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            url ??= data?.url;

            XmlReader reader = XmlReader.Create(url);
            SyndicationFeed feed = SyndicationFeed.Load(reader);
            reader.Close();

            var reducedList = new List<SimplifiedItem>();

            foreach(var item in feed.Items)
			{
				var si = new SimplifiedItem
				{
					Link = LinkCleanup(item.Links.First().Uri.ToString()),
					Title = item.Title.Text
				};

				if (item.Summary == null)
				{
                    try
                    {
                        si.Summary = ((TextSyndicationContent)item.Content).Text;
                    }
                    catch
                    { }
				}
				else
				{
					si.Summary = item.Summary.Text;
				}

                si.Summary = HTMLToText(si.Summary);

				reducedList.Add(si);
			}

            var result = new OkObjectResult(reducedList);
            return await Task.FromResult(result).ConfigureAwait(false);
        }

        public class SimplifiedItemnModelExample : OpenApiExample<SimplifiedItem>
        {
            public override IOpenApiExample<SimplifiedItem> Build(NamingStrategy namingStrategy = null)
            {
                this.Examples.Add(
                    OpenApiExampleResolver.Resolve(
                        "sample1",
                        new SimplifiedItem() { Title = "Hello World", Link = "https://example.com", Summary = "This is a summary" },
                        namingStrategy
                    ));

                return this;
            }
        }

        private static string LinkCleanup(string url)
		{
            Uri myUri = new Uri(url);
            string urlParam = HttpUtility.ParseQueryString(myUri.Query).Get("url");
			if (!string.IsNullOrEmpty(urlParam))
			{
                return urlParam;
			}
			else
			{
                return url;
			}
        }

		public static string HTMLToText(string HTMLCode)
        {
            // Remove new lines since they are not visible in HTML
            HTMLCode = HTMLCode.Replace("\n", " ");

            // Remove tab spaces
            HTMLCode = HTMLCode.Replace("\t", " ");

            // Remove multiple white spaces from HTML
            HTMLCode = Regex.Replace(HTMLCode, "\\s+", " ");

            // Remove HEAD tag
            HTMLCode = Regex.Replace(HTMLCode, "<head.*?</head>", ""
                                , RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Remove any JavaScript
            HTMLCode = Regex.Replace(HTMLCode, "<script.*?</script>", ""
              , RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Replace special characters like &, <, >, " etc.
            StringBuilder sbHTML = new StringBuilder(HTMLCode);
            // Note: There are many more special characters, these are just
            // most common. You can add new characters in this arrays if needed
            string[] OldWords = {"&nbsp;", "&amp;", "&quot;", "&lt;",
   "&gt;", "&reg;", "&copy;", "&bull;", "&trade;","&#39;"};
            string[] NewWords = { " ", "&", "\"", "<", ">", "®", "©", "•", "™", "\'" };
            for (int i = 0; i < OldWords.Length; i++)
            {
                sbHTML.Replace(OldWords[i], NewWords[i]);
            }

            // Check if there are line breaks (<br>) or paragraph (<p>)
            sbHTML.Replace("<br>", "\n<br>");
            sbHTML.Replace("<br ", "\n<br ");
            sbHTML.Replace("<p ", "\n<p ");

            // Finally, remove all HTML tags and return plain text
            return System.Text.RegularExpressions.Regex.Replace(
              sbHTML.ToString(), "<[^>]*>", "");
        }

    }

    public class SimplifiedItem
    {
        [JsonProperty("Title")]
        public string Title;
        public string Link;
        public string Summary;
	}

}
