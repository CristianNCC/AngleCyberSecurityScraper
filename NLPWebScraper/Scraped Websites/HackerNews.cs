using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLPWebScraper
{
    class HackerNews : StaticallyScrapedWebsite
    {
        public HackerNews(string siteUrl = "") : base(siteUrl)
        {
            siteUrl = "https://thehackernews.com/";
            storyClassName = "story-link";
        }

        public override async Task<List<IHtmlDocument>> ScrapeWebsite(int numberOfPages)
        {
            List <IHtmlDocument> webDocuments = new List<IHtmlDocument>();

            string currentSiteUrl = siteUrl;
            for (int iPageIdx = 0; iPageIdx < numberOfPages; iPageIdx++)
            {;

                Task<IHtmlDocument> documentTask = GetDocumentFromLink(currentSiteUrl);
                IHtmlDocument document = await documentTask;
                webDocuments.Add(document);

                currentSiteUrl = document.All.Where(x => x.ClassName == "blog-pager-older-link-mobile")
                    .FirstOrDefault()?
                    .OuterHtml.ReplaceFirst("<a class=\"blog-pager-older-link-mobile\" href=\"", "")
                    .ReplaceFirst("\" id", "*")
                    .Split('*').FirstOrDefault();

                if (string.IsNullOrEmpty(currentSiteUrl))
                    break;
            }

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
