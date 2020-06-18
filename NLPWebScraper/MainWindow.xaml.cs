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
using AngleSharp;
using OpenNLP;
using System.Threading;

namespace NLPWebScraper
{
    public partial class MainWindow : Window
    {
        private string pathToResults = "../Files/results.txt";
        private string pathToResultsHTML = "../Files/results.html";

        public bool analyzeNamedEntities = false;
        public int numberOfPages;
        public List<string> queryTerms;
        List<ScrapedWebsite> scrapedWebsites = new List<ScrapedWebsite>();

        // If scraping for information is false, then the scraping will stop at finding the template.
        public bool scrapeOnlyForTemplate = true;

        // List of dictionaries where Key=Term and list of tuples <url, articleTitle, titlePolarity>
        public List<Dictionary<string, List<Tuple<string, string, int>>>> listTermToScrapeDictionary = new List<Dictionary<string, List<Tuple<string, string, int>>>>();

        public MainWindow()
        {
            InitializeComponent();

            UICallbackTimer.DelayExecution(TimeSpan.FromSeconds(2),  () => 
            {
                new Thread(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        IsEnabled = false;
                        spinnerControl.Visibility = System.Windows.Visibility.Visible;
                    });

                    Word2VecManager.LoadUpWord2VecDatabase();
                    SiteDatabaseManager.DeserializeSiteInformation();

                    Dispatcher.Invoke(() =>
                    {
                        IsEnabled = true;
                        spinnerControl.Visibility = System.Windows.Visibility.Collapsed;
                    });

                }).Start();
            });

            dynamicScrapingCheckbox.IsChecked = true;
            dynamicScrapingGroupBox.IsEnabled = dynamicScrapingCheckbox.IsChecked == true;
            LoadUpSupportedWebsites();
        }

        private async void DynamicScraping()
        {
            string results = string.Empty;
            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                DynamicallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as DynamicallyScrapedWebsite;
                if (scrapedWebsite == null)
                    continue;

                await Task.Run(() => scrapedWebsite.DynamicScrapingForTemplateExtraction()).ConfigureAwait(true);

                if (!scrapeOnlyForTemplate)
                {
                    await Task.Run(() => scrapedWebsite.DynamicScrapingForInformationGathering(queryTerms, numberOfPages)).ConfigureAwait(true);
                }

                bool castSucceded = int.TryParse(summarySizeTextbox.Text, out int summarySize);
                if (!castSucceded)
                    return;

                if (summarySize > 0)
                {
                    UpdateTextBoxWithStatus("Summarizing the text using the TextRank algorithm...");
                    await Task.Run(() =>
                    {
                        Word2VecManager.CallbackToGUI = UpdateResults;
                        Word2VecManager.RunWord2Vec(scrapedWebsites, summarySize);
                    }).ConfigureAwait(true);
                }

                UpdateTextBoxWithStatus("Writing the results to disk...");
                ConcatenateDataForPrint(scrapedWebsite.scrapingResults, ref results);
                UpdateTextBoxWithStatus("Done writing the results to disk as a text file.");
                spinnerControl.Visibility = System.Windows.Visibility.Collapsed;

                var outputHTML = await CreateHTMLDocumentFromSummaryAsync(scrapedWebsite).ConfigureAwait(true);
                File.WriteAllText(pathToResultsHTML, outputHTML.ToHtml());
                UpdateTextBoxWithStatus("Generated a HTML page and saved to disk. The process is finished...");
            }
        }

        private void ConcatenateDataForPrint(List<DocumentScrapingResult> scrapingResults, ref string results) 
        {
            int iDocumentIdx = 0;
            foreach (var documentResult in scrapingResults)
            {
                results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                results += "========================================================= Link: " + documentResult.linkToPage + "==============================================================";
                results += "========================================================= Title: " + documentResult.title + "==============================================================";
                results += Environment.NewLine + Environment.NewLine + Environment.NewLine;

                results += documentResult.content + Environment.NewLine + Environment.NewLine;
                results += "Summary: " + Environment.NewLine + documentResult.contentSummary + Environment.NewLine;

                if (analyzeNamedEntities)
                {
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                    results += "============== Named Entities ==============";
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;

                    var namedEntities = OpenNLP.APIOpenNLP.FindNames(documentResult.content);
                    var dateListTupleIndexes = MainUtils.MergeToTuples(MainUtils.AllIndexesOf(namedEntities, "<date>"), (MainUtils.AllIndexesOf(namedEntities, "</date>")));
                    var personListTupleIndexes = MainUtils.MergeToTuples(MainUtils.AllIndexesOf(namedEntities, "<person>"), (MainUtils.AllIndexesOf(namedEntities, "</person>")));
                    var timeListTupleIndexes = MainUtils.MergeToTuples(MainUtils.AllIndexesOf(namedEntities, "<time>"), (MainUtils.AllIndexesOf(namedEntities, "</time>")));
                    var organizationListTupleIndexes = MainUtils.MergeToTuples(MainUtils.AllIndexesOf(namedEntities, "<organization>"), (MainUtils.AllIndexesOf(namedEntities, "</organization>")));

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
                    results += "============== END Named Entities ==============";
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                }

                results += Environment.NewLine;

                if (documentResult.topWords.Count > 0)
                {
                    results += "Most important words in document: ";
                    for (int iWordIdx = 0; iWordIdx < documentResult.topWords.Count; iWordIdx++)
                    {
                        results += documentResult.topWords[iWordIdx];

                        if (iWordIdx != documentResult.topWords.Count - 1)
                            results += ", ";
                        else
                            results += ".";
                    }
                }

                results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                results += "========================================================= END ==============================================================";
                results += Environment.NewLine + Environment.NewLine + Environment.NewLine;

                iDocumentIdx++;
            }
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
                List<IHtmlDocument> webDocuments = await scrapeWebSiteTask.ConfigureAwait(true);      

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

                    List<Tuple<string, string, int>> sortedResults = termResults.OrderBy(result => result.Item3).ToList();

                    for (int iTermResult = 0; iTermResult < sortedResults.Count; iTermResult++)
                    {
                        TextBlock title = new TextBlock()
                        {
#pragma warning disable CA1305 // Specify IFormatProvider
                            Text = " Polarity: (" + sortedResults[iTermResult].Item3.ToString() + "): " + sortedResults[iTermResult].Item1
#pragma warning restore CA1305 // Specify IFormatProvider
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

        public void FillResultsDictionary(int websiteIdx, IEnumerable<IElement> articleLinksList, Func<IElement, Tuple<string, string>> CleanUpResults)
        {
            if (articleLinksList == null)
                return;

            foreach (var result in articleLinksList)
            {
                Tuple<string, string> urlTitleTuple = CleanUpResults(result);
                string url = urlTitleTuple.Item1;
                string articleTitle = urlTitleTuple.Item2;

                foreach (var term in queryTerms)
                {
                    if (!string.IsNullOrWhiteSpace(articleTitle) && !string.IsNullOrWhiteSpace(url))
                    {
#pragma warning disable CA1304 // Specify CultureInfo
                        List<string> tokenizedTitle = OpenNLP.APIOpenNLP.TokenizeSentence(articleTitle).Select(token => token.ToLower()).ToList();
#pragma warning restore CA1304 // Specify CultureInfo

#pragma warning disable CA1304 // Specify CultureInfo
                        if (!tokenizedTitle.Contains(term.ToLower()))
#pragma warning restore CA1304 // Specify CultureInfo
                            continue;

                        int sentencePolarity = sentencePolarity = OpenNLP.APIOpenNLP.AFINNAnalysis(tokenizedTitle.ToArray());

                        if (!listTermToScrapeDictionary[websiteIdx].ContainsKey(term))
                            listTermToScrapeDictionary[websiteIdx][term] = new List<Tuple<string, string, int>>();

                        listTermToScrapeDictionary[websiteIdx][term].Add(new Tuple<string, string, int>(articleTitle, url, sentencePolarity));
                    }
                }
            }
        }

        private static async Task<IDocument> CreateHTMLDocumentFromSummaryAsync(DynamicallyScrapedWebsite scrapedWebsite)
        {
            var context = BrowsingContext.New();
            var document = await context.OpenNewAsync().ConfigureAwait(true);

            foreach (var documentResult in scrapedWebsite.scrapingResults)
            {
                var titleElement = document.CreateElement("h2");
                titleElement.TextContent = documentResult.title + Environment.NewLine;
                document.Body.AppendChild(titleElement);

                if (documentResult.topWords.Count > 0)
                {
                    var topWordsElement = document.CreateElement("h3");
                    topWordsElement.TextContent += "Most important words in document: ";
                    for (int iWordIdx = 0; iWordIdx < documentResult.topWords.Count; iWordIdx++)
                    {
                        topWordsElement.TextContent += documentResult.topWords[iWordIdx];

                        if (iWordIdx != documentResult.topWords.Count - 1)
                            topWordsElement.TextContent += ", ";
                        else
                            topWordsElement.TextContent += ".";
                    }

                    topWordsElement.TextContent += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                    document.Body.AppendChild(topWordsElement);
                }

                var summaryElement = document.CreateElement("p");
                summaryElement.TextContent = documentResult.contentSummary + Environment.NewLine;
                document.Body.AppendChild(summaryElement);
            }

            return document;
        }

        private void LoadUpSupportedWebsites()
        {
            scrapedWebsites.Add(new HackerNews("https://thehackernews.com/"));
        }

        private void ScrapWebsiteEvent(object sender, RoutedEventArgs e)
        {
            listTermToScrapeDictionary.Clear();
            spinnerControl.Visibility = System.Windows.Visibility.Visible;

            bool noPagesCast = int.TryParse(numberOfPagesTextBox.Text, out numberOfPages);
            bool subdigraphSizeCast = int.TryParse(subdigraphSizeTextbox.Text, out int subdigraphSize);
            bool maxConnectionsCast = int.TryParse(maxConnectionsTextbox.Text, out int maxConnections);
            bool word2VecMaxCountCast = int.TryParse(word2VecCountToLoadTextbox.Text, out int word2VecMaxCount);
            if (!noPagesCast || !subdigraphSizeCast || !maxConnectionsCast || !word2VecMaxCountCast)
                return;

            queryTerms = queryTermsTextBox.Text.Split(';').ToList();

            if (dynamicScrapingCheckbox.IsChecked == true)
            {
                scrapedWebsites.RemoveAll(scrapedWebsite => scrapedWebsite is DynamicallyScrapedWebsite);
                scrapedWebsites.Add(new DynamicallyScrapedWebsite(targetWebsiteTextbox.Text, subdigraphSize, maxConnections, word2VecMaxCount, UpdateTextBoxWithStatus));
                DynamicScraping();
            }
            else
                StaticScraping();
        }

        private void UpdateResults()
        {
            string results = string.Empty;
            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                DynamicallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as DynamicallyScrapedWebsite;
                if (scrapedWebsite == null)
                    continue;
                ConcatenateDataForPrint(scrapedWebsite.scrapingResults, ref results);
            }
            File.WriteAllText(pathToResults, results); 
        }

        private void UpdateTextBoxWithStatus(string textToPrint)
        {
            scrapingStatusTextbox.Text += textToPrint + Environment.NewLine;
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ToggleScrapingMode(object sender, RoutedEventArgs e)
        {
            dynamicScrapingGroupBox.IsEnabled = dynamicScrapingCheckbox.IsChecked == true;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void ScrapeForTemplateIsChecked(object sender, RoutedEventArgs e)
        {
            scrapeOnlyForTemplate = scrapeOnlyForTemplateCheckbox.IsChecked == true;

            if (scrapeOnlyForTemplate)
                queryTermsTextBox.IsEnabled = false;
            else
                queryTermsTextBox.IsEnabled = true;
        }

        private void onAsk(object sender, RoutedEventArgs e)
        {
            var question = questionTextbox.Text;
            var siteUrl = targetWebsiteTextbox.Text;

            new Thread(async () =>
            {
                  Dispatcher.Invoke(() =>
                  {
                      spinnerControl.Visibility = System.Windows.Visibility.Visible;
                  });

                  SiteDatabaseManager.DeserializeSiteInformation();

                  var tokenizedQuestion = APIOpenNLP.TokenizeSentence(question);
                  var filteredQuestionList = tokenizedQuestion.Where(word => !StopWords.GetStopWordsList().Contains(word.ToLower())).ToList();

                  filteredQuestionList.Remove(".");
                  filteredQuestionList.Remove("?");
                  filteredQuestionList.Remove("!");
                  filteredQuestionList.Remove(";");
                  var filteredQuestion = filteredQuestionList.ToArray();

                  // Find the best fit page for this question.
                  double bestFitPageSimilarity = Double.NegativeInfinity;
                  string bestFitPageURL = null;
                  foreach (var databaseEntry in SiteDatabaseManager.extractionDatabase)
                  {
                      var topWordsInEntry = databaseEntry.topWords.ToArray();
                      float similarity = Word2VecManager.GetSentencesSimilarity(filteredQuestion, topWordsInEntry);
                      if (similarity > bestFitPageSimilarity)
                      {
                          bestFitPageSimilarity = similarity;
                          bestFitPageURL = databaseEntry.pageUrl;
                      }
                  }

                  var document = await MainUtils.GetSubPageFromLink(bestFitPageURL, siteUrl).ConfigureAwait(true);
                  if (document == null)
                      return;

                  List<DocumentScrapingResult> scrapingResults = new List<DocumentScrapingResult>();
                  List<WebPage> webPagesList = new List<WebPage>
                  {
                      new WebPage(document, bestFitPageURL)
                  };

                  NoiseFilteringManager.NodeFiltering(webPagesList, ref scrapingResults);
                  NoiseFilteringManager.ApplyNLPFiltering(ref scrapingResults);

                  string results = string.Empty;

                  foreach (var result in scrapingResults)
                  {
                    var contentList = result.scrapingResults.Select(scrapingResult => scrapingResult.element.TextContent).ToList();

                    if (contentList.Count == 0)
                        continue;

                    results += contentList.Aggregate((a,b) => a + b);
                  }

                  var tokenizedSentences = APIOpenNLP.SplitSentences(results);

                  double bestFitSentenceSimilarity = Double.NegativeInfinity;
                  string bestFitSentence = null;
                  foreach (var sentence in tokenizedSentences)
                  {
                    if (sentence == null)
                        continue;

                    var tokenizedSentence = APIOpenNLP.TokenizeSentence(sentence);
                    if (tokenizedSentence == null || tokenizedSentence.Length < NoiseFilteringManager.minimumSentenceSize)
                        continue;

                    float similarity = Word2VecManager.GetSentencesSimilarity(filteredQuestion, tokenizedSentence) / tokenizedSentence.Length;

                    if (similarity > bestFitSentenceSimilarity)
                    {
                        bestFitSentenceSimilarity = similarity;
                        bestFitSentence = sentence;
                    }
                  }

                  Dispatcher.Invoke(() =>
                  {
                      scrapingStatusTextbox.Text = bestFitSentence;
                      spinnerControl.Visibility = System.Windows.Visibility.Collapsed;
                  });

              }).Start();
        }
    }
}
