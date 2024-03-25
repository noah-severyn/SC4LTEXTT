# SC4LTEXTT : SC4 LTEXT Translator
An assistant for translating Simcity 4 [LTEXT](https://wiki.sc4devotion.com/index.php?title=LTEXT) files.

To use, select a file containing the LTEXT files you wish to translate. Choose the language you wish to translate to, and translate each of the desired LTEXT files. Each LTEXT will turn yellow to indicate it has been translated in the currently selected language. The number of languages each base LTEXT has been translated to is also tracked. The program supports manual translation or automatic translation through the Azure AI Text Translation service. The output of the AI translation can be manually adjusted. An internet connection is required for the automatic translations.

For the time being, the only supported operation is saving the LTEXT files to a new .dat file. Only the translated LTEXTs will be saved to the new file. Each LTEXT GID will be incremented by the appropriate offset for the translated language. Currently all LTEXT files are written uncompressed.

It is important to note that due to [reported issues](https://community.simtropolis.com/forums/topic/761192-please-help-mz-crime-and-police-language-translations/?do=findComment&comment=1762752) with how Reader handles text encoding, certain 4-byte characters will appear as garbage data when viewed in the program. This applies to all characters of languages such as Chinese, Korean, Thai, and Japanese, but also certain diacriticals for European languages too. **Do not save these files with Reader**. If you do, the correctly functioning (but merely incorrectly displayed) characters will be overwritten by Reader with garbage characters.

Numerous improvements to come.

![](/img/ProductImage.png)
