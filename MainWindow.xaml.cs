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

namespace SC4LTEXTT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {


        public struct LanguageItem {
            public byte Offset;
            public string ISO;
            public string Desc;

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
        private List<DBPFEntry> ltexts = new List<DBPFEntry>();
        private DBPFFile? dbpf;
        private int selectedIndex;

        //https://stackoverflow.com/a/47596613/10802255
        //https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-templating-overview?view=netframeworkdesktop-4.8
        //public ObservableCollection<ListItem> LTEXTItems { get; set; }

        private string[] translatedTexts = [];


        public MainWindow() {
            InitializeComponent();
            //SC4 built in languages
            //Azure translation language tags are BCP 47 (IETF) https://en.wikipedia.org/wiki/IETF_language_tag
            languages.Add(new LanguageItem(0x00, "en-US", "Default")); //(used if localized LTEXT file Is missing)
            languages.Add(new LanguageItem(0x01, "en-US", "US English"));
            languages.Add(new LanguageItem(0x02, "en-GB", "UK English"));
            languages.Add(new LanguageItem(0x03, "fr", "French"));
            languages.Add(new LanguageItem(0x04, "de", "Gernam"));
            languages.Add(new LanguageItem(0x05, "it", "Italina"));
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

            TranslateTo.ItemsSource = languages.Skip(1);
        }



        private void TranslateText_Click(object sender, RoutedEventArgs e) {
            AzureKeyCredential credential = new(File.ReadAllText("C:\\source\\repos\\AzureTranslateAPIKey.txt"));
            TextTranslationClient client = new(credential, "global");

            if (TranslateTo.SelectedItem is null) {
                return;
            }

            try {
                string targetLanguage = languages.Where(r => r.Offset == Convert.ToByte(TranslateTo.SelectedItem.ToString()?.Substring(2, 2) )).First().ISO;
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

                Response<IReadOnlyList<TranslatedTextItem>> response = client.Translate(targetLanguage, inputText);
                IReadOnlyList<TranslatedTextItem> translations = response.Value;
                TranslatedTextItem? translation = translations.FirstOrDefault();


                TranslationOutput.Text = translation?.Translations?.FirstOrDefault().Text;
                translatedTexts[selectedIndex] = TranslationOutput.Text;

                //Console.WriteLine($"Detected languages of the input text: {translation?.DetectedLanguage?.Language} with score: {translation?.DetectedLanguage?.Score}.");
                //Console.WriteLine($"Text was translated to: '{translation?.Translations?.FirstOrDefault().To}' and the result is: '{translation?.Translations?.FirstOrDefault()?.Text}'.");
            }
            catch (RequestFailedException exception) {
                MessageBox.Show($"{exception.ErrorCode}: {exception.Message}", "Translation Error!", MessageBoxButton.OK);
            }
        }



        private void ListofLTEXTs_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            DBPFEntryLTEXT? selectedItem = (DBPFEntryLTEXT) ListofLTEXTs.SelectedItems[0];
            if (selectedItem is not null) {
                selectedIndex = (int) selectedItem.IndexPos;
                TranslationInput.Text = selectedItem.Text;
                TranslationOutput.Text = translatedTexts[selectedIndex];
            }
        }



        private void ChooseFile_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true) {
                FileName.Content = Path.GetFileName(dialog.FileName);

                dbpf = new DBPFFile(dialog.FileName);
                ltexts = dbpf.GetEntries(DBPFTGI.LTEXT);
                foreach (DBPFEntry entry in ltexts) {
                    entry.Decode();
                }
                ListofLTEXTs.ItemsSource = ltexts;
                //translatedTexts = new List<string>(ltexts.Count);
                translatedTexts = new string[ltexts.Count];


            } else {
                return;
            }
            
        }
    }
}