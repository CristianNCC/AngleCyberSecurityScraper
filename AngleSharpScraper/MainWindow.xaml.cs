using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Net.Http;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using System.IO;
using AngleSharp.Dom;
using AngleSharp.Text;

namespace AngleSharpScraper
{
    public partial class MainWindow : Window
    {
        private string siteUrl = "https://www.oceannetworks.ca/news/stories";

        private string ArticleTitle { get; set; }
        private string Url { get; set; }
        public string[] QueryTerms { get; } = { "Ocean", "Nature", "Pollution" };

        internal async void ScrapeWebsite()
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage request = await httpClient.GetAsync(siteUrl);
            cancellationToken.Token.ThrowIfCancellationRequested();

            resultsTextBox.Text = "";
            spinnerControl.Visibility = System.Windows.Visibility.Collapsed;

            Stream response = await request.Content.ReadAsStreamAsync();
            cancellationToken.Token.ThrowIfCancellationRequested();

            HtmlParser parser = new HtmlParser();
            IHtmlDocument document = parser.ParseDocument(response);
            GetScrapeResults(document);
        }

        private void GetScrapeResults(IHtmlDocument document)
        {
            List<IElement> articleLink = new List<IElement>();
            foreach (var term in QueryTerms)
            {
                articleLink = document.All.Where(x => x.ClassName == "views-field views-field-nothing" && (x.ParentElement.InnerHtml.Contains(term) || x.ParentElement.InnerHtml.Contains(term.ToLower()))).ToList();
                resultsTextBox.Text += "----- " + term + " -----" + Environment.NewLine;
                PrintResults(term, articleLink);
            }
        }

        public void PrintResults(string term, IEnumerable<IElement> articleLink)
        {
            foreach (var result in articleLink)
            {
                CleanUpResults(result);
                if (!string.IsNullOrWhiteSpace(ArticleTitle) && !string.IsNullOrWhiteSpace(Url))
                    resultsTextBox.Text += $"{ArticleTitle}{Environment.NewLine} - {Url}{Environment.NewLine}{Environment.NewLine}";
            }
        }

        private void CleanUpResults(IElement result)
        {
            if (result.InnerHtml.Contains("<span class=\"field-content\"><div><a href=\""))
            {
                string htmlResult = result.InnerHtml.ReplaceFirst("<span class=\"field-content\"><div><a href=\"", "https://www.oceannetworks.ca");
                htmlResult = htmlResult.ReplaceFirst("\">", "*");
                htmlResult = htmlResult.ReplaceFirst("</a></div>\n<div class=\"article-title-top\">", "-");
                htmlResult = htmlResult.ReplaceFirst("</div>\n<hr></span>  ", "");

                SplitResults(htmlResult);
            }
            else
            {
                ArticleTitle = string.Empty;
                Url = string.Empty;
            }
        }

        private void SplitResults(string htmlResult)
        {
            string[] splitResults = htmlResult.Split('*');
            Url = splitResults[0];
            ArticleTitle = splitResults[1];
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ScrapWebsiteEvent(object sender, RoutedEventArgs e)
        {
            spinnerControl.Visibility = System.Windows.Visibility.Visible;
            ScrapeWebsite();
        }
    }
}
