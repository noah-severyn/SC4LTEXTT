using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using csDBPF;
using Azure;
using Azure.AI.Translation.Text;
using Microsoft.Win32;
using System.Windows.Data;
using System.Linq;
using System.ComponentModel;

namespace SC4LTEXTT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        internal readonly Version releaseVersion = new Version(0, 4);
        private DBPFFile? _selectedFile;
        private TranslationItem _selectedListBoxItem;
        private LanguageItem _selectedLanguage;
        private readonly SolidColorBrush WarningColor = new SolidColorBrush(Color.FromRgb(245, 245, 163));
        private readonly SolidColorBrush ChangedColor = new SolidColorBrush(Color.FromRgb(184, 245, 163));


        /// <summary>
        /// Stores information about a game-supported language.
        /// </summary>
        public struct LanguageItem {
            /// <summary>
            /// The offset from the base LTEXT Group ID defines the language to use.
            /// </summary>
            public byte Offset { get; set; }
            /// <summary>
            /// Language tag. Azure translation language tags are <see href="https://en.wikipedia.org/wiki/IETF_language_tag">BCP 47 (IETF)</see>.
            /// </summary>
            public string ISO { get; set; }
            /// <summary>
            /// Language name (and detail, if appropriate).
            /// </summary>
            public string Desc { get; set; }

            public LanguageItem(byte offset, string iso, string desc) {
                Offset = offset;
                ISO = iso;
                Desc = desc;
            }
            public override readonly string ToString() {
                return $"0x{Offset.ToString("x2")}: {ISO} - {Desc}";
            }
        }
        private static readonly List<LanguageItem> _languages = [];


        private readonly CollectionView view;
        private readonly List<TranslationItem> _translationItems = new List<TranslationItem>();
        private class TranslationItem {
            /// <summary>
            /// TGI of the base (default) translation. Use to lookup the text to start with for creating a new translation.
            /// </summary>
            public TGI BaseTGI { get; set; }
            /// <summary>
            /// The language of this tranlsation.
            /// </summary>
            public LanguageItem Language { get; set; }
            /// <summary>
            /// The TGI of this translation, incorporating the language offset.
            /// </summary>
            public TGI ThisTGI { get; set; }
            /// <summary>
            /// The translation from the imported LTEXT file.
            /// </summary>
            public string ImportedTranslation { get; set; }
            /// <summary>
            /// The output returned from Azure translation. Saved separately in case the user wants to revert customizations made (to reduce calls to the API to re-translate).
            /// </summary>
            public string AzureTranslation { get; set; }
            /// <summary>
            /// The user-modified translation output. If this is empty then this item has not been translated or modified this session.
            /// </summary>
            public string ModifiedTranslation { get; set; }
            /// <summary>
            /// If the base translation has been changed then signal that this translation needs to be updated accordingly.
            /// </summary>
            public bool OutOfDate { get; set; }

            public SolidColorBrush BackColor { get; set; }

            public TranslationItem(TGI baseTGI, TGI thisTGI, LanguageItem language, string defaultText, string azureText = "", string modifiedText = "") {
                BaseTGI = baseTGI;
                ThisTGI = thisTGI;
                Language = language;
                ImportedTranslation = defaultText;
                AzureTranslation = azureText;
                ModifiedTranslation = modifiedText;
                BackColor = Brushes.Transparent;
            }
        }



        public MainWindow() { 
            InitializeComponent();
            FillGameLanguageList();
            Title = "SC4 LTEXT Translator - " + releaseVersion.ToString();

            TranslateTo.ItemsSource = _languages;

            _selectedListBoxItem = (TranslationItem) ListOfTranslations.SelectedItem;
            ListOfTranslations.ItemsSource = _translationItems;

            view = (CollectionView) CollectionViewSource.GetDefaultView(ListOfTranslations.ItemsSource);
            view.GroupDescriptions.Add(new PropertyGroupDescription("BaseTGI"));
            view.SortDescriptions.Add(new SortDescription("ThisTGI.GroupID", ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription("ThisTGI.InstanceID", ListSortDirection.Ascending));
            //============================================================================================================================================================================
            //TODO - Add input for input language detection
            //TODO - Add option for translation for any language or just sc4-supported languages
        }

        /// <summary>
        /// Initialize the available in-game languages.
        /// </summary>
        private static void FillGameLanguageList() {
            _languages.Add(new LanguageItem(0x00, "", "Default")); //(used if localized LTEXT file is missing)
            _languages.Add(new LanguageItem(0x01, "en-US", "US English"));
            _languages.Add(new LanguageItem(0x02, "en-GB", "UK English"));
            _languages.Add(new LanguageItem(0x03, "fr", "French"));
            _languages.Add(new LanguageItem(0x04, "de", "German"));
            _languages.Add(new LanguageItem(0x05, "it", "Italian"));
            _languages.Add(new LanguageItem(0x06, "es", "Spanish"));
            _languages.Add(new LanguageItem(0x07, "nl", "Dutch"));
            _languages.Add(new LanguageItem(0x08, "da", "Danish"));
            _languages.Add(new LanguageItem(0x09, "sv", "Swedish"));
            _languages.Add(new LanguageItem(0x0A, "no", "Norwegian"));
            _languages.Add(new LanguageItem(0x0B, "fi", "Finnish"));

            _languages.Add(new LanguageItem(0x0F, "ja", "Japanese"));
            _languages.Add(new LanguageItem(0x10, "pl", "Polish"));
            _languages.Add(new LanguageItem(0x11, "zh-hans", "Simplified Chinese"));
            _languages.Add(new LanguageItem(0x12, "zh-hant", "Traditional Chinese"));
            _languages.Add(new LanguageItem(0x13, "th", "Thai"));
            _languages.Add(new LanguageItem(0x14, "ko", "Korean"));
            _languages.Add(new LanguageItem(0x23, "pt", "Portuguese (pt-br)")); //Portuguese(Brazilian)
        }
        public static LanguageItem GetLanguage(uint offset) {
            return _languages.Find(item => item.Offset == offset);
        }



        private void ChooseFile_Click(object sender, RoutedEventArgs e) {
            _translationItems.Clear();
            TranslationInput.Clear();
            TranslationOutput.Clear();

            try {
                OpenFileDialog dialog = new OpenFileDialog();
                if (dialog.ShowDialog() == true) {
                    FileName.Content = Path.GetFileName(dialog.FileName);

                    _selectedFile = new DBPFFile(dialog.FileName);
                    List<DBPFEntry> allLTEXTs = _selectedFile.GetEntries(DBPFTGI.LTEXT);
                    allLTEXTs.DecodeEntries();

                    
                    //Since everything is sorted, make the first ltext is set as the base of the set. Every ltext with a GID within 0x23 (35) of the base is grouped as a translation of the base and added to the set.
                    uint offset = 0;
                    int translationIdx;
                    string defaultText;
                    LanguageItem lang;
                    for (int baseIdx = 0; baseIdx < allLTEXTs.Count; baseIdx++) {
                        translationIdx = baseIdx + 1;
                        offset = 0;
                        lang = GetLanguage(offset);
                        defaultText = ((DBPFEntryLTEXT) allLTEXTs[baseIdx]).Text;

                        _translationItems.Add(new TranslationItem(allLTEXTs[baseIdx].TGI, allLTEXTs[baseIdx].TGI, lang, defaultText));
                        if (translationIdx == allLTEXTs.Count) { break; }
                        while (allLTEXTs[translationIdx].TGI.GroupID - allLTEXTs[baseIdx].TGI.GroupID <= 0x23 && allLTEXTs[translationIdx].TGI.InstanceID == allLTEXTs[baseIdx].TGI.InstanceID) {
                            offset = allLTEXTs[translationIdx].TGI.GroupID - allLTEXTs[baseIdx].TGI.GroupID;
                            lang = GetLanguage(offset);
                            _translationItems.Add(new TranslationItem(allLTEXTs[baseIdx].TGI, allLTEXTs[translationIdx].TGI, lang, ((DBPFEntryLTEXT) allLTEXTs[translationIdx]).Text, string.Empty, string.Empty));
                            translationIdx++;
                        }
                        if (translationIdx - baseIdx > 1) {
                            baseIdx += translationIdx - 1;
                        }
                        
                    }
                    TranslateButton.IsEnabled = false;
                    TranslateTo.IsEnabled = false;
                } else {
                    return;
                }
            }
            catch (Exception ex) {
                ListOfTranslations.Items.Refresh();
                if (_selectedFile is not null) {
                    TranslationInput.Text = $"Error: Could not decode LTEXT file in `{_selectedFile.File.Name}` at position {_translationItems.Count}. File load terminated.\r\n{ex.Message}\r\n{ex.StackTrace}";
                }
            }

            view.Refresh();
        }



        private void FetchTranslation_Click(object sender, RoutedEventArgs e) {
            if (_selectedListBoxItem is null) {
                MessageBox.Show("Please select a LTEXT item first.", "", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            //To minimize hits to the API don't retranslate an item if it has already been translated --- I store this as an "original translation" so it can be reset to "default" later on if needed
            TranslationItem? thisTranslation = GetTranslation(_selectedListBoxItem.BaseTGI, _selectedLanguage.Offset);
            if (thisTranslation != null) {
                if (thisTranslation.AzureTranslation != string.Empty && thisTranslation.OutOfDate == false) {
                    return;
                }
            }

            AzureKeyCredential credential = new(Credentials.ApiKey);
            TextTranslationClient client = new(credential, "global");

            try {
                string targetLanguage = _selectedLanguage.ISO;
                TranslationItem baseTranslation = GetBaseTranslation(_selectedListBoxItem.BaseTGI);
                string inputText;
                if (baseTranslation.ModifiedTranslation == string.Empty) {
                    inputText = baseTranslation.ImportedTranslation;
                } else {
                    inputText = baseTranslation.ModifiedTranslation;
                }
                //Response<IReadOnlyList<TranslatedTextItem>> response = await client.TranslateAsync(targetLanguage, inputText).ConfigureAwait(false);
                //StringBuilder alllLangs = new StringBuilder();
                //List<string> langs = new List<string>();
                //List<string> texts = new List<string>();
                //foreach (lang item in languages.Values) {
                //    alllLangs.Append("to=" + item.iso + "&");
                //    langs.Add(item.iso);
                //    texts.Add(inputText);
                //}


                //string output = alllLangs.ToString();
                //output = output.Remove(output.Length - 1);
                //output = "to=en-US&to=de&to=es&to=it";


                //https://learn.microsoft.com/en-us/azure/ai-services/translator/language-support#translation
                Response<IReadOnlyList<TranslatedTextItem>> response = client.Translate(targetLanguage, inputText);
                IReadOnlyList<TranslatedTextItem> translations = response.Value;
                TranslatedTextItem? translation = translations.FirstOrDefault();
                string translatedText = translation.Translations.FirstOrDefault().Text;
                if (translatedText is null) {
                    return;
                }
                if (thisTranslation is null) {
                    AddTranslation(inputText, translatedText, translatedText);
                } else {
                    _selectedListBoxItem.AzureTranslation = translatedText;
                    _selectedListBoxItem.ModifiedTranslation = translatedText;
                    _selectedListBoxItem.OutOfDate = false;
                }
                _selectedListBoxItem.BackColor = ChangedColor;

                TranslationOutput.Text = translatedText;
                view.Refresh();
            }
            catch (RequestFailedException exception) {
                TranslationOutput.Text = $"{exception.ErrorCode}: {exception.Message}";
            } catch (Exception ex) {
                TranslationOutput.Text = $"Error: {ex.Message}\r\n {ex.StackTrace}";
            }
        }



        /// <summary>
        /// Save the modified translations when focus to the output textbox is lost
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TranslationOutput_LostFocus(object sender, RoutedEventArgs e) {
            string newTranslation = TranslationOutput.Text;
            if (_selectedListBoxItem is null) {
                MessageBox.Show("Please select a LTEXT item first.","", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            } 
            else if (_selectedListBoxItem.ModifiedTranslation != newTranslation) {
                //Determine if we add a new (manual) translation or adjust existing one
                //This assumption works because otherwise if an item with the same language was found it would have been selected; no translation in selected language found → selected item does not change
                if (_selectedListBoxItem.Language.Offset != _selectedLanguage.Offset) {
                    AddTranslation(string.Empty, string.Empty, newTranslation);
                } else {
                    _selectedListBoxItem.ModifiedTranslation = newTranslation;
                    _selectedListBoxItem.OutOfDate = false;
                }
                _selectedListBoxItem.BackColor = ChangedColor;


                //If the currently changing translation is the base translation then mark all child translations as out of date
                if (_selectedListBoxItem.BaseTGI == _selectedListBoxItem.ThisTGI && _selectedListBoxItem.ImportedTranslation != newTranslation) {
                    foreach (TranslationItem item in _translationItems) {
                        if (item.BaseTGI == _selectedListBoxItem.BaseTGI && item.Language.Offset > 0x00) {
                            item.OutOfDate = true;
                            item.BackColor = WarningColor;
                        }
                    }
                    TranslationInput.Text = newTranslation;
                }
                view.Refresh();
            }
        }


        private bool TranslationExists(TGI baseTGI, byte offset) {
            TranslationItem? result = _translationItems.Find(i => i.BaseTGI == baseTGI && i.Language.Offset == offset);
            return result is not null;
        }
        private TranslationItem? GetTranslation(TGI baseTGI, byte offset) {
            return _translationItems.Find(i => i.BaseTGI == baseTGI && i.Language.Offset == offset);
        }
        private TranslationItem GetBaseTranslation(TGI tgi) {
            TranslationItem? baseTranslation = _translationItems.Find(i => i.ThisTGI == tgi);
            if (baseTranslation is null) {
                throw new NullReferenceException("Could not find the designated default translation with TGI of " + _selectedListBoxItem.BaseTGI);
            }
            return baseTranslation;
        }
        private void AddTranslation(string importedText, string azureText, string translatedText) {
            TGI newTGI = new TGI(_selectedListBoxItem.BaseTGI.TypeID, _selectedListBoxItem.BaseTGI.GroupID + _selectedLanguage.Offset, _selectedListBoxItem.BaseTGI.InstanceID);
            TranslationItem newItem = new TranslationItem(_selectedListBoxItem.BaseTGI, newTGI, _selectedLanguage, importedText, azureText, translatedText);
            _translationItems.Add(newItem);

            ListOfTranslations.SelectedItem = newItem;
            _selectedListBoxItem = newItem;
        }



        private void ListOfTranslations_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            TranslateButton.IsEnabled = false;
            TranslateTo.IsEnabled = false;
            if (ListOfTranslations.SelectedItem is not null ) {
                _selectedListBoxItem = (TranslationItem) ListOfTranslations.SelectedItem;
                TranslationItem baseTranslation = GetBaseTranslation(_selectedListBoxItem.BaseTGI);

                if (baseTranslation.ModifiedTranslation == string.Empty) {
                    TranslationInput.Text = baseTranslation.ImportedTranslation;
                } else {
                    TranslationInput.Text = baseTranslation.ModifiedTranslation;
                }

                if (_selectedListBoxItem.ModifiedTranslation == string.Empty) {
                    TranslationOutput.Text = _selectedListBoxItem.ImportedTranslation;
                } else {
                    TranslationOutput.Text = _selectedListBoxItem.ModifiedTranslation;
                }
                TranslateTo.SelectedItem = _selectedListBoxItem.Language;
            }
        }



        private void RevertChanges_Click(object sender, RoutedEventArgs e) {
            if (_selectedListBoxItem.AzureTranslation == string.Empty) {
                TranslationOutput.Text = _selectedListBoxItem.ImportedTranslation;
                _selectedListBoxItem.ModifiedTranslation = string.Empty;
                _selectedListBoxItem.BackColor = Brushes.Transparent;
                view.Refresh();
            } else {
                TranslationOutput.Text = _selectedListBoxItem.AzureTranslation;
                _selectedListBoxItem.ModifiedTranslation = _selectedListBoxItem.AzureTranslation;
            }

            
        }



        private void TranslateTo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            _selectedLanguage = (LanguageItem) TranslateTo.SelectedItem;
            TranslateButton.IsEnabled = true;

            TranslationItem? result = GetTranslation(_selectedListBoxItem.BaseTGI, _selectedLanguage.Offset);
            if (result is null) {
                TranslationOutput.Text = string.Empty;
            } else {
                ListOfTranslations.SelectedItem = result;
                _selectedListBoxItem = result;
            }
        }



        /// <summary>
        /// Write the translated LTEXT subfiles to a new file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveLtextsToNewFile_Click(object sender, RoutedEventArgs e) {
            string saveAsPath;
            SaveFileDialog dialog = new SaveFileDialog() {
                AddExtension = true,
                DefaultExt = ".dat",
                Filter = "DAT Files|*.dat"
            };
            if (dialog.ShowDialog() == true) {
                saveAsPath = dialog.FileName;
                if (File.Exists(saveAsPath)) {
                    File.Delete(saveAsPath);
                }
            } else {
                return;
            }
            DBPFFile newDBPF = new DBPFFile(saveAsPath);
            SaveLTEXTs(newDBPF);
        }



        /// <summary>
        /// Write the translated LTEXT subfiles to the current file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddLtextsToCurrentFile_Click(object sender, RoutedEventArgs e) {
            if (_selectedFile is null) return;
            SaveLTEXTs(_selectedFile);
        }


        private void SaveLTEXTs(DBPFFile file) {
            try {
                TranslationItem translation;
                DBPFEntryLTEXT newEntry;
                DBPFEntryLTEXT existingEntry;

                for (int idx = 0; idx < _translationItems.Count; idx++) {
                    translation = _translationItems[idx];
                    TGI found = file.GetTGIs().Where(t => t == translation.ThisTGI).FirstOrDefault();
                    
                    //If translation TGI not found add a new entry
                    if (found == default) {
                        newEntry = new DBPFEntryLTEXT(translation.ThisTGI, Coalesce(translation.ModifiedTranslation, translation.ImportedTranslation));
                        file.AddEntry(new DBPFEntryLTEXT(translation.ThisTGI, Coalesce(translation.ModifiedTranslation, translation.ImportedTranslation)));
                    } 
                    
                    //If translation is found, we should update the text if it has been changed
                    else if (translation.ModifiedTranslation != translation.ImportedTranslation && translation.ModifiedTranslation != string.Empty) {
                        existingEntry = (DBPFEntryLTEXT) file.GetEntry(translation.ThisTGI);
                        existingEntry.Text = translation.ModifiedTranslation;
                    }
                }
                if (file.CountEntries() > 0) {
                    file.EncodeAllEntries();
                    file.Save();
                }
            }
            catch (Exception ex) {
                TranslationOutput.Text = $"Error: {ex.Message}\r\n {ex.StackTrace}";
            }
        }

        private static string Coalesce(string text1, string text2) {
            if (text1 == string.Empty) { return text2; } else { return text1; }
        }
    }
}