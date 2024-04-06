using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using csDBPF;
using Azure;
using Azure.AI.Translation.Text;
using Microsoft.Win32;
using System.Windows.Data;

namespace SC4LTEXTT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        internal readonly Version releaseVersion = new Version(0, 4);
        private DBPFFile? _selectedFile;
        private TranslationItem _selectedListBoxItem;
        //private byte _langOffset;
        private LanguageItem _selectedLanguage;


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



        private readonly List<TranslationItem> _translationItems = new List<TranslationItem>();
        private class TranslationItem {
            /// <summary>
            /// TGI of the base (default) translation.
            /// </summary>
            public TGI BaseTGI { get; set; }
            /// <summary>
            /// Indicates which language this item is.
            /// </summary>
            public LanguageItem Language { get; set; }
            /// <summary>
            /// TGI of this item, incorporating the language offset.
            /// </summary>
            public TGI ThisTGI { get; set; }
            /// <summary>
            /// Contains the translation from the imported LTEXT file.
            /// </summary>
            public string DefaultTranslation { get; set; }
            /// <summary>
            /// Contains the output returned from Azure translation.
            /// </summary>
            public string AzureTranslation { get; set; }
            /// <summary>
            /// Contains the user-modified translation output.
            /// </summary>
            public string ModifiedTranslation { get; set; }

            public SolidColorBrush BackColor { get; set; }

            public TranslationItem(TGI baseTGI, TGI thisTGI, LanguageItem language, string defaultText, string azureText = "", string modifiedText = "") {
                BaseTGI = baseTGI;
                ThisTGI = thisTGI;
                Language = language;
                DefaultTranslation = defaultText;
                AzureTranslation = azureText;
                ModifiedTranslation = modifiedText;
                BackColor = Brushes.Transparent;
            }
        }



        public MainWindow() { 
            InitializeComponent();
            FillGameLanguageList();
            Title = "SC4 LTEXT Translator - " + releaseVersion.ToString();

            TranslateTo.ItemsSource = _languages.Skip(1);
            TranslateButton.IsEnabled = false;

            _selectedListBoxItem = (TranslationItem) ListOfTranslations.SelectedItem;
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
            ListOfTranslations.Items.Refresh();
            try {
                OpenFileDialog dialog = new OpenFileDialog();
                if (dialog.ShowDialog() == true) {
                    FileName.Content = Path.GetFileName(dialog.FileName);

                    _selectedFile = new DBPFFile(dialog.FileName);
                    List<DBPFEntry> allLTEXTs = _selectedFile.GetEntries(DBPFTGI.LTEXT);
                    allLTEXTs = allLTEXTs.OrderBy(t => t.TGI.GroupID).ThenBy(t => t.TGI.InstanceID).ToList();
                    allLTEXTs.DecodeEntries();

                    
                    //Since everything is sorted, make the first ltext is set as the base of the set. Every ltext with a GID within 0x23 (35) of the base is grouped as a translation of the base and added to the set.
                    uint offset = 0;
                    int translationIdx;
                    LanguageItem lang;
                    for (int baseIdx = 0; baseIdx < allLTEXTs.Count; baseIdx++) {
                        translationIdx = baseIdx + 1;
                        offset = 0;
                        lang = GetLanguage(offset);

                        _translationItems.Add(new TranslationItem(allLTEXTs[baseIdx].TGI, allLTEXTs[baseIdx].TGI, lang, ((DBPFEntryLTEXT) allLTEXTs[baseIdx]).Text));
                        if (translationIdx >= allLTEXTs.Count) { break; }
                        while (allLTEXTs[translationIdx].TGI.GroupID - allLTEXTs[baseIdx].TGI.GroupID <= 0x23) {
                            offset = allLTEXTs[translationIdx].TGI.GroupID - allLTEXTs[baseIdx].TGI.GroupID;
                            lang = GetLanguage(offset);
                            _translationItems.Add(new TranslationItem(allLTEXTs[baseIdx].TGI, allLTEXTs[translationIdx].TGI, lang, defaultText:((DBPFEntryLTEXT) allLTEXTs[baseIdx]).Text, modifiedText:((DBPFEntryLTEXT) allLTEXTs[translationIdx]).Text));
                            
                            translationIdx++;
                        }
                        baseIdx += translationIdx - 1;
                    }
                    ListOfTranslations.ItemsSource = _translationItems;

                    CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(ListOfTranslations.ItemsSource);
                    PropertyGroupDescription groupDescription = new PropertyGroupDescription("BaseTGI");
                    view.GroupDescriptions.Add(groupDescription);

                    _selectedListBoxItem = (TranslationItem) ListOfTranslations.Items[0];
                    TranslateButton.IsEnabled = (TranslateTo.SelectedItem is not null);
                    ListOfTranslations.Items.Refresh();
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
        }



        private void FetchTranslation_Click(object sender, RoutedEventArgs e) {
            //To minimize hits to the API don't retranslate an item if it has already been translated --- I store this as an "original translation" so it can be reset to "default" later on if needed

            AzureKeyCredential credential = new(Credentials.ApiKey);
            TextTranslationClient client = new(credential, "global");

            try {
                string targetLanguage = _selectedLanguage.ISO;
                string inputText = TranslationInput.Text;

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

                TGI newTGI = new TGI(_selectedListBoxItem.BaseTGI.TypeID, _selectedListBoxItem.BaseTGI.GroupID + _selectedLanguage.Offset, _selectedListBoxItem.BaseTGI.InstanceID);

                //TODO - check if the translated item already exists before adding a new one
                //============================================================================================================================================================================
                _translationItems.Add(new TranslationItem(_selectedListBoxItem.BaseTGI, newTGI, _selectedLanguage, inputText, translatedText, translatedText));

                TranslationOutput.Text = translatedText;
                ListOfTranslations.Items.Refresh();
                //============================================================================================================================================================================
                //TODO - fix selected item
                //ListOfTranslations.SelectedItem = ListOfTranslations.Items.GetItemAt(ListOfTranslations.Items.Count - 1);

                CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(ListOfTranslations.ItemsSource);
                view.Refresh();
            }
            catch (RequestFailedException exception) {
                TranslationOutput.Text = $"{exception.ErrorCode}: {exception.Message}";
            } catch (Exception ex) {
                TranslationOutput.Text = $"Error: {ex.Message}\r\n {ex.StackTrace}";
            }
        }



        private void ListOfTranslations_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (ListOfTranslations.SelectedItem is not null ) {
                _selectedListBoxItem = (TranslationItem) ListOfTranslations.SelectedItem;


                TranslationInput.Text = _selectedListBoxItem.DefaultTranslation;
                TranslationOutput.Text = _selectedListBoxItem.ModifiedTranslation;
            }
        }



        private void RevertChanges_Click(object sender, RoutedEventArgs e) {
            TranslationOutput.Text = _selectedListBoxItem.AzureTranslation;
            _selectedListBoxItem.ModifiedTranslation = _selectedListBoxItem.AzureTranslation;
        }



        /// <summary>
        /// Save the modified translations when focus to the output textbox is lost
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TranslationOutput_LostFocus(object sender, RoutedEventArgs e) {
            if (_selectedListBoxItem is not null) {
                if (_selectedLanguage.Offset == 0x00) {
                    MessageBox.Show("Please choose a language to save this translation as!", "No Language Chosen", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _selectedListBoxItem.ModifiedTranslation = TranslationOutput.Text;

                ////if the original translation is not found, then the user manually translated instead of fetching the AI translation
                //bool origFound = _selectedListBoxItem.OriginalTranslations.TryGetValue(_langOffset, out _);
                //if (!origFound) {
                //    _selectedListBoxItem.OriginalTranslations.Add(_langOffset, TranslationOutput.Text);
                //    _selectedListBoxItem.HasTranslations = true;
                //}
                ListOfTranslations.Items.Refresh();
            }
        }



        private void TranslateTo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            _selectedLanguage = (LanguageItem) TranslateTo.SelectedItem;
            TranslateButton.IsEnabled = true;
        }



        /// <summary>
        /// Write the translated LTEXT subfiles to a new file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveLtextsToNewFile_Click(object sender, RoutedEventArgs e) {
            if (_selectedLanguage.Offset == 0x00) {
                MessageBox.Show("Please choose a language to save this translation as!", "No Language Chosen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
            if (_selectedLanguage.Offset == 0x00) {
                MessageBox.Show("Please choose a language to save this translation as!", "No Language Chosen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedFile is null) return;
            SaveLTEXTs(_selectedFile);
        }


        private void SaveLTEXTs(DBPFFile file) {
            //try {
            //    ListBoxItem item;
            //    DBPFEntryLTEXT newEntry;

            //    for (int idx = 0; idx < _listBoxItems.Count; idx++) {
            //        item = _listBoxItems[idx];
            //        if (item.ModifiedTranslations.Count > 0) {
            //            TGI baseTGI = item.BaseEntry.TGI;
            //            foreach (byte offset in item.ModifiedTranslations.Keys) {
            //                newEntry = new DBPFEntryLTEXT(new TGI(baseTGI.TypeID, baseTGI.GroupID + offset, baseTGI.InstanceID), item.ModifiedTranslations[offset]);
            //                file.AddEntry(newEntry);
            //            }
            //        }
            //    }
            //    if (file.CountEntries() > 0) {
            //        file.EncodeAllEntries();
            //        file.Save();
            //    }
            //}
            //catch (Exception ex) {
            //    TranslationOutput.Text = $"Error: {ex.Message}\r\n {ex.StackTrace}";
            //}
        }
    }
}