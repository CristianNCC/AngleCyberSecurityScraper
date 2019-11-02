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
        public bool analyzeNamedEntities = false;
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

                    if (analyzeNamedEntities)
                    {
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
                    }

                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                    results += "========================================================= END ==============================================================";
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                }

                string test1 = "Overthepastfewmonths,hundredsofAndroidusershavebeencomplainingonlineofanewpieceofmysteriousmalwarethathidesontheinfecteddevicesandcanreportedlyreinstallitselfevenafterusersdeleteit,orfactoryresettheirdevices.DubbedXhelper,themalwarehasalreadyinfectedmorethan45,000Androiddevicesinjustthelastsixmonthsandiscontinuingtospreadbyinfectingatleast2,400devicesonanaverageeachmonth,accordingtothelatestreportpublishedtodaybySymantec.Herebelow,IhavecollectedexcerptsfromsomecommentsthataffecteduserssharedontheonlineforumswhileaskingforhowtoremovetheXhelperAndroidmalware:\"xhelperregularlyreinstallsitself,almosteveryday!\"\"the'installappsfromunknownsources'settingturnsitselfon.\"\"Irebootedmyphoneandalsowipedmyphoneyettheappxhelpercameback.\"\"Xhelpercamepre-installedonthephonefromChina.\"\"don'tbuycheapbrandphones.\"FromWhereXhelperAndroidMalwareComes?ThoughtheSymantecresearchersdidnotfindtheexactsourcefromwherethemaliciousapppackedwiththeXhelpermalwarecomesinthefirstplace,thesecurityfirmdidsuspectthatamalicioussystemapppre-installedonAndroiddevicesfromcertainbrandsactuallydownloadedthemalware.removeXhelperandroidmalware\"NoneofthesamplesweanalysedwereavailableontheGooglePlayStore,andwhileitispossiblethattheXhelpermalwareisdownloadedbyusersfromunknownsources,webelievethatmaynotbetheonlychannelofdistribution,\"Symantecresearcherswriteinitsreport.\"Fromourtelemetry,wehaveseentheseappsinstalledmorefrequentlyoncertainphonebrands,whichleadsustobelievethattheattackersmaybefocusingonspecificbrands.\"InaseparatereportpublishedtwomonthsagobyMalwarebytes,researchersbelievedthattheXhelpermalwareisbeingspreadby\"webredirects\"or\"othershadywebsites\"thatpromptuserstodownloadappsfromuntrustedthird-partysources.HowDoestheXhelperMalwareWork?Onceinstalled,Xhelperdoesn'tprovidearegularuserinterface;instead,itgetsinstalledasanapplicationcomponentthatdoesn'tshowuponthedevice'sapplicationlauncherinanattempttoremainhiddenfromtheusers.Inordertolaunchitself,Xhelperreliesonsomeexternaleventstriggeredbyusers,likeconnectingordisconnectingtheinfecteddevicefromapowersupply,rebootingadevice,orinstallingoruninstallinganapp.Oncelaunched,themalwareconnectstoitsremotecommand-and-controlserveroveranencryptedchannelanddownloadsadditionalpayloadssuchasdroppers,clickers,androotkitsonthecompromisedAndroiddevices.\"WebelievethepoolofmalwarestoredontheC&Cservertobevastandvariedinfunctionality,givingtheattackermultipleoptions,includingdatatheftorevencompletetakeoverofthedevice,\"theresearcherssay.TheresearchersbelievethatthesourcecodeofXhelperisstillaworkinprogress,assomeofits\"oldervariantsincludedemptyclassesthatwerenotimplementedatthetime,butthefunctionalityisnowfullyenabled.\"TheXhelpermalwarehasbeenseentargetingAndroidsmartphoneusersprimarilyinIndia,theUnitedStates,andRussia.ThoughmanyantivirusproductsforAndroiddetecttheXhelpermalware,theyareyetnotabletopermanentlyremoveorblockitfromgettingitselfreinstalledontheinfecteddevices.Sincethesourceofthemalwareisstillunclear,Androidusersarerecommendedtotakesimplebuteffectiveprecautionslike:keepdevicesandappsup-to-date,avoidappdownloadsfromunfamiliarsources,alwayspaycloseattentiontothepermissionsrequestedbyapps,frequentlybackupdata,andinstallagoodantivirusappthatprotectsagainstthismalwareandsimilarthreats.Havesomethingtosayaboutthisarticle?CommentbeloworshareitwithusonFacebook,TwitterorourLinkedInGroup.";
                string string1 = dynamicScrapingResultList.First().content.Replace(Environment.NewLine, "");
                string string2 = string1.Replace(" ", "");
                int distance1 = Utils.ComputeLevenshteinDistance(test1, string2);

                string test2 = "—77,758acres,65 % containment—Atleast352structuresdestroyed,55damaged—Noreporteddeathsormissingpersons—1,630structuresthreatened—4injuries(firefighters)Theweathercontinuestocooperate.CalmconditionsallowedfirefighterstomakemoreprogresscontainingtheKincadeFireovernight.CalFireannouncedat7a.m.Fridaythattheblazeisnow68 % contained,andtheacreageremains77,758.\"Accesstothenorthernpartofthefireremainschallengingbecauseofsteepterrainandnarrowroads,butfirefighterswillcontinuetobuildontheheadwaytheyhavebeenmakingwithevenmorecontrollinesbeingestablished,\"CalFiresaidinastatementThursdaymorning.EvacuationordershavebeenliftedfortensofthousandsofresidentsinSonomaCounty,includinginthetownsofHealdsburgandWindsor,inGeyservillesouthofCanyonRoadandinportionsofSantaRosa,Larkfield,RinconValleyandFulton.Someoftheselocationsremainunder\"advisoryevacuation,\"meaningpeoplecanreturnhomeattheirownrisk.\"Becauseoftheprogress,repopulationplanningeffortsareunderway,\"CalFiresaid.Whilethemajorityofthe186,000peopleevacuatedatthefire'speakhavebeenallowedtoreturnhome,thefollowingzonesarestillunderevacuationorder:—Zone1B:WestofLakeCountyLine,NorthandEastofHighway128.SouthofCloverdale,EastofAstiRoad/GeyservilleAveatCanyonRoad.IncludingAstiRoad.—Zone3C:AreaSouthofHighway128andthefireline,EastofWindsorTownlimits,NorthofFaughtRoadatShilohRoadandtheZone5Bboundary.—Zone5B:AreaSouthofHighway128andYellowJacketRanchRoad,WestofHighway128andtheZone6boundarytotheZone3Cboundary,includingareasaccessedEastofShilohRidgeRoadatMayacamaClubDrive.FindevacuationandrepopulationinformationontheSoCoEmergencywebsite.JeffOhs,aCalFiresectionchief,saidattheThursdaymorningoperationsmeetingthateffortswerefocusedonthefire'seasternedgenearLakeCounty,theMountStHelenaareaand\"acrossthebottomofthefireandupneartheHighway101corridor.\"Wehaveasignificantnumberofgroundtroopsthereandtheirprimarilymissionistogoaftereverythingtheyseesmoking,\"Ohssaid.\"Therewillbesmokes.Youwillseethemthenextweekorso,andafterthattheyshoulddiminish.\"ThefirefightingeffortturnedacorneronWednesdaywhenawindeventwasn'tassevereasitcouldhavebeen.\"Thewindsthatweweretalkingaboutyesterdaydidnotmaterializetotheextremesthatwewerefearfulof,andthatgaveusabigopportunitytoincreasethatcontainmentovernightandagaintoday,\"saidCalFireDivisionChiefJonathanCoxataWednesdaynightpressconference.Whilethere'snorainintheseven-dayforecast,weatherconditionsareexpectedtobegenerallyfavorableintotheweekendandlikelyintonextweek.\"Mainlylightwindsanddryconditionsareexpectedthroughatleasttheendofnextweek,/ \"saidtheNationalWeatherForecastinitsBayAreaforecast.TheKincadeFireisnowthelargestfireeverinSonomaCounty.TheblazestartedOct.23northeastofGeyservilleatJohnKincadeRoadandBurnedMountainRoad.CalFireofficialssaytheyhopetohavetheblazefullycontainedbyNov.7.Overthecourseofthefire,helicoptershavedroppedmorethan2.1milliongallonsofwaterandtankershaveletlooseover1milliongallonsofretardant.AmyGraffisadigitaleditorwithSFGATE.Emailheratagraff@sfgate.com.";
                string string21 = dynamicScrapingResultList[1].content.Replace(Environment.NewLine, "");
                string string22 = string21.Replace(" ", "");
                int distance2 = Utils.ComputeLevenshteinDistance(test2, string22);

                string test3 = "AstoryhasbeenmakingtheroundsontheInternetsinceyesterdayaboutacyberattackonanIndiannuclearpowerplant.Duetosomeexpertscommentaryonsocialmediaevenafterlackofinformationabouttheeventandoverreactionsbymany,theincidentreceivedfactuallyincorrectcoveragewidelysuggestingapieceofmalwarehascompromised\"mission-criticalsystems\"attheKudankulamNuclearPowerPlant.Relax!That'snotwhathappened.Theattackmerelyinfectedasystemthatwasnotconnectedtoanycriticalcontrolsinthenuclearfacility.HerewehavesharedatimelineoftheeventswithbriefinformationoneverythingweknowsofaraboutthecyberattackatKudankulamNuclearPowerPlant(KKNPP)inTamilNadu.Fromwherethisnewscame?ThestorystartedwhenIndiansecurityresearcherPukhrajSinghtweetedthatheinformedIndianauthoritiesafewmonthsagoaboutaninformation-stealingmalware,dubbedDtrack,whichsuccessfullyhit\"extremelymission-criticaltargets\"atKudankulamNuclearPowerPlant.AccordingtoPukhraj,themalwaremanagedtogaindomaincontroller-levelaccessatthenuclearfacility.WhatistheDtrackmalware(linkedtotheNorthKoreanhackers)?AccordingtoapreviousreportpublishedbyresearchersatKaspersky,DtrackisaremoteaccessTrojan(RAT)intendedtospyonitsvictimsandinstallvariousmaliciousmodulesonthetargetedcomputers,including:keylogger,browserhistorystealer,functionsthatcollecthostIPaddress,informationaboutavailablenetworksandactiveconnections,listofallrunningprocesses,andalsothelistofallfilesonallavailablediskvolumes.Dtrackallowsremoteattackerstodownloadfilestothevictim'scomputer,executemaliciouscommands,uploaddatafromthevictim'scomputertoaremoteservercontrolledbyattackers,andmore.Accordingtotheresearchers,DtrackmalwarewasdevelopedbytheLazarusGroup,ahackinggroupbelievedtobeworkingonbehalfofNorthKorea'sstatespyagency.HowdidtheIndianGovernmentrespond?ImmediatelyafterPukhraj'stweet,manyTwitterusersandIndianoppositionpoliticians,includingCongressMPShashiTharoor,demandedanexplanationfromtheIndianGovernmentabouttheallegedcyberattack—whichitneverdisclosedtothepublic.Inresponsetotheinitialmediareports,theNuclearPowerCorporationofIndia(NPCIL),agovernment-ownedentity,onTuesdayreleasedanofficialstatement,denyinganycyberattackonthecontrolsystemofthenuclearpowerplant.\"ThisistoclarifyKudankulamNuclearPowerPlant(KNPP)andotherIndianNuclearPowerPlantsControlarestand-aloneandnotconnectedtooutsidecybernetworkandInternet.Anycyber-attackontheNuclearPowerPlantControlSystemisnotpossible,\"theNPCILstatementreads.Tobehonest,thestatementisfactuallycorrect,exceptthe\"notpossible\"part,asPukhrajwasalsotalkingaboutthecompromiseoftheadministrativeITnetwork,notthecriticalsystemsthatcontrolthepowerplant.IndianGovernmentlateracknowledgedthecyberattack,but...However,whileprimarilyaddressingfalsemediareportsandrumorsofStuxnetlikemalwareattack,theNPCIL,intentionallyorunintentionally,leftanimportantquestionunanswered:Ifnotcontrolsystems,thenwhichsystemswereactuallycompromised?Whentheabsolutedenialbackfired,NPCILonWednesdayreleasedasecondstatement,confirmingthattherewasindeedacyberattack,butitwaslimitedonlytoanInternet-connectedcomputerusedforadministrativepurposes,whichisisolatedfromanymission-criticalsystematthenuclearfacility.\"IdentificationofmalwareintheNPCILsystemiscorrect.ThematterwasconveyedbyCERT-InwhenitwasnoticedbythemonSeptember4,2019,\"theNPCILstatementreads.\"TheinvestigationrevealedthattheinfectedPCbelongedtoauserwhowasconnectedtotheInternet-connectednetwork.Thisisisolatedfromthecriticalinternalnetwork.Thenetworksarebeingcontinuouslymonitored.\"ThoughNorthKoreanhackersdevelopedthemalware,theIndianGovernmenthasnotyetattributedtheattacktoanygrouporcountry.Whatcouldattackershaveachieved?Forsecurityreasons,controlprocessingtechnologiesatnuclearpowerplantsaretypicallyisolatedfromtheInternetoranyothercomputersthatareconnectedtotheInternetorexternalnetwork.Suchisolatedsystemsarealsotermedasair-gappedcomputersandarecommoninproductionormanufacturingenvironmentstomaintainagapbetweentheadministrativeandoperationalnetworks.CompromisinganInternet-connectedadministrativesystemdoesn'tallowhackerstomanipulatetheair-gappedcontrolsystem.Still,itcertainlycouldallowattackerstoinfectothercomputersconnectedtothesamenetworkandstealinformationstoredinthem.Ifwethinklikeahackerwhowantstosabotageanuclearfacility,thefirststepwouldbecollectingasmuchinformationaboutthetargetedorganizationaspossible,includingtypeofdevicesandequipmentbeingusedinthefacility,todeterminethenextpossiblewaystojumpthroughairgaps.TheDtrackmalwarecouldbethefirstphaseofabiggercyber-attackthat,fortunately,getspottedandraisedthealarmbeforecausinganychaos.However,ithasnotyetbeenrevealed,byresearchersortheGovernment,thatwhatkindofdatathemalwarewasabletosteal,analysisofwhichcouldbehelpfultoshedmorelightonthegravityoftheincident.TheHackerNewswillupdatethearticlewhenmoreinformationbecomesavailableonthisincident.StayTuned!Havesomethingtosayaboutthisarticle?CommentbeloworshareitwithusonFacebook,TwitterorourLinkedInGroup.";
                string string31 = dynamicScrapingResultList[2].content.Replace(Environment.NewLine, "");
                string string32 = string31.Replace(" ", "");
                int distance3 = Utils.ComputeLevenshteinDistance(test3, string32);

                string test4 = "Finally,fortheveryfirsttime,anencryptedmessagingserviceprovideristakinglegalactionagainstaprivateentitythathascarriedoutmaliciousattacksagainstitsusers.FacebookfiledalawsuitagainstIsraelimobilesurveillancefirmNSOGrouponTuesday,allegingthatthecompanywasactivelyinvolvedinhackingusersofitsend-to-endencryptedWhatsAppmessagingservice.Earlierthisyear,itwasdiscoveredthatWhatsApphadacriticalvulnerabilitythatattackerswerefoundexploitinginthewildtoremotelyinstallPegasusspywareontargetedAndroidandiOSdevices.Theflaw(CVE-2019-3568)successfullyallowedattackerstosilentlyinstallthespywareappontargetedphonesbymerelyplacingaWhatsAppvideocallwithspeciallycraftedrequests,evenwhenthecallwasnotanswered.DevelopedbyNSOGroup,Pegasusallowsaccesstoanincredibleamountofdatafromvictims'smartphonesremotely,includingtheirtextmessages,emails,WhatsAppchats,contactdetails,callsrecords,location,microphone,andcamera.PegasusisNSO'ssignatureproductthathaspreviouslybeenusedagainstseveralhumanrightsactivistsandjournalists,fromMexicototheUnitedArabEmiratestwoyearsago,andAmnestyInternationalstaffersinSaudiArabiaandanotherSaudihumanrightsdefenderbasedabroadearlierlastyear.ThoughNSOGroupalwaysclaimsitlegallysellsitsspywareonlytogovernmentswithnodirectinvolvement,WhatsAppheadWillCathcartsaysthecompanyhasevidenceofNSOGroup'sdirectinvolvementintherecentattacksagainstWhatsAppusers.NSOGroupViolatedWhatsApp'sTermsofServiceInalawsuitfiled(PDF)inU.S.DistrictCourtinSanFranciscotoday,FacebooksaidNSOGrouphadviolatedWhatsApp'stermsofservicesbyusingitsserverstospreadthespywaretoapproximately1,400mobiledevicesduringanattackinAprilandMaythisyear.Thecompanyalsobelievesthattheattacktargeted\"atleast100membersofcivilsociety,whichisanunmistakablepatternofabuse,\"thoughitsaysthisnumbermaygrowhigherasmorevictimscomeforward.\"Thisattackwasdevelopedtoaccessmessagesaftertheyweredecryptedonaninfecteddevice,abusingin-appvulnerabilitiesandtheoperatingsystemsthatpowerourmobilephones,\"Facebook-ownedWhatsAppsaidinablogpost.\"Defendants(attackers)createdWhatsAppaccountsthattheyusedandcausedtobeusedtosendmaliciouscodetoTargetDevicesinAprilandMay2019.Theaccountswerecreatedusingtelephonenumbersregisteredindifferentcounties,includingCyprus,Israel,Brazil,Indonesia,Sweden,andtheNetherlands.\"Thetargetedusersincludeattorneys,journalists,humanrightsactivists,politicaldissidents,diplomats,andotherseniorforeigngovernmentofficials,withWhatsAppnumbersfromdifferentcountrycodes,includingtheKingdomofBahrain,theUnitedArabEmirates,andMexico.WhatsAppsaidthecompanysentawarningnotetoalltheaffected1,400usersimpactedbythisattack,directlyinformingthemaboutwhathappened.FacebookhasalsonamedNSOGroup'sparentcompany'QCyberTechnologies'asaseconddefendantinthecase.\"ThecomplaintallegestheyviolatedbothU.S.andCalifornialawsaswellastheWhatsAppTermsofService,whichprohibitsthistypeofabuse,\"thelawsuitstates.Now,thecompanyhassuedNSOGroupundertheUnitedStatesstateandfederallaws,includingtheComputerFraudandAbuseAct,aswellastheCaliforniaComprehensiveComputerDataAccessandFraudAct.Havesomethingtosayaboutthisarticle?CommentbeloworshareitwithusonFacebook,TwitterorourLinkedInGroup.";
                string string41 = dynamicScrapingResultList[3].content.Replace(Environment.NewLine, "");
                string string42 = string41.Replace(" ", "");
                int distance4 = Utils.ComputeLevenshteinDistance(test4, string42);
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
