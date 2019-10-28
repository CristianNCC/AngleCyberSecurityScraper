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
using System.IO;

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

        private async void DynamicScraping()
        {
            string results = string.Empty;
            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                DynamicallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as DynamicallyScrapedWebsite;
                if (scrapedWebsite == null)
                    continue;

                var dynamicScrapingResultList = await scrapedWebsite.DynamicScraping();
                foreach (var documentResult in dynamicScrapingResultList)
                {
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                    results += "========================================================= Link: " + documentResult.linkToPage + "==============================================================";
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;

                    results += documentResult.content;

                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                    results += "============== Named Entities ==============";
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;

                    var namedEntities = OpenNLP.APIOpenNLP.FindNames(documentResult.content);
                    var dateListTupleIndexes = Utils.MergeToTuples(Utils.AllIndexesOf(namedEntities, "<date>"), (Utils.AllIndexesOf(namedEntities, "</date>")));
                    var personListTupleIndexes = Utils.MergeToTuples(Utils.AllIndexesOf(namedEntities, "<person>"), (Utils.AllIndexesOf(namedEntities, "</person>")));
                    var timeListTupleIndexes = Utils.MergeToTuples(Utils.AllIndexesOf(namedEntities, "<time>"), (Utils.AllIndexesOf(namedEntities, "</time>")));
                    var organizationListTupleIndexes = Utils.MergeToTuples(Utils.AllIndexesOf(namedEntities, "<organization>"), (Utils.AllIndexesOf(namedEntities, "</organization>")));

                    results += dateListTupleIndexes.Count > 0 ? "Dates: " : "";
                    foreach (var tuple in dateListTupleIndexes)
                        results += namedEntities.Substring(tuple.Item1 + 6, (tuple.Item2 - 6) - tuple.Item1) + (tuple != dateListTupleIndexes.Last() ? ", " : "." + Environment.NewLine);

                    results += personListTupleIndexes.Count > 0 ? "People: " : "";
                    foreach (var tuple in personListTupleIndexes)
                        results += namedEntities.Substring(tuple.Item1 + 8, (tuple.Item2 - 8) - tuple.Item1) + (tuple != personListTupleIndexes.Last() ? ", " : "." + Environment.NewLine);

                    results += timeListTupleIndexes.Count > 0 ? "Time: " : "";
                    foreach (var tuple in timeListTupleIndexes)
                        results += namedEntities.Substring(tuple.Item1 + 6, (tuple.Item2 - 6) - tuple.Item1) + (tuple != timeListTupleIndexes.Last() ? ", " : "." + Environment.NewLine);

                    results += organizationListTupleIndexes.Count > 0 ? "Organizations: " : "";
                    foreach (var tuple in organizationListTupleIndexes)
                        results += namedEntities.Substring(tuple.Item1 + 14, (tuple.Item2 - 14) - tuple.Item1) + (tuple != organizationListTupleIndexes.Last() ? ", " : "." + Environment.NewLine);

                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                    results += "========================================================= END ==============================================================";
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                }
            }

            TextBox textbox = new TextBox()
            {
                Text = results,
                TextWrapping = TextWrapping.Wrap
            };

            resultsStackPanel.Children.Add(textbox);
            spinnerControl.Visibility = System.Windows.Visibility.Collapsed;
        }

        private async void StaticScraping()
        {
            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                StaticallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as StaticallyScrapedWebsite;
                listTermToScrapeDictionary.Add(new Dictionary<string, List<Tuple<string, string, int>>>());

                if (scrapedWebsite == null)
                    continue;

                Task<List<IHtmlDocument>> scrapeWebSiteTask = scrapedWebsite.ScrapeWebsite(numberOfPages);
                List<IHtmlDocument> webDocuments = await scrapeWebSiteTask;      

                foreach (var document in webDocuments)
                {
                    FillResultsDictionary(iWebsiteIdx, document.All.Where(x => x.ClassName == scrapedWebsite.storyClassName),
                        scrapedWebsite.CleanUpResultsForUrlAndTitle);
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
            scrapedWebsites.Add(new HackerNews("https://thehackernews.com/"));
        }

        private void ScrapWebsiteEvent(object sender, RoutedEventArgs e)
        {
            listTermToScrapeDictionary.Clear();
            resultsStackPanel.Children.Clear();
            spinnerControl.Visibility = System.Windows.Visibility.Visible;

            int.TryParse(numberOfPagesTextBox.Text, out numberOfPages);
            queryTerms = queryTermsTextBox.Text.Split(';').ToList();

            if (dynamicScrapingCheckbox.IsChecked == true)
            {
                scrapedWebsites.RemoveAll(scrapedWebsite => scrapedWebsite is DynamicallyScrapedWebsite);
                scrapedWebsites.Add(new DynamicallyScrapedWebsite(targetWebsiteTextbox.Text));
                DynamicScraping();
            }
            else
                StaticScraping();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ToggleScrapingMode(object sender, RoutedEventArgs e)
        {
            staticScrapingGroupBox.IsEnabled = dynamicScrapingCheckbox.IsChecked == false;
            dynamicScrapingGroupBox.IsEnabled = dynamicScrapingCheckbox.IsChecked == true;
        }
    }
}
