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
using System.Windows.Controls;
using System.Windows.Documents;
using System.Diagnostics;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace AngleSharpScraper
{
    public partial class MainWindow : Window
    {
        private string siteUrl = "https://thehackernews.com/";
        public int numberOfPages;
        private string articleTitle;
        private string url;
        public string[] queryTerms;

        public Dictionary<string, List<Tuple<string, string>>> termToScrapeDictionary = new Dictionary<string, List<Tuple<string, string>>>();

        internal async void ScrapeWebsite()
        {
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
                GetScrapeResults(document);

                siteUrl = document.All.Where(x => x.ClassName == "blog-pager-older-link-mobile")
                    .FirstOrDefault()?
                    .OuterHtml.ReplaceFirst("<a class=\"blog-pager-older-link-mobile\" href=\"", "")
                    .ReplaceFirst("\" id", "*")
                    .Split('*').FirstOrDefault();

                if (string.IsNullOrEmpty(siteUrl))
                    break;
            }

            foreach (var termToScrape in termToScrapeDictionary)
            {
                List<Tuple<string, string>> termResults = termToScrape.Value;

                GroupBox termGroupBox = new GroupBox()
                {
                    Header = termToScrape.Key,
                    Content = new StackPanel()
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(5, 5, 5, 5)
                    }
                };

                resultsStackPanel.Children.Add(termGroupBox);

                for (int iTermResult = 0; iTermResult < termResults.Count; iTermResult++)
                {
                    TextBlock title = new TextBlock()
                    {
                        Text = termToScrapeDictionary[termToScrape.Key][iTermResult].Item1
                    };

                    (termGroupBox.Content as StackPanel).Children.Add(title);

                    Hyperlink hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(termToScrapeDictionary[termToScrape.Key][iTermResult].Item2);
                    hyperlink.NavigateUri = new Uri(termToScrapeDictionary[termToScrape.Key][iTermResult].Item2);
                    hyperlink.RequestNavigate += Hyperlink_RequestNavigate;

                    TextBlock urlTextBlock = new TextBlock();
                    urlTextBlock.Inlines.Add(hyperlink);
                    urlTextBlock.Margin = new Thickness(5, 5, 5, 10);

                    (termGroupBox.Content as StackPanel).Children.Add(urlTextBlock);
                }
            }

            spinnerControl.Visibility = System.Windows.Visibility.Collapsed;
            httpClient.Dispose();
            cancellationToken.Dispose();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void GetScrapeResults(IHtmlDocument document)
        {
            List<IElement> articleLink = new List<IElement>();
            foreach (var term in queryTerms)
            {
                articleLink = document.All.Where(x => x.ClassName == "story-link" && (x.InnerHtml.Contains(term) || 
                        x.InnerHtml.Contains(term.ToLower()))).ToList();

                FillResultsDictionary(term, articleLink);
            }
        }

        public void FillResultsDictionary(string term, IEnumerable<IElement> articleLink)
        {
            foreach (var result in articleLink)
            {
                CleanUpResults(result);
                if (!string.IsNullOrWhiteSpace(articleTitle) && !string.IsNullOrWhiteSpace(url))
                {
                    string currentLine = $"{articleTitle}{Environment.NewLine} " +
                        $"- {url}{Environment.NewLine}{Environment.NewLine}";

                    if (!termToScrapeDictionary.ContainsKey(term))
                        termToScrapeDictionary[term] = new List<Tuple<string, string>>();

                    termToScrapeDictionary[term].Add(new Tuple<string, string>(articleTitle, url));
                }
            }
        }

        private void CleanUpResults(IElement result)
        {
            string htmlResult = result.OuterHtml.ReplaceFirst("<a class=\"story-link\" href=\"", "");
            htmlResult = htmlResult.ReplaceFirst("\">", "");
            htmlResult = htmlResult.ReplaceFirst("\n<div class=\"clear home-post-box cf\">\n<div class=\"home-img clear\">\n<div class=\"img-ratio\"><img alt=\"", " * ");
            htmlResult = htmlResult.ReplaceFirst("\" class=\"home-img-src lazyload\"", "*");       
            SplitResults(htmlResult);
        }

        private void SplitResults(string htmlResult)
        {
            string[] splitResults = htmlResult.Split('*');
            url = splitResults[0];
            articleTitle = splitResults[1];
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ScrapWebsiteEvent(object sender, RoutedEventArgs e)
        {
            termToScrapeDictionary.Clear();
            resultsStackPanel.Children.Clear();
            spinnerControl.Visibility = System.Windows.Visibility.Visible;

            int.TryParse(numberOfPagesTextBox.Text, out numberOfPages);
            queryTerms = queryTermsTextBox.Text.Split(';');
            ScrapeWebsite();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
