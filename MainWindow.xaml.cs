using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using csDBPF;
using csDBPF.Entries;
using System.IO;
using Azure;
using Azure.AI.Translation.Text;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

//<Window x:Class="SC4LTEXTT.MainWindow"
namespace SC4LTEXTT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        internal readonly Version releaseVersion = new Version(0, 1);

        /// <summary>
        /// Stores information about a game-supported language.
        /// </summary>
        public struct LanguageItem {
            /// <summary>
            /// The offset from the base LTEXT Group ID defines the language to use.
            /// </summary>
            public byte Offset {get; set;}
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
        private readonly List<LanguageItem> languages = new List<LanguageItem>();

        /// <summary>
        /// Item of the list box, consisting of the LTEXT entry plus other parameters.
        /// </summary>
        private class ListBoxItem {
            public DBPFEntryLTEXT Entry { get; set; }

            private bool _isTranslated;
            public bool IsTranslated {
                get { return _isTranslated; }
                set {
                    _isTranslated = value;
                    if (_isTranslated) {
                        ForeColor = Brushes.Black;
                        BackColor = Brushes.LightGoldenrodYellow;
                    } else {
                        ForeColor = Brushes.Black; 
                        BackColor = Brushes.Transparent;
                    }
                }
            }

            public SolidColorBrush ForeColor { get; private set; }
            public SolidColorBrush BackColor { get; private set; }

            public ListBoxItem(DBPFEntry entry, bool isTranslated) {
                Entry = (DBPFEntryLTEXT) entry;
                _isTranslated = isTranslated;
                ForeColor = Brushes.Black;
                BackColor = Brushes.Transparent;
            }
            public ListBoxItem(DBPFEntryLTEXT entry, bool isTranslated) {
                Entry = entry;
                _isTranslated = isTranslated;
                ForeColor = Brushes.Black;
                BackColor = Brushes.Transparent;
            }
        }
        private List<ListBoxItem> listitems = new List<ListBoxItem>();



        private DBPFFile? _openFile;
        private int _selectedIndex;
        private byte _selectedLanguageOffset;

        //https://stackoverflow.com/a/47596613/10802255
        //https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-templating-overview?view=netframeworkdesktop-4.8
        //public ObservableCollection<ListItem> LTEXTItems { get; set; }

        private string[] originalTranslations = []; //Stores the output returned from Azure
        private string[] modifiedTranslations = []; //Stores the user-modified translation output


        public MainWindow() {
            Title = "SC4 LTEXT Translator - " + releaseVersion.ToString();
            InitializeComponent();
            SetGameLanguages();
            

            TranslateTo.ItemsSource = languages.Skip(1);
            TranslateButton.IsEnabled = false;
            AddLtextsToCurrentFile.IsEnabled = false;
            //===================================================
            //Add input for input language detection
            //Add option for translation for any language or just sc4-supported languages
        }

        /// <summary>
        /// Initialize the available in-game languages.
        /// </summary>
        private void SetGameLanguages() {
            languages.Add(new LanguageItem(0x00, "", "Default")); //(used if localized LTEXT file is missing)
            languages.Add(new LanguageItem(0x01, "en-US", "US English"));
            languages.Add(new LanguageItem(0x02, "en-GB", "UK English"));
            languages.Add(new LanguageItem(0x03, "fr", "French"));
            languages.Add(new LanguageItem(0x04, "de", "German"));
            languages.Add(new LanguageItem(0x05, "it", "Italian"));
            languages.Add(new LanguageItem(0x06, "es", "Spanish"));
            languages.Add(new LanguageItem(0x07, "nl", "Dutch"));
            languages.Add(new LanguageItem(0x08, "da", "Danish"));
            languages.Add(new LanguageItem(0x09, "sv", "Swedish"));
            languages.Add(new LanguageItem(0x0A, "no", "Norwegian"));
            languages.Add(new LanguageItem(0x0B, "fi", "Finnish"));

            languages.Add(new LanguageItem(0x0F, "ja", "Japanese"));
            languages.Add(new LanguageItem(0x10, "pl", "Polish"));
            languages.Add(new LanguageItem(0x11, "zh-hans", "Simplified Chinese"));
            languages.Add(new LanguageItem(0x12, "zh-hant", "Traditional Chinese"));
            languages.Add(new LanguageItem(0x13, "th", "Thai"));
            languages.Add(new LanguageItem(0x14, "ko", "Korean"));
            languages.Add(new LanguageItem(0x23, "pt", "Portuguese (Brazilian)"));
        }



        private void ChooseFile_Click(object sender, RoutedEventArgs e) {
            listitems.Clear();
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true) {
                FileName.Content = Path.GetFileName(dialog.FileName);

                _openFile = new DBPFFile(dialog.FileName);
                List<DBPFEntry> ltexts = _openFile.GetEntries(DBPFTGI.LTEXT);
                foreach (DBPFEntry entry in ltexts) {
                    entry.Decode();
                    listitems.Add(new ListBoxItem(entry, false));
                }
                ListofLTEXTs.ItemsSource = listitems;
                originalTranslations = new string[ltexts.Count];
                modifiedTranslations = new string[ltexts.Count];
                ListofLTEXTs.Items.Refresh();

                TranslateButton.IsEnabled = (TranslateTo.SelectedItem is not null);


            } else {
                return;
            }

        }



        private void TranslateText_Click(object sender, RoutedEventArgs e) {
            AzureKeyCredential credential = new(File.ReadAllText("C:\\source\\repos\\AzureTranslateAPIKey.txt"));
            TextTranslationClient client = new(credential, "global");

            if (TranslateTo.SelectedItem is null) {
                return;
            }

            try {
                string targetLanguage = languages.Where(r => r.Offset == _selectedLanguageOffset).First().ISO;
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


                TranslationOutput.Text = translation?.Translations?.FirstOrDefault().Text;
                originalTranslations[_selectedIndex] = TranslationOutput.Text;
                modifiedTranslations[_selectedIndex] = TranslationOutput.Text;

                //Update formatting of the item in the listbox
                listitems[_selectedIndex].IsTranslated = true;
                ListofLTEXTs.Items.Refresh();


                //Console.WriteLine($"Detected languages of the input text: {translation?.DetectedLanguage?.Language} with score: {translation?.DetectedLanguage?.Score}.");
                //Console.WriteLine($"Text was translated to: '{translation?.Translations?.FirstOrDefault().To}' and the result is: '{translation?.Translations?.FirstOrDefault()?.Text}'.");
            }
            catch (RequestFailedException exception) {
                MessageBox.Show($"{exception.ErrorCode}: {exception.Message}", "Translation Error!", MessageBoxButton.OK);
            }
        }



        private void ListofLTEXTs_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (ListofLTEXTs.SelectedItem is not null ) {originalTranslations[_selectedIndex] = TranslationOutput.Text;
                ListBoxItem? selectedItem = (ListBoxItem) ListofLTEXTs.SelectedItem;
                _selectedIndex = ListofLTEXTs.SelectedIndex;
                TranslationInput.Text = selectedItem.Entry.Text;
                TranslationOutput.Text = modifiedTranslations[_selectedIndex];
            }
        }



        private void RevertChanges_Click(object sender, RoutedEventArgs e) {
            TranslationOutput.Text = originalTranslations[_selectedIndex];
        }



        /// <summary>
        /// Save the modified translations when focus to the output textbox is lost
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TranslationOutput_LostFocus(object sender, RoutedEventArgs e) {
            modifiedTranslations[_selectedIndex] = TranslationOutput.Text;
        }



        private void TranslateTo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            _selectedLanguageOffset = Convert.ToByte(TranslateTo.SelectedItem.ToString()?.Substring(2, 2), 16);
            TranslateButton.IsEnabled = true;
        }



        private void SaveLtextsToNewFile_Click(object sender, RoutedEventArgs e) {

        }
    }
}