using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AngleSharp.Dom;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Diagnostics;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using System.Threading.Tasks;

namespace NLPWebScraper
{
    public partial class MainWindow : Window
    {
        public int numberOfPages;
        public List<string> queryTerms;
        List<ScrapedWebsite> scrapedWebsites = new List<ScrapedWebsite>();

        // List of dictionaries where Key=Term and list of tuples <url, articleTitle, titlePolarity>
        public List<Dictionary<string, List<Tuple<string, string, int>>>> listTermToScrapeDictionary = 
            new List<Dictionary<string, List<Tuple<string, string, int>>>>();

        private async void StartScraping()
        {
            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                Task<List<IHtmlDocument>> scrapeWebSiteTask = scrapedWebsites[iWebsiteIdx].ScrapeWebsite(numberOfPages);
                List<IHtmlDocument> webDocuments = await scrapeWebSiteTask;

                listTermToScrapeDictionary.Add(new Dictionary<string, List<Tuple<string, string, int>>>());
                foreach (var document in webDocuments)
                {
                    FillResultsDictionary(iWebsiteIdx, document.All.Where(x => x.ClassName == "story-link"), 
                        scrapedWebsites[iWebsiteIdx].CleanUpResultsForUrlAndTitle);
                }
            }

            foreach (var websiteDictionary in listTermToScrapeDictionary)
            {
                foreach (var termToScrape in websiteDictionary)
                {
                    List<Tuple<string, string, int>> termResults = termToScrape.Value;

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

                    List<Tuple<string, string, int>> sortedResults = termResults.OrderBy(result => result.Item3).ToList();

                    for (int iTermResult = 0; iTermResult < sortedResults.Count; iTermResult++)
                    {
                        TextBlock title = new TextBlock()
                        {
                            Text = " Polarity: (" + sortedResults[iTermResult].Item3.ToString() + "): " + sortedResults[iTermResult].Item1
                        };

                        (termGroupBox.Content as StackPanel).Children.Add(title);

                        Hyperlink hyperlink = new Hyperlink();
                        hyperlink.Inlines.Add(sortedResults[iTermResult].Item2);
                        hyperlink.NavigateUri = new Uri(sortedResults[iTermResult].Item2);
                        hyperlink.RequestNavigate += Hyperlink_RequestNavigate;

                        TextBlock urlTextBlock = new TextBlock();
                        urlTextBlock.Inlines.Add(hyperlink);
                        urlTextBlock.Margin = new Thickness(5, 5, 5, 10);

                        (termGroupBox.Content as StackPanel).Children.Add(urlTextBlock);
                    }
                }
            }

            spinnerControl.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        public void FillResultsDictionary(int websiteIdx, IEnumerable<IElement> articleLinksList, Func<IElement, Tuple<string, string>> CleanUpResults)
        {
            foreach (var result in articleLinksList)
            {
                Tuple<string, string> urlTitleTuple = CleanUpResults(result);
                string url = urlTitleTuple.Item1;
                string articleTitle = urlTitleTuple.Item2;

                foreach (var term in queryTerms)
                {
                    if (!string.IsNullOrWhiteSpace(articleTitle) && !string.IsNullOrWhiteSpace(url))
                    {
                        List<string> tokenizedTitle = OpenNLP.APIOpenNLP.TokenizeSentence(articleTitle).Select(token => token.ToLower()).ToList();

                        if (!tokenizedTitle.Contains(term.ToLower()))
                            continue;

                        int sentencePolarity = sentencePolarity = OpenNLP.APIOpenNLP.AFINNAnalysis(tokenizedTitle.ToArray());

                        if (!listTermToScrapeDictionary[websiteIdx].ContainsKey(term))
                            listTermToScrapeDictionary[websiteIdx][term] = new List<Tuple<string, string, int>>();

                        listTermToScrapeDictionary[websiteIdx][term].Add(new Tuple<string, string, int>(articleTitle, url, sentencePolarity));
                    }
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadUpSupportedWebsites();
        }

        private void LoadUpSupportedWebsites()
        {
            scrapedWebsites.Add(new HackerNews());
        }

        private void ScrapWebsiteEvent(object sender, RoutedEventArgs e)
        {
            listTermToScrapeDictionary.Clear();
            resultsStackPanel.Children.Clear();
            spinnerControl.Visibility = System.Windows.Visibility.Visible;

            int.TryParse(numberOfPagesTextBox.Text, out numberOfPages);
            queryTerms = queryTermsTextBox.Text.Split(';').ToList();
            StartScraping();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
