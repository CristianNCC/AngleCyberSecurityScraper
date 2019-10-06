using System;
using AngleSharp.Dom;
using System.Collections.Generic;
using AngleSharp.Html.Dom;
using System.Threading.Tasks;

namespace NLPWebScraper
{
    abstract class ScrapedWebsite
    {
        public string siteUrl;
        public abstract Task<List<IHtmlDocument>> ScrapeWebsite(int numberOfPages);
        public abstract Tuple<string, string> CleanUpResultsForUrlAndTitle(IElement result);
    }
}
