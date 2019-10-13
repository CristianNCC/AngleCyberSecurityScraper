using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLPWebScraper
{
    abstract class StaticallyScrapedWebsite : ScrapedWebsite
    {
        public string storyClassName;
        public StaticallyScrapedWebsite(string siteUrl) : base(siteUrl)
        {

        }

        public abstract Task<List<IHtmlDocument>> ScrapeWebsite(int numberOfPages);
        public abstract Tuple<string, string> CleanUpResultsForUrlAndTitle(IElement result);
    }
}
