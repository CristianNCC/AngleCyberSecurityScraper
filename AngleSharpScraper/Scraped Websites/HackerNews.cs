using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AngleSharpScraper
{
    class HackerNews : ScrapedWebsite
    {
        public HackerNews()
        {
            siteUrl = "https://thehackernews.com/";
        }

        public override async Task<List<IHtmlDocument>> ScrapeWebsite(int numberOfPages)
        {
            List <IHtmlDocument> webDocuments = new List<IHtmlDocument>();

            CancellationTokenSource cancellationToken = new CancellationTokenSource();
            HttpClient httpClient = new HttpClient();
            HtmlParser parser = new HtmlParser();

            for (int iPageIdx = 0; iPageIdx < numberOfPages; iPageIdx++)
            {
                HttpResponseMessage request = await httpClient.GetAsync(siteUrl);
                cancellationToken.Token.ThrowIfCancellationRequested();

                Stream response = await request.Content.ReadAsStreamAsync();
                cancellationToken.Token.ThrowIfCancellationRequested();

                IHtmlDocument document = parser.ParseDocument(response);
                webDocuments.Add(document);

                siteUrl = document.All.Where(x => x.ClassName == "blog-pager-older-link-mobile")
                    .FirstOrDefault()?
                    .OuterHtml.ReplaceFirst("<a class=\"blog-pager-older-link-mobile\" href=\"", "")
                    .ReplaceFirst("\" id", "*")
                    .Split('*').FirstOrDefault();

                if (string.IsNullOrEmpty(siteUrl))
                    break;
            }

            httpClient.Dispose();
            cancellationToken.Dispose();

            return webDocuments;
        }

        public override Tuple<string, string> CleanUpResultsForUrlAndTitle(IElement result)
        {
            string htmlResult = result.OuterHtml.ReplaceFirst("<a class=\"story-link\" href=\"", "");
            htmlResult = htmlResult.ReplaceFirst("\">", "");
            htmlResult = htmlResult.ReplaceFirst("\n<div class=\"clear home-post-box cf\">\n<div class=\"home-img clear\">\n<div class=\"img-ratio\"><img alt=\"", " * ");
            htmlResult = htmlResult.ReplaceFirst("\" class=\"home-img-src lazyload\"", "*");
            string[] splitResults = htmlResult.Split('*');
            return new Tuple<string, string>(splitResults[0], splitResults[1]);
        }
    }
}
