﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Controls;
using System.Windows.Documents;
using WindowsTweaker.AppTasks;
using WindowsTweaker.Models;
using EnglishStemmer;
using Iveonik.Stemmers;

namespace WindowsTweaker.Search {
    internal class Searcher {
        private readonly Dictionary<string, List<SearchItem>> _termUiDictionary;
        private readonly MainWindow _window;
        private readonly LocalizationHandler.Language _language;

        internal Searcher(MainWindow window) {
            _window = window;
            _language = LocalizationHandler.GetCurrentLanguage();
            _termUiDictionary = LoadSearchDictionary();
            _termUiDictionary = AddTextToSearchItems(_termUiDictionary);
        }

        internal List<SearchItem> Search(string term, int count=10) {
            if (_termUiDictionary == null || !_termUiDictionary.Any()) return null;
            Dictionary<SearchItem, int> searchItemAndCntDict = GetMatches(term.Split(' '));
            return searchItemAndCntDict != null && searchItemAndCntDict.Any() ? 
                (from entry in searchItemAndCntDict orderby entry.Value descending select entry).
                Select(keyValuePair => keyValuePair.Key).ToList() : null;
        }

        private Dictionary<SearchItem, int> GetMatches(string[] individualTerms) {
            Dictionary<SearchItem, int> searchItemAndCntDict = new Dictionary<SearchItem, int>(new SearchItemEqualityComparer());
            foreach (string individualTerm in individualTerms) {
                string stemmedTxt = StemTerm(individualTerm);
                List<SearchItem> searchItems = GetMatchingSearchItems(stemmedTxt);
                if (searchItems == null || !searchItems.Any()) continue;
                foreach (SearchItem searchItem in searchItems) {
                    if (searchItemAndCntDict.ContainsKey(searchItem)) {
                        searchItemAndCntDict[searchItem]++;
                    } else {
                        searchItemAndCntDict[searchItem] = 1;
                    }
                }
            }
            return searchItemAndCntDict;
        }

        private string StemTerm(string term) {
            string massagedTerm = term.Trim().ToLower();
            if (String.IsNullOrEmpty(massagedTerm)) return null;
            switch (_language) {
                case LocalizationHandler.Language.English:
                    EnglishWord englishWord = new EnglishWord(massagedTerm);
                    return englishWord.Stem;
                case LocalizationHandler.Language.German:
                    GermanStemmer germanStemmer = new GermanStemmer();
                    return germanStemmer.Stem(massagedTerm);
                case LocalizationHandler.Language.Russian:
                    RussianStemmer russianStemmer = new RussianStemmer();
                    return russianStemmer.Stem(massagedTerm);
                default:
                    return massagedTerm;
            }
        }

        private List<SearchItem> GetMatchingSearchItems(string stemmedTxt) {
            if (_termUiDictionary == null || !_termUiDictionary.Any()) return null;
            if (_termUiDictionary.ContainsKey(stemmedTxt)) return _termUiDictionary[stemmedTxt];
            List<SearchItem> matchedSearchItems = new List<SearchItem>();
            foreach (KeyValuePair<string, List<SearchItem>> keyValuePair in _termUiDictionary.
                    Where(keyValuePair => keyValuePair.Key.StartsWith(stemmedTxt) || stemmedTxt.StartsWith(keyValuePair.Key))) {
                matchedSearchItems.AddRange(keyValuePair.Value);
            }
            return matchedSearchItems;
        }

        private Dictionary<string, List<SearchItem>> AddTextToSearchItems(Dictionary<string, List<SearchItem>> termUiDictionary) {
            if (termUiDictionary == null || !termUiDictionary.Any())
                return null;
            foreach (KeyValuePair<string, List<SearchItem>> keyValuePair in termUiDictionary) {
                List<SearchItem> searchItemList = keyValuePair.Value;
                foreach (SearchItem searchItem in searchItemList) {
                    object uiControl = _window.FindName(searchItem.UiElement);
                    if (uiControl is CheckBox) {
                        CheckBox chkBox = (CheckBox)uiControl;
                        searchItem.UiElementText = chkBox.Content is TextBlock ? ((TextBlock)chkBox.Content).Text : chkBox.Content.ToString();
                    } else if (uiControl is RadioButton) {
                        RadioButton rbtn = (RadioButton)uiControl;
                        searchItem.UiElementText = rbtn.Content is TextBlock ? ((TextBlock)rbtn.Content).Text : rbtn.Content.ToString();
                    } else if (uiControl is TextBlock) {
                        searchItem.UiElementText = ((TextBlock)uiControl).Text;
                    } else if (uiControl is TabItem) {
                        searchItem.UiElementText = ((TabItem)uiControl).Header.ToString();
                    } else if (uiControl is Hyperlink) {
                        Hyperlink hyperlink = (Hyperlink) uiControl;
                        Run run = hyperlink.Inlines.FirstInline as Run;
                        searchItem.UiElementText = run != null ? run.Text : "";
                    } else if (uiControl is Button) {
                        searchItem.UiElementText = ((Button) uiControl).Content.ToString();
                    }
                }
            }
            return termUiDictionary;
        }

        private Dictionary<string, List<SearchItem>> LoadSearchDictionary() {
            Dictionary<string, List<SearchItem>> termUiDictionary;
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = GetResourceName();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName)) {
                if (stream == null) return null;
                using (StreamReader reader = new StreamReader(stream)) {
                    JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
                    termUiDictionary = javaScriptSerializer.Deserialize<Dictionary<string, List<SearchItem>>>(reader.ReadToEnd());
                }
            }
            return ConvertStringKeyToNativeString(termUiDictionary);
        }

        private string GetResourceName() {
            switch (_language) {
                case LocalizationHandler.Language.English:
                    return Constants.EnglishSearchInputFileName;
                case LocalizationHandler.Language.German:
                    return Constants.GermanSearchInputFileName;
                case LocalizationHandler.Language.Russian:
                    return Constants.RussianSearchInputFileName;
                default:
                    return Constants.EnglishSearchInputFileName;
            }
        }

        private Dictionary<string, List<SearchItem>> ConvertStringKeyToNativeString(Dictionary<string, List<SearchItem>> termUiDictionary) {
            if (termUiDictionary == null || !termUiDictionary.Any()) return null;
            if (_language != LocalizationHandler.Language.German && _language != LocalizationHandler.Language.Russian) return termUiDictionary;
            Dictionary<string, List<SearchItem>> newTermUiDictionary = new Dictionary<string, List<SearchItem>>();
            foreach (KeyValuePair<string, List<SearchItem>> keyValuePair in termUiDictionary) {
                newTermUiDictionary[AsNativeString(keyValuePair.Key)] = keyValuePair.Value;
            }
            return newTermUiDictionary;
        } 

        private string AsNativeString(string utf8EncodedStr) {
            if (String.IsNullOrEmpty(utf8EncodedStr)) return null;
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(utf8EncodedStr);
            return Encoding.UTF8.GetString(utf8Bytes);
        }
    }
}