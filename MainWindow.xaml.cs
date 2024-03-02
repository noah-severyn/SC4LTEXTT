using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using csDBPF;
using csDBPF.Entries;
using System.IO;
using Azure;
using Azure.AI.Translation.Text;
using System.Collections.ObjectModel;

namespace SC4LTEXTT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {


        public struct lang {
            public string iso;
            public string desc;

            public lang(string iso, string desc) {
                this.iso = iso;
                this.desc = desc;
            }
        }

        public Dictionary<byte, lang> languages = new Dictionary<byte, lang>();
        public DBPFFile dbpf;
        private List<DBPFEntry> ltexts = new List<DBPFEntry>();
        //public ObservableCollection<DBPFEntry> LTEXTEntries {
        //    get { return new ObservableCollection<DBPFEntry>(ltexts); }
        //}
        public ObservableCollection<DBPFEntry> LTEXTEntries { get; set; }
        private string _selectedText;



        public MainWindow() {
            //SC4 built in languages
            //Azure translation language tags are BCP 47 (IETF) https://en.wikipedia.org/wiki/IETF_language_tag
            languages.Add(0x00, new lang("en-US", "Default")); //(used if localized LTEXT file Is missing)
            languages.Add(0x01, new lang("en-US", "US English"));
            languages.Add(0x02, new lang("en-GB", "UK English"));
            languages.Add(0x03, new lang("fr", "French"));
            languages.Add(0x04, new lang("de", "Gernam"));
            languages.Add(0x05, new lang("it", "Italina"));
            languages.Add(0x06, new lang("es", "Spanish"));
            languages.Add(0x07, new lang("nl", "Dutch"));
            languages.Add(0x08, new lang("da", "Danish"));
            languages.Add(0x09, new lang("sv", "Swedish"));
            languages.Add(0x0A, new lang("no", "Norwegian"));
            languages.Add(0x0B, new lang("fi", "Finnish"));

            languages.Add(0x0F, new lang("ja", "Japanese"));
            languages.Add(0x10, new lang("pl", "Polish"));
            languages.Add(0x11, new lang("zh-hans", "Simplified Chinese"));
            languages.Add(0x12, new lang("zh-hant", "Traditional Chinese"));
            languages.Add(0x13, new lang("th", "Thai")); 
            languages.Add(0x14, new lang("ko", "Korean"));
            languages.Add(0x23, new lang("pt", "Portuguese (Brazilian)"));



            


            dbpf = new DBPFFile("C:\\Users\\Administrator\\Documents\\SimCity 4\\Plugins\\Fixed Underfunded Notices (Med-High).dat");
            ltexts = dbpf.GetEntries(DBPFTGI.LTEXT);
            foreach (DBPFEntry entry in ltexts) {
                entry.Decode();
            }

            
            InitializeComponent();
            //https://stackoverflow.com/a/47596613/10802255
            //https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-templating-overview?view=netframeworkdesktop-4.8
            DataContext = this;
            LTEXTEntries = new ObservableCollection<DBPFEntry>(ltexts);
        }

        private void TranslateText_Click(object sender, RoutedEventArgs e) {
            AzureKeyCredential credential = new(File.ReadAllText("C:\\source\\repos\\AzureTranslateAPIKey.txt"));
            TextTranslationClient client = new(credential, "global");



            try {
                string targetLanguage = TranslateTo.Text;
                string inputText = _selectedText;

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

                
                //foreach (TranslatedTextItem translationItem in translations) {
                //    output = output + translationItem.Translations?.FirstOrDefault().Text + " ";
                //}

                TranslationOutput.Text = translation?.Translations?.FirstOrDefault().Text;

                //Console.WriteLine($"Detected languages of the input text: {translation?.DetectedLanguage?.Language} with score: {translation?.DetectedLanguage?.Score}.");
                //Console.WriteLine($"Text was translated to: '{translation?.Translations?.FirstOrDefault().To}' and the result is: '{translation?.Translations?.FirstOrDefault()?.Text}'.");
            }
            catch (RequestFailedException exception) {
                Console.WriteLine($"Error Code: {exception.ErrorCode}");
                Console.WriteLine($"Message: {exception.Message}");
            }
        }

        private void ListofLTEXTs_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (ListofLTEXTs.SelectedItems is not null) {
                _selectedText = ((DBPFEntryLTEXT) ListofLTEXTs.SelectedItems[0]).Text;
                TranslationInput.Text = _selectedText;
            }
            
        }
    }
}