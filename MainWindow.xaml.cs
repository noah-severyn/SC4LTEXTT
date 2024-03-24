using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using csDBPF;
using csDBPF.Entries;
using Azure;
using Azure.AI.Translation.Text;
using Microsoft.Win32;

namespace SC4LTEXTT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        internal readonly Version releaseVersion = new Version(0, 2);
        //private string[] originalTranslations = []; //Stores the output returned from Azure
        //private string[] modifiedTranslations = []; //Stores the user-modified translation output
        //private Dictionary<int, Dictionary<byte, string>> _originalTranslations = []; 
        //private Dictionary<int, Dictionary<byte, string>> _modifiedTranslations = [];


        private readonly List<ListBoxItem> _listBoxItems = new List<ListBoxItem>();
        private DBPFFile? _selectedFile;
        private ListBoxItem _selectedListBoxItem;
        private int _selectedIndex;
        private byte _langOffset;



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
        private readonly List<LanguageItem> _languages = [];



        /// <summary>
        /// Item of the list box, consisting of the LTEXT entry plus other parameters.
        /// </summary>
        private class ListBoxItem {
            public DBPFEntryLTEXT BaseEntry { get; set; }
            public Dictionary<byte, string> OriginalTranslations { get; set; } //Stores the output returned from Azure
            public Dictionary<byte, string> ModifiedTranslations { get; set; } //Stores the user-modified translation output
            public SolidColorBrush BackColor { get; set; }

            private bool _isTranslated; //Whether this item has been translated to the current language.
            public bool IsTranslated {
                get { return _isTranslated; }
                set {
                    _isTranslated = value;
                    if (ModifiedTranslations.Count == 18) {
                        BackColor = Brushes.YellowGreen;
                    } else if (_isTranslated) {
                        BackColor = Brushes.LightGoldenrodYellow;
                    } else {
                        BackColor = Brushes.Transparent;
                    }
                }
            }


            public ListBoxItem(DBPFEntry entry, bool isTranslated) {
                BaseEntry = (DBPFEntryLTEXT) entry;
                OriginalTranslations = new Dictionary<byte, string>();
                ModifiedTranslations = new Dictionary<byte, string>();
                _isTranslated = isTranslated;
                BackColor = Brushes.Transparent;
            }
            public ListBoxItem(DBPFEntryLTEXT entry, bool isTranslated) {
                BaseEntry = entry;
                OriginalTranslations = new Dictionary<byte, string>();
                ModifiedTranslations = new Dictionary<byte, string>();
                _isTranslated = isTranslated;
                BackColor = Brushes.Transparent;
            }
        }


        public MainWindow() { 
            InitializeComponent();
            FillGameLanguageList();
            Title = "SC4 LTEXT Translator - " + releaseVersion.ToString();

            TranslateTo.ItemsSource = _languages.Skip(1);
            TranslateButton.IsEnabled = false;

            _selectedListBoxItem = (ListBoxItem) ListofLTEXTs.SelectedItem;
            //===================================================
            //Add input for input language detection
            //Add option for translation for any language or just sc4-supported languages
        }

        /// <summary>
        /// Initialize the available in-game languages.
        /// </summary>
        private void FillGameLanguageList() {
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
            _languages.Add(new LanguageItem(0x23, "pt", "Portuguese (Brazilian)"));
        }



        private void ChooseFile_Click(object sender, RoutedEventArgs e) {
            _listBoxItems.Clear();
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true) {
                FileName.Content = Path.GetFileName(dialog.FileName);

                _selectedFile = new DBPFFile(dialog.FileName);
                List<DBPFEntry> ltexts = _selectedFile.GetEntries(DBPFTGI.LTEXT);
                foreach (DBPFEntry entry in ltexts) {
                    entry.Decode();
                    _listBoxItems.Add(new ListBoxItem(entry, false));
                }
                TranslateButton.IsEnabled = (TranslateTo.SelectedItem is not null);
                ListofLTEXTs.ItemsSource = _listBoxItems;
                ListofLTEXTs.Items.Refresh();

                _selectedListBoxItem = (ListBoxItem) ListofLTEXTs.SelectedItem;
                //_originalTranslations.Clear();
                //_modifiedTranslations.Clear();
                //for (int idx = 0; idx < _listOfLTEXTs.Count; idx++) {
                //    _originalTranslations.Add(idx, new Dictionary<byte, string>());
                //    _modifiedTranslations.Add(idx, new Dictionary<byte, string>());
                //}
            } else {
                return;
            }

        }



        private void TranslateText_Click(object sender, RoutedEventArgs e) {
            //To minimize hits to the API don't retranslate an item if it has already been translated --- I store this as an "original translation" so it can be reset to "default" later on if needed
            _listBoxItems[_selectedIndex].OriginalTranslations.TryGetValue(_langOffset, out string? translatedText);
            if (TranslateTo.SelectedItem is null || translatedText is not null) {
                return;
            }

            AzureKeyCredential credential = new(Credentials.ApiKey);
            TextTranslationClient client = new(credential, "global");

            try {
                string targetLanguage = _languages.Where(r => r.Offset == _langOffset).First().ISO;
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
                translatedText = translation.Translations.FirstOrDefault().Text;



                _selectedListBoxItem.OriginalTranslations.Add(_langOffset, translatedText);
                _selectedListBoxItem.ModifiedTranslations.Add(_langOffset, translatedText);
                _selectedListBoxItem.IsTranslated = true;
                //TGI baseTGI = _listBoxItems[_selectedIndex].BaseEntry.TGI;

                //Look for the desired translation in the selected item; if not found add it, and if found update it
                //bool found = _listBoxItems[_selectedIndex].ModifiedTranslations.TryGetValue(_selectedLangOffset, out DBPFEntryLTEXT? translatedEntry);

                //if (!found) {
                //    _listBoxItems[_selectedIndex].ModifiedTranslations.Add(_selectedLangOffset, new DBPFEntryLTEXT(new TGI((uint) baseTGI.TypeID, (uint) baseTGI.GroupID + _selectedLangOffset, (uint) baseTGI.InstanceID), _modifiedTranslations[_selectedIndex]));
                //    _listBoxItems[_selectedIndex].Status = _listBoxItems[_selectedIndex].ModifiedTranslations.Count + "/18";
                //} else if (translatedEntry is not null) {
                //    translatedEntry.Text = _modifiedTranslations[_selectedIndex];
                //}

                TranslationOutput.Text = translatedText;
                ListofLTEXTs.Items.Refresh();
            }
            catch (RequestFailedException exception) {
                TranslationOutput.Text = $"{exception.ErrorCode}: {exception.Message}";
            }
        }



        private void ListofLTEXTs_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (ListofLTEXTs.SelectedItem is not null ) {
                _selectedListBoxItem = (ListBoxItem) ListofLTEXTs.SelectedItem;
                _selectedIndex = ListofLTEXTs.SelectedIndex;


                TranslationInput.Text = _selectedListBoxItem.BaseEntry.Text;
                _selectedListBoxItem.ModifiedTranslations.TryGetValue(_langOffset, out string? modifiedText);
                TranslationOutput.Text = modifiedText ?? string.Empty;
            }
        }



        private void RevertChanges_Click(object sender, RoutedEventArgs e) {
            TranslationOutput.Text = _selectedListBoxItem.OriginalTranslations[_langOffset];
            _selectedListBoxItem.ModifiedTranslations[_langOffset] = TranslationOutput.Text;
        }



        /// <summary>
        /// Save the modified translations when focus to the output textbox is lost
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TranslationOutput_LostFocus(object sender, RoutedEventArgs e) {
            _selectedListBoxItem.ModifiedTranslations[_langOffset] = TranslationOutput.Text;

            //bool found = _listBoxItems[_selectedIndex].ModifiedTranslations.TryGetValue(_selectedLangOffset, out DBPFEntryLTEXT? translatedEntry);
            //if (found && translatedEntry is not null) {
            //    translatedEntry.Text = _modifiedTranslations[_selectedIndex];
            //}
            ListofLTEXTs.Items.Refresh();
        }



        private void TranslateTo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            _langOffset = Convert.ToByte(TranslateTo.SelectedItem.ToString()?.Substring(2, 2), 16);
            TranslationOutput.Text = string.Empty;

            //Refresh the listbox to highlight all the items translated in the chosen language
            foreach (ListBoxItem item in _listBoxItems) {
                item.IsTranslated = item.ModifiedTranslations.ContainsKey(_langOffset);
            }
            ListofLTEXTs.Items.Refresh();



            //Refresh the output with the chosen language
            bool translationFound = _selectedListBoxItem.ModifiedTranslations.TryGetValue(_langOffset, out string? translatedText);
            if (translationFound) {
                TranslationOutput.Text = translatedText;
            }

            TranslateButton.IsEnabled = true;
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
            if (file is null) return;
            ListBoxItem item;
            for (int idx = 0; idx < _listBoxItems.Count; idx++) {
                item = _listBoxItems[idx];
                if (item.IsTranslated) {
                    //file.AddEntries(item.ModifiedTranslations.Values);
                }
            }
            if (file.CountEntries() > 0) {
                file.EncodeAllEntries();
                file.Save();
            }
        }
    }
}